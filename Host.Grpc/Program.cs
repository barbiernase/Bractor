// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// SERVER — der Laufzeit-Host (docs/konzept-werkbank.md §4.2). Er lädt die Umgebung (`--umgebung <name>` →
// umgebungen/umgebung.<name>.json; Umgebungsvariablen und Argumente überschreiben) und verdrahtet daraus alles: was existiert,
// liefern die generierten Tabellen, was aktiv ist und mit welchen Werten, sagt die Umgebung. Sim und Prod unterscheiden sich nur dort.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

using Domain.Pipeline.Infrastructure;
using Infrastructure.Quellen;
using Infrastructure.Umgebung;

var builder = WebApplication.CreateBuilder(args);
var umgebung = builder.AddCqrs(args);

// Dienste hinter den Bild-Funktionen (Konfig-Record PreprocessingConfig aus dem Abschnitt Konfig der Umgebung).
builder.Services.AddDomainPipelineServices(builder.Configuration);

var app = builder.Build();

// Ein zweiter Weg in die Datei-Quelle: POST /webhook/datei mit der Nachricht als JSON startet (genau einmal) den Bildeingang.
app.MapQuellWebhook<Domain.ImagePair.DateiErkannt>("/webhook/datei");

await app.LaufeAsync(umgebung);
