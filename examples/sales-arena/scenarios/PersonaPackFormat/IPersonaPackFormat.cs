namespace SalesArena.PersonaPackFormat;

/// <summary>
/// Round-trippable persona pack codec. Implementations MUST round-trip
/// every byte of every file exactly. Hash + signature verification happens
/// on import; integrity failures throw <see cref="PersonaPackException"/>.
/// </summary>
public interface IPersonaPackFormat
{
    /// <summary>Write <paramref name="pack"/> as a <c>.salesman.zip</c> stream.</summary>
    Task ExportAsync(PersonaPack pack, Stream destination, CancellationToken cancellationToken = default);

    /// <summary>Read a <c>.salesman.zip</c> stream and return the verified pack.</summary>
    Task<PersonaPack> ImportAsync(Stream source, CancellationToken cancellationToken = default);
}
