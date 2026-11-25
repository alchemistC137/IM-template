namespace MemByte.Application.Catalog;

public sealed record CreateProductRequest(
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId = null,
	string? Sku = null,
	bool IsActive = true,
	string? UnitOfMeasure = null,
	decimal? TaxRate = null,
	string? Supplier = null,
	string? Brand = null,
	int? DeliveryDays = null);

public sealed record UpdateProductRequest(
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId,
	string? Sku,
	bool IsActive,
	string? UnitOfMeasure = null,
	decimal? TaxRate = null,
	string? Supplier = null,
	string? Brand = null,
	int? DeliveryDays = null);
