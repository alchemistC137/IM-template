namespace MemByte.Application.Catalog;

/// <summary>
/// Modelo de lectura de un producto del catálogo.
/// </summary>
public sealed record ProductDto(
	Guid Id,
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId,
	string? Sku,
	bool IsActive,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	string? UnitOfMeasure,
	decimal? TaxRate,
	string? Supplier,
	string? Brand,
	int? DeliveryDays);
