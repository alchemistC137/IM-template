using MemByte.Domain.Catalog;

namespace MemByte.Domain.Tests.Catalog;

public class ProductTests
{
	private static Price DefaultPrice => new(100, "EUR");

	[Fact]
	public void Constructor_WithEmptyName_Throws()
	{
		Assert.Throws<ArgumentException>(() => new Product(" ", "desc", DefaultPrice));
	}

	[Fact]
	public void Constructor_WithoutSku_LeavesSkuNull()
	{
		var product = new Product("Licencia anual", "desc", DefaultPrice);
		Assert.Null(product.Sku);
	}

	[Fact]
	public void UpdateSku_WithBlankValue_ClearsSku()
	{
		var product = new Product("Licencia anual", "desc", DefaultPrice, sku: "SKU-1");
		product.UpdateSku("   ");
		Assert.Null(product.Sku);
	}

	[Fact]
	public void UpdatePrice_ChangesPriceAndTouchesUpdatedAt()
	{
		var product = new Product("Licencia anual", "desc", DefaultPrice);
		var createdUpdatedAt = product.UpdatedAt;
		var newPrice = new Price(200, "USD");
		product.UpdatePrice(newPrice);
		Assert.Equal(newPrice, product.Price);
		Assert.True(product.UpdatedAt >= createdUpdatedAt);
	}

	[Fact]
	public void Deactivate_ThenActivate_TogglesIsActive()
	{
		var product = new Product("Licencia anual", "desc", DefaultPrice);
		product.Deactivate();
		Assert.False(product.IsActive);
		product.Activate();
		Assert.True(product.IsActive);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(30)]
	public void Constructor_AssignsCommercialAndSupplyDetails(int deliveryDays)
	{
		var product = new Product("Producto", "desc", DefaultPrice, unitOfMeasure: " caja ",
			taxRate: 21.25m, supplier: " Proveedor ", brand: " Marca ", deliveryDays: deliveryDays);
		Assert.Equal("caja", product.UnitOfMeasure);
		Assert.Equal(21.25m, product.TaxRate);
		Assert.Equal("Proveedor", product.Supplier);
		Assert.Equal("Marca", product.Brand);
		Assert.Equal(deliveryDays, product.DeliveryDays);
	}

	[Fact]
	public void UpdateSupplyDetails_WithInvalidDelivery_DoesNotChangeBrand()
	{
		var product = new Product("Producto", "desc", DefaultPrice, brand: "Original", deliveryDays: 3);
		Assert.Throws<ArgumentException>(() => product.UpdateSupplyDetails("Nueva", -1));
		Assert.Equal("Original", product.Brand);
		Assert.Equal(3, product.DeliveryDays);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	public void UpdateCommercialDetails_WithInvalidTax_DoesNotChangeValues(int tax)
	{
		var product = new Product("Producto", "desc", DefaultPrice, unitOfMeasure: "unidad", taxRate: 21m, supplier: "Original");
		Assert.Throws<ArgumentException>(() => product.UpdateCommercialDetails("caja", tax, "Nuevo"));
		Assert.Equal("unidad", product.UnitOfMeasure);
		Assert.Equal(21m, product.TaxRate);
		Assert.Equal("Original", product.Supplier);
	}

	[Fact]
	public void UpdateCommercialDetails_WithExcessPrecision_Throws()
	{
		var product = new Product("Producto", "desc", DefaultPrice);
		Assert.Throws<ArgumentException>(() => product.UpdateCommercialDetails(null, 21.123m, null));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	public void UpdateCommercialDetails_AcceptsTaxBoundaries(int tax)
	{
		var product = new Product("Producto", "desc", DefaultPrice);
		product.UpdateCommercialDetails(null, tax, null);
		Assert.Equal((decimal)tax, product.TaxRate);
	}

	[Fact]
	public void OptionalDetails_CanBeCleared()
	{
		var product = new Product("Producto", "desc", DefaultPrice, unitOfMeasure: "caja", taxRate: 0m,
			supplier: "Proveedor", brand: "Marca", deliveryDays: 3);
		product.UpdateCommercialDetails(" ", null, " ");
		product.UpdateSupplyDetails(" ", null);
		Assert.Null(product.UnitOfMeasure);
		Assert.Null(product.TaxRate);
		Assert.Null(product.Supplier);
		Assert.Null(product.Brand);
		Assert.Null(product.DeliveryDays);
	}

	[Fact]
	public void Constructor_EnforcesTextLimits()
	{
		Assert.Throws<ArgumentException>(() => new Product("Producto", "desc", DefaultPrice, unitOfMeasure: new string('x', 51)));
		Assert.Throws<ArgumentException>(() => new Product("Producto", "desc", DefaultPrice, supplier: new string('x', 201)));
		Assert.Throws<ArgumentException>(() => new Product("Producto", "desc", DefaultPrice, brand: new string('x', 101)));
		var product = new Product("Producto", "desc", DefaultPrice, unitOfMeasure: new string('x', 50),
			supplier: new string('x', 200), brand: new string('x', 100));
		Assert.Equal(50, product.UnitOfMeasure!.Length);
		Assert.Equal(200, product.Supplier!.Length);
		Assert.Equal(100, product.Brand!.Length);
	}
}
