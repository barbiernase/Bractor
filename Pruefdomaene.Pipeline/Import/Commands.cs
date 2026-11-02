using Abstractions;

namespace Pruefdomaene.Pipeline.Import;

// COMMANDS ohne Ziel am Kopf des Import-Graphen (docs/04 §4.4): ein Akteur gibt sie hinein, nie ins Log.

/// <summary>
/// Ein Import ist angefordert — ein Command ohne Ziel von draußen an den Kopf des Import-Graphen. Seine Command-Id vergibt der Absender und schickt sie beim
/// Wiederholen wieder mit: derselbe offene Eingang, derselbe Lauf.
/// </summary>
public sealed record ImportAngefordert(int Zeilen) : ICommand;
