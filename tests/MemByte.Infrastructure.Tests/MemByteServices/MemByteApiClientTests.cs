using MemByte.Infrastructure.MemByteServices;

namespace MemByte.Infrastructure.Tests.MemByteServices;

public class MemByteApiClientTests
{
	[Fact]
	public void Client_IsUnavailable_AndExplainsWhy()
	{
		using var httpClient = new HttpClient();
		var client = new MemByteApiClient(httpClient);

		Assert.False(client.IsAvailable);
		var exception = Assert.Throws<InvalidOperationException>(client.EnsureAvailable);
		Assert.Equal("La integración con el API de MemByte todavía no está disponible.", exception.Message);
	}

	[Fact]
	public void Constructor_RejectsMissingHttpClient()
	{
		Assert.Throws<ArgumentNullException>(() => new MemByteApiClient(null!));
	}
}
