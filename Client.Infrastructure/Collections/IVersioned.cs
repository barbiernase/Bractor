namespace Client.Infrastructure.Collections;

/// <summary>Monoton steigender Versions-Zähler für selektive Change-Detection.</summary>
public interface IVersioned { int Version { get; } }
