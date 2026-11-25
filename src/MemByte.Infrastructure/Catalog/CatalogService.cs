using MemByte.Application.Catalog;
using MemByte.Domain.Catalog;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Catalog;

/// <summary>
/// Gestión del catálogo único de la instalación sobre <see cref="MemByteDbContext"/>.
/// </summary>
public class CatalogService : ICatalogService
{
	private readonly MemByteDbContext _dbContext;

	public CatalogService(MemByteDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<IReadOnlyList<CatalogCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
	{
		return await _dbContext.CatalogCategories.AsNoTracking()
			.OrderBy(c => c.Name).Select(c => ToDto(c)).ToListAsync(cancellationToken);
	}

	public async Task<CatalogCategoryDto?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
	{
		var category = await FindCategoryAsync(categoryId, false, cancellationToken);
		return category is null ? null : ToDto(category);
	}

	public async Task<CatalogOperationResult<CatalogCategoryDto>> CreateCategoryAsync(CreateCatalogCategoryRequest request, CancellationToken cancellationToken = default)
	{
		if (request.ParentCategoryId is Guid parentId && await FindCategoryAsync(parentId, false, cancellationToken) is null)
			return CatalogOperationResult<CatalogCategoryDto>.Failure("La categoría padre indicada no existe.");

		CatalogCategory category;
		try
		{
			category = new CatalogCategory(request.Name, request.ParentCategoryId, request.IsActive);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<CatalogCategoryDto>.Failure(ex.Message);
		}

		_dbContext.CatalogCategories.Add(category);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<CatalogCategoryDto>.Success(ToDto(category));
	}

	public async Task<CatalogOperationResult<CatalogCategoryDto>> UpdateCategoryAsync(Guid categoryId, UpdateCatalogCategoryRequest request, CancellationToken cancellationToken = default)
	{
		var category = await FindCategoryAsync(categoryId, true, cancellationToken);
		if (category is null)
			return CatalogOperationResult<CatalogCategoryDto>.Failure("La categoría no existe.");

		if (request.ParentCategoryId is Guid parentId)
		{
			if (parentId == categoryId)
				return CatalogOperationResult<CatalogCategoryDto>.Failure("Una categoría no puede ser su propio padre.");
			if (await FindCategoryAsync(parentId, false, cancellationToken) is null)
				return CatalogOperationResult<CatalogCategoryDto>.Failure("La categoría padre indicada no existe.");
			if (await IsDescendantAsync(categoryId, parentId, cancellationToken))
				return CatalogOperationResult<CatalogCategoryDto>.Failure("No se puede mover una categoría dentro de su propio subárbol.");
		}

		try
		{
			category.Rename(request.Name);
			category.MoveTo(request.ParentCategoryId);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			return CatalogOperationResult<CatalogCategoryDto>.Failure(ex.Message);
		}

		if (request.IsActive)
			category.Activate();
		else
			category.Deactivate();
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<CatalogCategoryDto>.Success(ToDto(category));
	}

	public async Task<CatalogOperationResult<bool>> DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
	{
		var category = await FindCategoryAsync(categoryId, true, cancellationToken);
		if (category is null)
			return CatalogOperationResult<bool>.Failure("La categoría no existe.");
		if (await _dbContext.CatalogCategories.AsNoTracking().AnyAsync(c => c.ParentCategoryId == categoryId, cancellationToken))
			return CatalogOperationResult<bool>.Failure("No se puede eliminar una categoría que tiene subcategorías.");
		if (await _dbContext.Set<CatalogItem>().AsNoTracking().AnyAsync(i => i.CategoryId == categoryId, cancellationToken))
			return CatalogOperationResult<bool>.Failure("No se puede eliminar una categoría que tiene productos o servicios asociados.");

		_dbContext.CatalogCategories.Remove(category);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<bool>.Success(true);
	}

	public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
	{
		return await _dbContext.Products.AsNoTracking()
			.OrderBy(p => p.Name).Select(p => ToDto(p)).ToListAsync(cancellationToken);
	}

	public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(productId, false, cancellationToken);
		return product is null ? null : ToDto(product);
	}

	public async Task<CatalogOperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
	{
		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(categoryId, false, cancellationToken) is null)
			return CatalogOperationResult<ProductDto>.Failure("La categoría indicada no existe.");

