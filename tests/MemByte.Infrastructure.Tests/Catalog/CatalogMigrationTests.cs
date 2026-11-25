using MemByte.Infrastructure.Persistence;
using MemByte.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MemByte.Infrastructure.Tests.Catalog;

public class CatalogMigrationTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Migration_GuardsEmptyCatalogBeforeChangingSchema(bool rollback)
	{
		var migration = new SingleInstallationCatalog();
		var operations = rollback ? migration.DownOperations : migration.UpOperations;
		var guard = Assert.IsType<SqlOperation>(operations[0]);
		Assert.False(guard.SuppressTransaction);
		Assert.Contains("LOCK TABLE \"CatalogItems\", \"CatalogCategories\" IN ACCESS EXCLUSIVE MODE", guard.Sql);
		Assert.Contains("EXISTS (SELECT 1 FROM \"CatalogItems\") OR EXISTS (SELECT 1 FROM \"CatalogCategories\")", guard.Sql);
		Assert.Contains("RAISE EXCEPTION", guard.Sql);
		Assert.DoesNotContain(operations, operation => operation is DeleteDataOperation or DropTableOperation);
		var clientColumns = operations.OfType<DropColumnOperation>().Where(c => c.Name == "ClientId").ToList();
		if (!rollback)
			Assert.Equal(new[] { "CatalogCategories", "CatalogItems" }, clientColumns.Select(c => c.Table).OrderBy(t => t));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Migration_GeneratesTransactionalPostgresScript(bool rollback)
	{
		var options = new DbContextOptionsBuilder<MemByteDbContext>()
			.UseNpgsql("Host=localhost;Database=intranet_design;Username=intranet_design").Options;
		using var db = new MemByteDbContext(options);
		const string previous = "20260903181621_AddOrders";
		const string current = "20261125201008_SingleInstallationCatalog";
		var sql = db.GetService<IMigrator>().GenerateScript(rollback ? current : previous, rollback ? previous : current);
		Assert.Contains("START TRANSACTION;", sql);
		Assert.Contains("COMMIT;", sql);
		var guardPosition = sql.IndexOf("LOCK TABLE", StringComparison.Ordinal);
		var schemaPosition = sql.IndexOf("ALTER TABLE", StringComparison.Ordinal);
		Assert.True(guardPosition >= 0 && schemaPosition > guardPosition);
		Assert.Contains("RAISE EXCEPTION", sql);
		Assert.DoesNotContain("DELETE FROM \"CatalogItems\"", sql);
		Assert.DoesNotContain("DELETE FROM \"CatalogCategories\"", sql);
		Assert.DoesNotContain("ALTER TABLE \"Orders\"", sql);
		Assert.DoesNotContain("ALTER TABLE \"Conversations\"", sql);
	}

	[Fact]
	public void Migration_AddsAllOptionalFieldsWithExpectedStorageTypes()
	{
		var columns = new SingleInstallationCatalog().UpOperations.OfType<AddColumnOperation>()
			.ToDictionary(c => c.Name);
		Assert.Equal(new[] { "BillingPeriod", "Brand", "DeliveryDays", "DeliveryMode", "Reference", "Supplier", "TaxRate", "UnitOfMeasure" },
			columns.Keys.OrderBy(name => name));
		Assert.All(columns.Values, column =>
		{
			Assert.Equal("CatalogItems", column.Table);
			Assert.True(column.IsNullable);
		});
		Assert.Equal("numeric(5,2)", columns["TaxRate"].ColumnType);
		Assert.Equal(50, columns["UnitOfMeasure"].MaxLength);
		Assert.Equal(200, columns["Supplier"].MaxLength);
		Assert.Equal(100, columns["Brand"].MaxLength);
		Assert.Equal(64, columns["Reference"].MaxLength);
		Assert.Equal(100, columns["DeliveryMode"].MaxLength);
		Assert.Equal(100, columns["BillingPeriod"].MaxLength);
		Assert.Equal(typeof(int), columns["DeliveryDays"].ClrType);
	}
}
