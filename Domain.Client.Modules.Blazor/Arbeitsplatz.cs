using Abstractions;
using Domain.Clients;

// Dieser Client IST der Arbeitsplatz (docs/konzept-akteure.md §4): Handshake-Name, Vertrags-Hash, Abos, gesendete Commands und gestellte
// Queries kommen aus IArbeitsplatz — der Analyzer hält den Client-Code dagegen (CQRS095–097).
[assembly: IstClient<IArbeitsplatz>]