		Product product;
		try
		{
			product = new Product(request.Name, request.Description, new Price(request.PriceAmount, request.PriceCurrencyCode),
				request.CategoryId, request.Sku, request.IsActive, request.UnitOfMeasure, request.TaxRate,
				request.Supplier, request.Brand, request.DeliveryDays);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ProductDto>.Failure(ex.Message);
		}

		_dbContext.Products.Add(product);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<ProductDto>.Success(ToDto(product));
	}

	public async Task<CatalogOperationResult<ProductDto>> UpdateProductAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(productId, true, cancellationToken);
		if (product is null)
			return CatalogOperationResult<ProductDto>.Failure("El producto no existe.");
		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(categoryId, false, cancellationToken) is null)
			return CatalogOperationResult<ProductDto>.Failure("La categoría indicada no existe.");

		try
		{
			// Validar todo antes de modificar la entidad seguida por EF.
			var validated = new Product(request.Name, request.Description, new Price(request.PriceAmount, request.PriceCurrencyCode),
				request.CategoryId, request.Sku, request.IsActive, request.UnitOfMeasure, request.TaxRate,
				request.Supplier, request.Brand, request.DeliveryDays);
			product.Rename(validated.Name);
			product.UpdateDescription(validated.Description);
			product.UpdatePrice(validated.Price);
			product.ChangeCategory(validated.CategoryId);
			product.UpdateSku(validated.Sku);
			product.UpdateCommercialDetails(validated.UnitOfMeasure, validated.TaxRate, validated.Supplier);
			product.UpdateSupplyDetails(validated.Brand, validated.DeliveryDays);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ProductDto>.Failure(ex.Message);
		}

		if (request.IsActive)
			product.Activate();
		else
			product.Deactivate();
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<ProductDto>.Success(ToDto(product));
	}

	public async Task<CatalogOperationResult<bool>> SetProductActiveAsync(Guid productId, bool isActive, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(productId, true, cancellationToken);
		if (product is null)
			return CatalogOperationResult<bool>.Failure("El producto no existe.");
		if (isActive)
			product.Activate();
		else
			product.Deactivate();
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<bool>.Success(isActive);
	}

	public async Task<CatalogOperationResult<bool>> DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default)
	{
		var product = await FindProductAsync(productId, true, cancellationToken);
		if (product is null)
			return CatalogOperationResult<bool>.Failure("El producto no existe.");
		if (await HasOrderLinesAsync(productId, cancellationToken))
			return CatalogOperationResult<bool>.Failure("El producto está incluido en pedidos. Desactívalo para conservar el historial.");
		_dbContext.Products.Remove(product);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<bool>.Success(true);
	}

	public async Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken cancellationToken = default)
	{
		return await _dbContext.Services.AsNoTracking()
			.OrderBy(s => s.Name).Select(s => ToDto(s)).ToListAsync(cancellationToken);
	}

	public async Task<ServiceDto?> GetServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(serviceId, false, cancellationToken);
		return service is null ? null : ToDto(service);
	}

	public async Task<CatalogOperationResult<ServiceDto>> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken = default)
	{
		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(categoryId, false, cancellationToken) is null)
			return CatalogOperationResult<ServiceDto>.Failure("La categoría indicada no existe.");

		Service service;
		try
		{
			service = new Service(request.Name, request.Description, new Price(request.PriceAmount, request.PriceCurrencyCode),
				request.CategoryId, request.DurationMinutes, request.IsRecurring, request.IsActive,
				request.UnitOfMeasure, request.TaxRate, request.Supplier, request.Reference, request.DeliveryMode, request.BillingPeriod);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ServiceDto>.Failure(ex.Message);
		}

		_dbContext.Services.Add(service);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<ServiceDto>.Success(ToDto(service));
	}

	public async Task<CatalogOperationResult<ServiceDto>> UpdateServiceAsync(Guid serviceId, UpdateServiceRequest request, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(serviceId, true, cancellationToken);
		if (service is null)
			return CatalogOperationResult<ServiceDto>.Failure("El servicio no existe.");
		if (request.CategoryId is Guid categoryId && await FindCategoryAsync(categoryId, false, cancellationToken) is null)
			return CatalogOperationResult<ServiceDto>.Failure("La categoría indicada no existe.");

		try
		{
			// Validar todo antes de modificar la entidad seguida por EF.
			var validated = new Service(request.Name, request.Description, new Price(request.PriceAmount, request.PriceCurrencyCode),
				request.CategoryId, request.DurationMinutes, request.IsRecurring, request.IsActive,
				request.UnitOfMeasure, request.TaxRate, request.Supplier, request.Reference, request.DeliveryMode, request.BillingPeriod);
			service.Rename(validated.Name);
			service.UpdateDescription(validated.Description);
			service.UpdatePrice(validated.Price);
			service.ChangeCategory(validated.CategoryId);
			service.UpdateDuration(validated.DurationMinutes);
			service.SetRecurring(validated.IsRecurring);
			service.UpdateCommercialDetails(validated.UnitOfMeasure, validated.TaxRate, validated.Supplier);
			service.UpdateProvisionDetails(validated.Reference, validated.DeliveryMode, validated.BillingPeriod);
		}
		catch (ArgumentException ex)
		{
			return CatalogOperationResult<ServiceDto>.Failure(ex.Message);
		}

		if (request.IsActive)
			service.Activate();
		else
			service.Deactivate();
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<ServiceDto>.Success(ToDto(service));
	}

	public async Task<CatalogOperationResult<bool>> SetServiceActiveAsync(Guid serviceId, bool isActive, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(serviceId, true, cancellationToken);
		if (service is null)
			return CatalogOperationResult<bool>.Failure("El servicio no existe.");
		if (isActive)
			service.Activate();
		else
			service.Deactivate();
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<bool>.Success(isActive);
	}

	public async Task<CatalogOperationResult<bool>> DeleteServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
	{
		var service = await FindServiceAsync(serviceId, true, cancellationToken);
		if (service is null)
			return CatalogOperationResult<bool>.Failure("El servicio no existe.");
		if (await HasOrderLinesAsync(serviceId, cancellationToken))
			return CatalogOperationResult<bool>.Failure("El servicio está incluido en pedidos. Desactívalo para conservar el historial.");
		_dbContext.Services.Remove(service);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return CatalogOperationResult<bool>.Success(true);
	}

	private Task<bool> HasOrderLinesAsync(Guid itemId, CancellationToken cancellationToken) =>
		_dbContext.OrderLines.AsNoTracking().AnyAsync(l => l.CatalogItemId == itemId, cancellationToken);

	private Task<CatalogCategory?> FindCategoryAsync(Guid categoryId, bool tracking, CancellationToken cancellationToken)
	{
		var query = tracking ? _dbContext.CatalogCategories : _dbContext.CatalogCategories.AsNoTracking();
		return query.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
	}

	private Task<Product?> FindProductAsync(Guid productId, bool tracking, CancellationToken cancellationToken)
	{
		var query = tracking ? _dbContext.Products : _dbContext.Products.AsNoTracking();
		return query.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
	}

	private Task<Service?> FindServiceAsync(Guid serviceId, bool tracking, CancellationToken cancellationToken)
	{
		var query = tracking ? _dbContext.Services : _dbContext.Services.AsNoTracking();
		return query.FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
	}

	private async Task<bool> IsDescendantAsync(Guid categoryId, Guid candidateParentId, CancellationToken cancellationToken)
	{
		var categories = await _dbContext.CatalogCategories.AsNoTracking()
			.Select(c => new { c.Id, c.ParentCategoryId }).ToListAsync(cancellationToken);
		var currentId = (Guid?)candidateParentId;
		while (currentId is not null)
		{
			if (currentId == categoryId)
				return true;
			currentId = categories.FirstOrDefault(c => c.Id == currentId)?.ParentCategoryId;
		}
		return false;
	}

	private static CatalogCategoryDto ToDto(CatalogCategory category) => new(
		category.Id, category.Name, category.ParentCategoryId, category.IsActive, category.CreatedAt);

	private static ProductDto ToDto(Product product) => new(
		product.Id, product.Name, product.Description, product.Price.Amount, product.Price.CurrencyCode,
		product.CategoryId, product.Sku, product.IsActive, product.CreatedAt, product.UpdatedAt,
		product.UnitOfMeasure, product.TaxRate, product.Supplier, product.Brand, product.DeliveryDays);

	private static ServiceDto ToDto(Service service) => new(
		service.Id, service.Name, service.Description, service.Price.Amount, service.Price.CurrencyCode,
		service.CategoryId, service.DurationMinutes, service.IsRecurring, service.IsActive, service.CreatedAt, service.UpdatedAt,
		service.UnitOfMeasure, service.TaxRate, service.Supplier, service.Reference, service.DeliveryMode, service.BillingPeriod);
}
