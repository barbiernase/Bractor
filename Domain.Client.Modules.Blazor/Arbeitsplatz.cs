using Abstractions;
using Domain.Akteure;

// Diese Assembly IST die ArbeitsplatzApp des Clients Arbeitsplatz (docs/konzept-akteure.md §4): Handshake-Name, Vertrags-Hash, Abos, gesendete
// Commands und gestellte Queries kommen aus dem Vertrag der App — der Analyzer hält den Code dagegen (CQRS095–097).
[assembly: IstApp<ArbeitsplatzApp>]
