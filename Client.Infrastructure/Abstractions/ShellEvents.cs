namespace Client.Infrastructure.Abstractions;
public record OpenSettingsRequested : IClientEvent;

/// <summary>
/// Bittet die Shell, die aktive Bühne (Stage-Tab) auf die mit <see cref="StageId"/> zu wechseln.
/// Client-lokal — ein Modul dispatcht dies (z. B. die Galerie bei Enter „ins Bild"), die Shell
/// setzt daraufhin ihren aktiven Tab. Unbekannte Id = folgenlos.
/// </summary>
public record StageWechselAngefordert(string StageId) : IClientEvent;
