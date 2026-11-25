namespace MemByte.Application.MemByteServices;

/// <summary>
/// Punto de integración con el futuro API de productos, contratación y soporte de MemByte.
/// </summary>
public interface IMemByteApiClient
{
	/// <summary>Indica si la integración está disponible para su uso.</summary>
	bool IsAvailable { get; }

	/// <summary>Comprueba que el API esté disponible antes de iniciar una operación.</summary>
	/// <exception cref="InvalidOperationException">La integración todavía no está disponible.</exception>
	void EnsureAvailable();
}
