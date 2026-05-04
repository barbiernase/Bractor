using System;

namespace Abstractions.Options;

/// <summary>
/// OBSOLETE: Diese Klasse wird nicht von AddCqrsFramework genutzt.
/// Die aktive Konfiguration läuft über CqrsFrameworkBuilder.
/// 
/// Wird in einer zukünftigen Phase entfernt. Vorerst beibehalten
/// um keine Compile-Fehler in eventuellem externem Code zu erzeugen.
/// </summary>
[Obsolete("Verwende CqrsFrameworkBuilder in AddCqrsFramework() statt CqrsFrameworkOptions. Wird in einer zukünftigen Version entfernt.")]
public class CqrsFrameworkOptions
{
    public ClusterOptions Cluster { get; } = new();
    public PubSubOptions PubSub { get; } = new();
    public EventStoreOptions EventStore { get; } = new();
}