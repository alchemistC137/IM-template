namespace MemByte.Domain.Catalog;

/// <summary>
/// Artículo de catálogo de tipo producto (bien tangible o digital que se
/// entrega tal cual, sin prestación continuada). Incluye SKU, marca y
/// plazo de entrega opcionales para adaptarse a distintos sectores.
/// </summary>
public class Product : CatalogItem
{
	public const int SkuMaxLength = 64;
	public const int BrandMaxLength = 100;

	public string? Sku { get; private set; }

	/// <summary>Marca o fabricante del producto, si procede.</summary>
	public string? Brand { get; private set; }

	/// <summary>Plazo estimado de entrega en días; nulo si no se conoce.</summary>
	public int? DeliveryDays { get; private set; }

	private Product()
	{
		// Requerido por EF Core.
	}

	public Product(
		string name,
		string description,
		Price price,
		Guid? categoryId = null,
		string? sku = null,
		bool isActive = true,
		string? unitOfMeasure = null,
		decimal? taxRate = null,
		string? supplier = null,
		string? brand = null,
		int? deliveryDays = null)
		: base(name, description, price, categoryId, isActive, unitOfMeasure, taxRate, supplier)
	{
		SetSku(sku);
		UpdateSupplyDetails(brand, deliveryDays);
	}

	/// <summary>Actualiza la marca y el plazo de entrega después de validarlos.</summary>
	public void UpdateSupplyDetails(string? brand, int? deliveryDays)
	{
		var normalizedBrand = NormalizeOptional(brand, BrandMaxLength, "La marca");
		if (deliveryDays is < 0)
			throw new ArgumentException("El plazo de entrega no puede ser negativo.", nameof(deliveryDays));
		Brand = normalizedBrand;
		DeliveryDays = deliveryDays;
		Touch();
	}

	public void UpdateSku(string? sku) => SetSku(sku);

	private void SetSku(string? sku)
	{
		if (string.IsNullOrWhiteSpace(sku))
		{
			Sku = null;
			return;
		}

		sku = sku.Trim();
		Sku = sku.Length > SkuMaxLength ? sku[..SkuMaxLength] : sku;
	}
}
