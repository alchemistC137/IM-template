namespace MemByte.Domain.Catalog;

/// <summary>
/// Base de los productos y servicios del catálogo único de la instalación.
/// Comparte nombre, descripción, precio, categoría y datos comerciales
/// independientes del sector de la empresa.
/// </summary>
public abstract class CatalogItem
{
	public const int NameMaxLength = 200;
	public const int DescriptionMaxLength = 2000;
	public const int UnitOfMeasureMaxLength = 50;
	public const int SupplierMaxLength = 200;

	public Guid Id { get; private set; }

	/// <summary>Unidad de venta o facturación, por ejemplo unidad, hora o proyecto.</summary>
	public string? UnitOfMeasure { get; private set; }

	/// <summary>Porcentaje de impuesto informativo; nulo si no se ha definido.</summary>
	public decimal? TaxRate { get; private set; }

	/// <summary>Proveedor o prestador del artículo, si procede.</summary>
	public string? Supplier { get; private set; }

	public string Name { get; private set; } = string.Empty;

	public string Description { get; private set; } = string.Empty;

	public Price Price { get; private set; } = null!;

	public Guid? CategoryId { get; private set; }

	public bool IsActive { get; private set; } = true;

	public DateTimeOffset CreatedAt { get; private set; }

	public DateTimeOffset UpdatedAt { get; private set; }

	protected CatalogItem()
	{
		// Requerido por EF Core.
	}

	protected CatalogItem(
		string name,
		string description,
		Price price,
		Guid? categoryId,
		bool isActive,
		string? unitOfMeasure,
		decimal? taxRate,
		string? supplier)
	{
		Id = Guid.NewGuid();
		CreatedAt = DateTimeOffset.UtcNow;
		UpdatedAt = CreatedAt;
		SetName(name);
		SetDescription(description);
		Price = price ?? throw new ArgumentNullException(nameof(price));
		CategoryId = categoryId;
		IsActive = isActive;
		UpdateCommercialDetails(unitOfMeasure, taxRate, supplier);
	}

	/// <summary>Actualiza los datos comerciales tras validar todos los valores.</summary>
	public void UpdateCommercialDetails(string? unitOfMeasure, decimal? taxRate, string? supplier)
	{
		var unit = NormalizeOptional(unitOfMeasure, UnitOfMeasureMaxLength, "La unidad de medida");
		var provider = NormalizeOptional(supplier, SupplierMaxLength, "El proveedor");
		if (taxRate is < 0 or > 100)
			throw new ArgumentException("El impuesto debe estar entre 0 y 100 %.", nameof(taxRate));
		if (taxRate.HasValue && decimal.Round(taxRate.Value, 2) != taxRate.Value)
			throw new ArgumentException("El impuesto admite como máximo dos decimales.", nameof(taxRate));

		UnitOfMeasure = unit;
		TaxRate = taxRate;
		Supplier = provider;
		Touch();
	}

	/// <summary>Normaliza un texto opcional sin truncar valores inválidos.</summary>
	protected static string? NormalizeOptional(string? value, int maxLength, string label)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;
		value = value.Trim();
		if (value.Length > maxLength)
			throw new ArgumentException($"{label} no puede superar {maxLength} caracteres.", nameof(value));
		return value;
	}

	public void Rename(string name)
	{
		SetName(name);
		Touch();
	}

	public void UpdateDescription(string description)
	{
		SetDescription(description);
		Touch();
	}

	public void UpdatePrice(Price price)
	{
		Price = price ?? throw new ArgumentNullException(nameof(price));
		Touch();
	}

	public void ChangeCategory(Guid? categoryId)
	{
		CategoryId = categoryId;
		Touch();
	}

	public void Activate()
	{
		IsActive = true;
		Touch();
	}

	public void Deactivate()
	{
		IsActive = false;
		Touch();
	}

	private void SetName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("El nombre no puede estar vacío.", nameof(name));

		name = name.Trim();
		Name = name.Length > NameMaxLength ? name[..NameMaxLength] : name;
	}

	private void SetDescription(string description)
	{
		description = (description ?? string.Empty).Trim();
		Description = description.Length > DescriptionMaxLength ? description[..DescriptionMaxLength] : description;
	}

	/// <summary>Registra la modificación del artículo.</summary>
	protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
