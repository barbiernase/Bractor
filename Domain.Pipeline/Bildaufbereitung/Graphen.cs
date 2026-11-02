using Abstractions;
using Domain.Pipeline.ImagePair;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// Die Bildaufbereitung (docs/04 §4.4): eine erkannte Datei wird gelesen, verkleinert, im Kontrast ausgeglichen und als Bild ihres Paars gemeldet. Der
/// Kopf nimmt die Quell-Nachricht <see cref="DateiErkannt"/>; die Kanten folgen aus den Typen (BildEingelesen → Verkleinern → Kontrast → Eingang);
/// hinaus geht <c>NimmBildAuf</c> an das Paar und die Hinweise.
/// </summary>
public sealed record Bildaufbereitung(IBildEinlesen Einlesen, IBildVerkleinerung Verkleinern, IKontrastausgleich Kontrast, Bildeingang Eingang) : IGraph;
