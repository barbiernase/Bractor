// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// SERVER — der Laufzeit-Host (docs/konzept-werkbank.md §4.2). Er lädt die Umgebung (`--umgebung <name>` →
// umgebungen/umgebung.<name>.json; Umgebungsvariablen und Argumente überschreiben) und verdrahtet daraus alles: was existiert,
// liefern die generierten Tabellen, was aktiv ist und mit welchen Werten, sagt die Umgebung. Sim und Prod unterscheiden sich nur dort.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

using Domain.Laufzeit;            // AddCqrs der Komposition (meldet die Anwendung an)
using Infrastructure.Umgebung;    // LaufeAsync

var builder = WebApplication.CreateBuilder(args);
var umgebung = builder.AddCqrs(args);
var app = builder.Build();
await app.LaufeAsync(umgebung);
