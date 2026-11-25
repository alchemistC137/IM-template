using MemByte.Domain.Catalog;

namespace MemByte.Domain.Tests.Catalog;

public class ServiceTests
{
	private static Price DefaultPrice => new(50, "EUR");

	[Fact]
	public void Constructor_WithNegativeDuration_Throws()
	{
		Assert.Throws<ArgumentException>(() => new Service("Consultoría", "desc", DefaultPrice, durationMinutes: -1));
	}

	[Fact]
	public void Constructor_WithNullDuration_IsAllowed()
	{
		var service = new Service("Consultoría", "desc", DefaultPrice);
		Assert.Null(service.DurationMinutes);
	}

	[Fact]
	public void UpdateDuration_WithNegativeValue_Throws()
	{
		var service = new Service("Consultoría", "desc", DefaultPrice, durationMinutes: 60);
		Assert.Throws<ArgumentException>(() => service.UpdateDuration(-5));
		Assert.Equal(60, service.DurationMinutes);
	}

	[Fact]
	public void SetRecurring_UpdatesFlag()
	{
		var service = new Service("Soporte mensual", "desc", DefaultPrice);
		service.SetRecurring(true);
		Assert.True(service.IsRecurring);
	}

	[Fact]
	public void Constructor_NormalizesAndAssignsProvisionDetails()
	{
		var service = new Service("Servicio", "desc", DefaultPrice, unitOfMeasure: " hora ", taxRate: 0m,
			supplier: " Prestador ", reference: " REF-1 ", deliveryMode: " Remota ", billingPeriod: " Mensual ");
		Assert.Equal("hora", service.UnitOfMeasure);
		Assert.Equal(0m, service.TaxRate);
		Assert.Equal("Prestador", service.Supplier);
		Assert.Equal("REF-1", service.Reference);
		Assert.Equal("Remota", service.DeliveryMode);
		Assert.Equal("Mensual", service.BillingPeriod);
	}

	[Fact]
	public void UpdateProvisionDetails_ValidatesBeforeChangingValues()
	{
		var service = new Service("Servicio", "desc", DefaultPrice, reference: "Original", deliveryMode: "Remota");
		Assert.Throws<ArgumentException>(() => service.UpdateProvisionDetails("Nueva", "Presencial", new string('x', 101)));
		Assert.Equal("Original", service.Reference);
		Assert.Equal("Remota", service.DeliveryMode);
		Assert.Null(service.BillingPeriod);
	}

	[Fact]
	public void Constructor_EnforcesProvisionTextLimits()
	{
		Assert.Throws<ArgumentException>(() => new Service("Servicio", "desc", DefaultPrice, reference: new string('x', 65)));
		Assert.Throws<ArgumentException>(() => new Service("Servicio", "desc", DefaultPrice, deliveryMode: new string('x', 101)));
		Assert.Throws<ArgumentException>(() => new Service("Servicio", "desc", DefaultPrice, billingPeriod: new string('x', 101)));
		var service = new Service("Servicio", "desc", DefaultPrice, reference: new string('x', 64),
			deliveryMode: new string('x', 100), billingPeriod: new string('x', 100));
		Assert.Equal(64, service.Reference!.Length);
		Assert.Equal(100, service.DeliveryMode!.Length);
		Assert.Equal(100, service.BillingPeriod!.Length);
	}

	[Fact]
	public void UpdateProvisionDetails_BlankValuesClearFields()
	{
		var service = new Service("Servicio", "desc", DefaultPrice, reference: "REF", deliveryMode: "Remota", billingPeriod: "Mensual");
		service.UpdateProvisionDetails(" ", null, " ");
		Assert.Null(service.Reference);
		Assert.Null(service.DeliveryMode);
		Assert.Null(service.BillingPeriod);
	}
}
