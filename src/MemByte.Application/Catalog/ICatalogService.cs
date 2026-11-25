namespace MemByte.Application.Catalog;

/// <summary>
/// Gestión del catálogo único de productos y servicios de la instalación.
/// No depende de los clientes comerciales ni acepta un identificador de cliente.
/// </summary>
public interface ICatalogService
{
	// Categorías.
	Task<IReadOnlyList<CatalogCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

	Task<CatalogCategoryDto?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<CatalogCategoryDto>> CreateCategoryAsync(CreateCatalogCategoryRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<CatalogCategoryDto>> UpdateCategoryAsync(Guid categoryId, UpdateCatalogCategoryRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

	// Productos.
	Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default);

	Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ProductDto>> UpdateProductAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> SetProductActiveAsync(Guid productId, bool isActive, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default);

	// Servicios.
	Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken cancellationToken = default);

	Task<ServiceDto?> GetServiceAsync(Guid serviceId, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ServiceDto>> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<ServiceDto>> UpdateServiceAsync(Guid serviceId, UpdateServiceRequest request, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> SetServiceActiveAsync(Guid serviceId, bool isActive, CancellationToken cancellationToken = default);

	Task<CatalogOperationResult<bool>> DeleteServiceAsync(Guid serviceId, CancellationToken cancellationToken = default);
}
