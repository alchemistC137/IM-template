using MemByte.Domain.Catalog;

namespace MemByte.Domain.Tests.Catalog;

public class CatalogCategoryTests
{
	[Fact]
	public void Constructor_WithEmptyName_Throws()
	{
		Assert.Throws<ArgumentException>(() => new CatalogCategory("   "));
	}

	[Fact]
	public void Constructor_TrimsAndAssignsName()
	{
		var category = new CatalogCategory("  Soluciones digitales  ");
		Assert.Equal("Soluciones digitales", category.Name);
		Assert.True(category.IsActive);
	}

	[Fact]
	public void MoveTo_WithOwnId_Throws()
	{
		var category = new CatalogCategory("Raíz");
		Assert.Throws<InvalidOperationException>(() => category.MoveTo(category.Id));
	}

	[Fact]
	public void MoveTo_WithDifferentParent_UpdatesParent()
	{
		var category = new CatalogCategory("Subcategoría");
		var parentId = Guid.NewGuid();
		category.MoveTo(parentId);
		Assert.Equal(parentId, category.ParentCategoryId);
	}

	[Fact]
	public void Deactivate_ThenActivate_TogglesIsActive()
	{
		var category = new CatalogCategory("Soluciones");
		category.Deactivate();
		Assert.False(category.IsActive);
		category.Activate();
		Assert.True(category.IsActive);
	}
}
