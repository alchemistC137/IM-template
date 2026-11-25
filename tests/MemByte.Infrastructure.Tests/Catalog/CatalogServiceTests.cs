using MemByte.Application.Catalog;
using MemByte.Application.Orders;
using MemByte.Domain.Catalog;
using MemByte.Infrastructure.Catalog;
using MemByte.Infrastructure.Orders;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Tests.Catalog;

/// <summary>
/// Pruebas del catálogo único, persistencia de atributos y dependencias del historial.
/// </summary>
public class CatalogServiceTests
{
	private static MemByteDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<MemByteDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
		return new MemByteDbContext(options);
	}

	private static CreateProductRequest NewProductRequest(string name = "Licencia anual", Guid? categoryId = null) =>
		new(name, "Descripción", 100m, "EUR", categoryId);

	private static CreateServiceRequest NewServiceRequest(string name = "Consultoría", Guid? categoryId = null) =>
		new(name, "Descripción", 50m, "EUR", categoryId);

	[Fact]
	public async Task Catalog_WorksWithoutClients_AndListsAllItemsSorted()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		await service.CreateProductAsync(NewProductRequest("Producto B"));
		await service.CreateProductAsync(NewProductRequest("Producto A"));
		await service.CreateServiceAsync(NewServiceRequest());
		await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Categoría B"));
		await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Categoría A"));
		Assert.Empty(db.Clients);
		Assert.Equal(new[] { "Producto A", "Producto B" }, (await service.GetProductsAsync()).Select(p => p.Name));
		Assert.Equal("Consultoría", Assert.Single(await service.GetServicesAsync()).Name);
		Assert.Equal(new[] { "Categoría A", "Categoría B" }, (await service.GetCategoriesAsync()).Select(c => c.Name));
	}

	[Fact]
	public async Task CreateProductAsync_WithInvalidPrice_Fails()
	{
		await using var db = CreateDbContext();
		var result = await new CatalogService(db).CreateProductAsync(new CreateProductRequest("Producto", "Desc", -1, "EUR"));
		Assert.False(result.Succeeded);
		Assert.NotEmpty(result.Errors);
		Assert.Empty(db.Products);
	}

	[Fact]
	public async Task CreateProductAsync_WithUnknownCategory_Fails()
	{
		await using var db = CreateDbContext();
		var result = await new CatalogService(db).CreateProductAsync(NewProductRequest(categoryId: Guid.NewGuid()));
		Assert.False(result.Succeeded);
		Assert.Empty(db.Products);
	}

	[Fact]
	public async Task ProductDetails_RoundTripCreateUpdateAndClear()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var category = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Material"));
		var created = await service.CreateProductAsync(new CreateProductRequest("Producto", "Desc", 100, "EUR",
			category.Value!.Id, "SKU-1", true, " caja ", 21m, " Proveedor ", " Marca ", 5));
		Assert.True(created.Succeeded);
		db.ChangeTracker.Clear();
		var product = await service.GetProductAsync(created.Value!.Id);
		Assert.Equal("caja", product!.UnitOfMeasure);
		Assert.Equal(21m, product.TaxRate);
		Assert.Equal("Proveedor", product.Supplier);
		Assert.Equal("Marca", product.Brand);
		Assert.Equal(5, product.DeliveryDays);
		Assert.Equal(category.Value.Id, product.CategoryId);
		Assert.Equal("SKU-1", product.Sku);

		var updated = await service.UpdateProductAsync(product.Id, new UpdateProductRequest("Nuevo", "Nueva descripción", 200,
			"USD", null, "SKU-2", false, "unidad", 0m, "Otro", "Otra", 0));
		Assert.True(updated.Succeeded);
		db.ChangeTracker.Clear();
		product = await service.GetProductAsync(product.Id);
		Assert.Equal("Nuevo", product!.Name);
		Assert.Equal("Nueva descripción", product.Description);
		Assert.Equal(200m, product.PriceAmount);
		Assert.Equal("USD", product.PriceCurrencyCode);
		Assert.Null(product.CategoryId);
		Assert.Equal("SKU-2", product.Sku);
		Assert.False(product.IsActive);
		Assert.Equal("unidad", product.UnitOfMeasure);
		Assert.Equal(0m, product.TaxRate);
		Assert.Equal("Otro", product.Supplier);
		Assert.Equal("Otra", product.Brand);
		Assert.Equal(0, product.DeliveryDays);

		var cleared = await service.UpdateProductAsync(product.Id, new UpdateProductRequest("Nuevo", "Desc", 200, "USD", null, null, true));
		Assert.True(cleared.Succeeded);
		db.ChangeTracker.Clear();
		product = await service.GetProductAsync(product.Id);
		Assert.Null(product!.UnitOfMeasure);
		Assert.Null(product.TaxRate);
		Assert.Null(product.Supplier);
		Assert.Null(product.Brand);
		Assert.Null(product.DeliveryDays);
	}

	[Fact]
	public async Task ServiceDetails_RoundTripCreateUpdateAndClear()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var created = await service.CreateServiceAsync(new CreateServiceRequest("Servicio", "Desc", 50, "EUR", null,
			60, true, true, "hora", 10m, "Prestador", "REF-1", "Remota", "Mensual"));
		Assert.True(created.Succeeded);
		db.ChangeTracker.Clear();
		var item = await service.GetServiceAsync(created.Value!.Id);
		Assert.Equal("hora", item!.UnitOfMeasure);
		Assert.Equal(10m, item.TaxRate);
		Assert.Equal("Prestador", item.Supplier);
		Assert.Equal("REF-1", item.Reference);
		Assert.Equal("Remota", item.DeliveryMode);
		Assert.Equal("Mensual", item.BillingPeriod);
		Assert.Equal(60, item.DurationMinutes);
		Assert.True(item.IsRecurring);

		var updated = await service.UpdateServiceAsync(item.Id, new UpdateServiceRequest("Nuevo", "Otra descripción", 75, "USD",
			null, 120, false, false, "proyecto", 21m, "Otro", "REF-2", "Presencial", "Por entrega"));
		Assert.True(updated.Succeeded);
		db.ChangeTracker.Clear();
		item = await service.GetServiceAsync(item.Id);
		Assert.Equal("Nuevo", item!.Name);
		Assert.Equal(75m, item.PriceAmount);
		Assert.Equal("USD", item.PriceCurrencyCode);
		Assert.Equal("proyecto", item.UnitOfMeasure);
		Assert.Equal(21m, item.TaxRate);
		Assert.Equal("Otro", item.Supplier);
		Assert.Equal("REF-2", item.Reference);
		Assert.Equal("Presencial", item.DeliveryMode);
		Assert.Equal("Por entrega", item.BillingPeriod);
		Assert.Equal(120, item.DurationMinutes);
		Assert.False(item.IsRecurring);
		Assert.False(item.IsActive);

		var cleared = await service.UpdateServiceAsync(item.Id, new UpdateServiceRequest("Nuevo", "Desc", 75, "USD", null, null, false, true));
		Assert.True(cleared.Succeeded);
		db.ChangeTracker.Clear();
		item = await service.GetServiceAsync(item.Id);
		Assert.Null(item!.UnitOfMeasure);
		Assert.Null(item.TaxRate);
		Assert.Null(item.Supplier);
		Assert.Null(item.Reference);
		Assert.Null(item.DeliveryMode);
		Assert.Null(item.BillingPeriod);
	}

	[Fact]
	public async Task InvalidUpdates_DoNotLeakPartialChangesIntoNextSave()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var product = await service.CreateProductAsync(NewProductRequest());
		var item = await service.CreateServiceAsync(NewServiceRequest());
		var failedProduct = await service.UpdateProductAsync(product.Value!.Id,
			new UpdateProductRequest("Cambió", "Desc", 200, "USD", null, null, true, DeliveryDays: -1));
		var failedService = await service.UpdateServiceAsync(item.Value!.Id,
			new UpdateServiceRequest("Cambió", "Desc", 200, "USD", null, 60, true, true, BillingPeriod: new string('x', 101)));
		Assert.False(failedProduct.Succeeded);
		Assert.False(failedService.Succeeded);
		await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Otra operación"));
		db.ChangeTracker.Clear();
		Assert.Equal("Licencia anual", (await service.GetProductAsync(product.Value.Id))!.Name);
		Assert.Equal(100m, (await service.GetProductAsync(product.Value.Id))!.PriceAmount);
		Assert.Equal("Consultoría", (await service.GetServiceAsync(item.Value.Id))!.Name);
		Assert.Equal(50m, (await service.GetServiceAsync(item.Value.Id))!.PriceAmount);
	}

	[Fact]
	public async Task ActiveState_AndDeletion_WorkForBothTypes()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var product = await service.CreateProductAsync(NewProductRequest());
		var item = await service.CreateServiceAsync(NewServiceRequest());
		Assert.True((await service.SetProductActiveAsync(product.Value!.Id, false)).Succeeded);
		Assert.True((await service.SetServiceActiveAsync(item.Value!.Id, false)).Succeeded);
		Assert.False((await service.GetProductAsync(product.Value.Id))!.IsActive);
		Assert.False((await service.GetServiceAsync(item.Value.Id))!.IsActive);
		Assert.True((await service.DeleteProductAsync(product.Value.Id)).Succeeded);
		Assert.True((await service.DeleteServiceAsync(item.Value.Id)).Succeeded);
		Assert.Null(await service.GetProductAsync(product.Value.Id));
		Assert.Null(await service.GetServiceAsync(item.Value.Id));
		Assert.False((await service.DeleteProductAsync(product.Value.Id)).Succeeded);
		Assert.False((await service.DeleteServiceAsync(item.Value.Id)).Succeeded);
	}

	[Fact]
	public async Task DeleteItems_InOrders_FailsAndPreservesHistory()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var product = await service.CreateProductAsync(NewProductRequest());
		var item = await service.CreateServiceAsync(NewServiceRequest());
		var clientId = Guid.NewGuid();
		var orders = new OrderService(db);
		var order = await orders.CreateOrderAsync(clientId, Guid.NewGuid(), new CreateOrderRequest());
		Assert.True((await orders.AddLineAsync(clientId, order.Value!.Id, new AddOrderLineRequest(product.Value!.Id, 1))).Succeeded);
		Assert.True((await orders.AddLineAsync(clientId, order.Value.Id, new AddOrderLineRequest(item.Value!.Id, 1))).Succeeded);
		Assert.False((await service.DeleteProductAsync(product.Value.Id)).Succeeded);
		Assert.False((await service.DeleteServiceAsync(item.Value.Id)).Succeeded);
		Assert.Equal(2, (await orders.GetOrderAsync(clientId, order.Value.Id))!.Lines.Count);
		Assert.NotNull(await service.GetProductAsync(product.Value.Id));
		Assert.NotNull(await service.GetServiceAsync(item.Value.Id));
	}

	[Fact]
	public async Task CreateCategoryAsync_WithValidParent_Succeeds()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var parent = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Raíz"));
		var child = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Hija", parent.Value!.Id));
		Assert.True(child.Succeeded);
		Assert.Equal(parent.Value.Id, child.Value!.ParentCategoryId);
	}

	[Fact]
	public async Task CreateCategoryAsync_WithUnknownParent_Fails()
	{
		await using var db = CreateDbContext();
		var result = await new CatalogService(db).CreateCategoryAsync(new CreateCatalogCategoryRequest("Hija", Guid.NewGuid()));
		Assert.False(result.Succeeded);
		Assert.Empty(db.CatalogCategories);
	}

	[Fact]
	public async Task UpdateCategoryAsync_MovingIntoOwnDescendant_Fails()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var root = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Raíz"));
		var child = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Hija", root.Value!.Id));
		var result = await service.UpdateCategoryAsync(root.Value.Id, new UpdateCatalogCategoryRequest("Raíz", child.Value!.Id, true));
		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Contains("subárbol", StringComparison.OrdinalIgnoreCase));
		Assert.Null((await service.GetCategoryAsync(root.Value.Id))!.ParentCategoryId);
	}

	[Fact]
	public async Task DeleteCategoryAsync_WithChildren_Fails()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var root = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Raíz"));
		await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Hija", root.Value!.Id));
		Assert.False((await service.DeleteCategoryAsync(root.Value.Id)).Succeeded);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DeleteCategoryAsync_WithAssociatedItem_Fails(bool isService)
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var category = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Soluciones"));
		if (isService)
			await service.CreateServiceAsync(NewServiceRequest(categoryId: category.Value!.Id));
		else
			await service.CreateProductAsync(NewProductRequest(categoryId: category.Value!.Id));
		Assert.False((await service.DeleteCategoryAsync(category.Value.Id)).Succeeded);
	}

	[Fact]
	public async Task DeleteCategoryAsync_WithoutDependencies_Succeeds()
	{
		await using var db = CreateDbContext();
		var service = new CatalogService(db);
		var category = await service.CreateCategoryAsync(new CreateCatalogCategoryRequest("Soluciones"));
		Assert.True((await service.DeleteCategoryAsync(category.Value!.Id)).Succeeded);
		Assert.Null(await service.GetCategoryAsync(category.Value.Id));
	}

	[Fact]
	public void Model_HasNoCatalogClientRelationship_AndKeepsRestrictDependencies()
	{
		using var db = CreateDbContext();
		var item = db.Model.FindEntityType(typeof(CatalogItem))!;
		var category = db.Model.FindEntityType(typeof(CatalogCategory))!;
		Assert.Null(item.FindProperty("ClientId"));
		Assert.Null(category.FindProperty("ClientId"));
		Assert.Equal(DeleteBehavior.Restrict, Assert.Single(category.GetForeignKeys()).DeleteBehavior);
		Assert.Equal(DeleteBehavior.Restrict, Assert.Single(item.GetForeignKeys()).DeleteBehavior);
		Assert.Equal(50, item.FindProperty(nameof(CatalogItem.UnitOfMeasure))!.GetMaxLength());
		Assert.Equal(5, item.FindProperty(nameof(CatalogItem.TaxRate))!.GetPrecision());
		Assert.Equal(2, item.FindProperty(nameof(CatalogItem.TaxRate))!.GetScale());
	}
}
