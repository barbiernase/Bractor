// Der Laufzeit-Host der Prüfdomäne (docs/06 §6.5) — wie Host.Grpc, mit der Komposition der zweiten Anwendung.

using Pruefdomaene.Laufzeit;      // AddCqrs der Komposition (meldet die Anwendung an)
using Infrastructure.Umgebung;    // LaufeAsync

var builder = WebApplication.CreateBuilder(args);
var umgebung = builder.AddCqrs(args);
var app = builder.Build();
await app.LaufeAsync(umgebung);
