using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
namespace Domain.Client.Modules;

public enum ArbeitsModus { Live, Inspekt, Tag }

// Navigation
public record ImagePairAusgewaehlt(Guid PairId) : IClientEvent;
public record NavigationZielGesetzt(int Index) : IClientEvent;
public record SeiteAngefordert(int Seite) : IClientEvent;

// Modus + Filter
public record ModusGeaendert(ArbeitsModus Modus) : IClientEvent;
public record FilterWertGeaendert(string Wert) : IClientEvent;
public record InspektionsfilterGetoggelt() : IClientEvent;
public record TagLabelingGestartet(DateTimeOffset Datum) : IClientEvent;
public record TagLabelingBeendet() : IClientEvent;

// User-Intents
public record NavigateNextAngefordert() : IClientEvent;
public record NavigatePrevAngefordert() : IClientEvent;
public record NavigateNextOffenAngefordert() : IClientEvent;
public record LabelProduktAngefordert(Klassifikation Label) : IClientEvent;
public record LabelKameraAngefordert(Klassifikation Label) : IClientEvent;
public record ModusToggleAngefordert() : IClientEvent;
public record StarteTagLabelingAngefordert() : IClientEvent;
public record BeendeTagLabelingAngefordert() : IClientEvent;
