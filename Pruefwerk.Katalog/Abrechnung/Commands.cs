using Abstractions;

namespace Pruefwerk.Katalog.Abrechnung;

// Die Nachrichten von Freigabe und Mahnung — domänenneutral: ein Vorgang und ein Empfänger sind Guids, keine Ids einer Anwendung.

/// <summary>Ein Betrag ist zur Freigabe zu prüfen.</summary>
public record Pruefe(Guid Vorgang, decimal Betrag) : ICommand;

/// <summary>Der Betrag liegt unter der Grenze: freigegeben.</summary>
public record Freigegeben(Guid Vorgang) : ICommand;

/// <summary>Der Betrag liegt über der Grenze: ein Mensch sieht ihn sich an.</summary>
public record ZurPruefungVorzulegen(Guid Vorgang) : ICommand;

/// <summary>Ein offener Betrag ist anzumahnen — beim Empfänger, in einer Stufe.</summary>
public record Mahne(Guid Vorgang, Guid Empfaenger, decimal Betrag, int Stufe) : ICommand;

/// <summary>Die Mahnung ist verschickt — mit der Versandnummer.</summary>
public record Gemahnt(Guid Vorgang, Guid Empfaenger, string Versand, int Stufe) : ICommand;
