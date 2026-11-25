namespace MemByte.Domain.Catalog;

/// <summary>
/// Artículo de catálogo de tipo servicio (una prestación, con o sin
/// duración/recurrencia). Incluye referencia, modalidad de prestación y
/// periodicidad opcionales, sin imponer un sector o modelo de negocio.
/// </summary>
public class Service : CatalogItem
{
	public const int ReferenceMaxLength = 64;
	public const int DeliveryModeMaxLength = 100;
	public const int BillingPeriodMaxLength = 100;

	/// <summary>Referencia interna del servicio.</summary>
	public string? Reference { get; private set; }

	/// <summary>Modalidad de prestación, por ejemplo presencial o remota.</summary>
	public string? DeliveryMode { get; private set; }

	/// <summary>Periodicidad de facturación informativa, por ejemplo mensual.</summary>
	public string? BillingPeriod { get; private set; }

	public int? DurationMinutes { get; private set; }

	public bool IsRecurring { get; private set; }

	private Service()
	{
		// Requerido por EF Core.
	}

	public Service(
		string name,
		string description,
		Price price,
		Guid? categoryId = null,
		int? durationMinutes = null,
		bool isRecurring = false,
		bool isActive = true,
		string? unitOfMeasure = null,
		decimal? taxRate = null,
		string? supplier = null,
		string? reference = null,
		string? deliveryMode = null,
		string? billingPeriod = null)
		: base(name, description, price, categoryId, isActive, unitOfMeasure, taxRate, supplier)
	{
		SetDurationMinutes(durationMinutes);
		IsRecurring = isRecurring;
		UpdateProvisionDetails(reference, deliveryMode, billingPeriod);
	}

	/// <summary>Actualiza la referencia y las condiciones de prestación tras validarlas.</summary>
	public void UpdateProvisionDetails(string? reference, string? deliveryMode, string? billingPeriod)
	{
		var normalizedReference = NormalizeOptional(reference, ReferenceMaxLength, "La referencia");
		var mode = NormalizeOptional(deliveryMode, DeliveryModeMaxLength, "La modalidad");
		var period = NormalizeOptional(billingPeriod, BillingPeriodMaxLength, "La periodicidad");
		Reference = normalizedReference;
		DeliveryMode = mode;
		BillingPeriod = period;
		Touch();
	}

	public void UpdateDuration(int? durationMinutes) => SetDurationMinutes(durationMinutes);

	public void SetRecurring(bool isRecurring) => IsRecurring = isRecurring;

	private void SetDurationMinutes(int? durationMinutes)
	{
		if (durationMinutes is < 0)
			throw new ArgumentException("La duración no puede ser negativa.", nameof(durationMinutes));

		DurationMinutes = durationMinutes;
	}
}
