namespace MemByte.Application.Catalog;

public sealed record CreateServiceRequest(
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId = null,
	int? DurationMinutes = null,
	bool IsRecurring = false,
	bool IsActive = true,
	string? UnitOfMeasure = null,
	decimal? TaxRate = null,
	string? Supplier = null,
	string? Reference = null,
	string? DeliveryMode = null,
	string? BillingPeriod = null);

public sealed record UpdateServiceRequest(
	string Name,
	string Description,
	decimal PriceAmount,
	string PriceCurrencyCode,
	Guid? CategoryId,
	int? DurationMinutes,
	bool IsRecurring,
	bool IsActive,
	string? UnitOfMeasure = null,
	decimal? TaxRate = null,
	string? Supplier = null,
	string? Reference = null,
	string? DeliveryMode = null,
	string? BillingPeriod = null);
