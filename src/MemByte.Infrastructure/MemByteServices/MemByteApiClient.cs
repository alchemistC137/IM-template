using MemByte.Application.MemByteServices;

namespace MemByte.Infrastructure.MemByteServices;

/// <summary>
/// Cliente HTTP preparado para integrar el API de MemByte cuando se defina su contrato.
/// </summary>
public sealed class MemByteApiClient : IMemByteApiClient
{
	/// <summary>Inicializa el cliente sin configurar ni enviar peticiones externas.</summary>
	public MemByteApiClient(HttpClient httpClient)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
	}

	/// <inheritdoc />
	public bool IsAvailable => false;

	/// <inheritdoc />
	public void EnsureAvailable()
	{
		throw new InvalidOperationException("La integración con el API de MemByte todavía no está disponible.");
	}
}
