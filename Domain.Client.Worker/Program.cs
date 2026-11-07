using Client.Infrastructure.Reaktionen;
using Domain.Client.Worker;

// Der Worker als Prozess (die Werkbank startet ihn wie jeden Worker): CQRS_GRPC_HOST/CQRS_GRPC_PORT/CQRS_AKTEUR_TOKEN aus der Umgebung, der Stand der
//   Reaktionen in einer Datei (CQRS_REAKTION_STAND, sonst neben dem Programm). Bricht die Verbindung ab, verbindet er neu und läuft ab dem Stand weiter.
var host = Environment.GetEnvironmentVariable("CQRS_GRPC_HOST") ?? "localhost";
var port = Environment.GetEnvironmentVariable("CQRS_GRPC_PORT") ?? "5100";
var stand = new DateiReaktionsStand(Environment.GetEnvironmentVariable("CQRS_REAKTION_STAND") ?? Path.Combine(AppContext.BaseDirectory, "reaktionen"));
using var ende = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; ende.Cancel(); };
await DatensatzReaktionen.LaufeAsync($"http://{host}:{port}", Environment.GetEnvironmentVariable("CQRS_AKTEUR_TOKEN"), stand, "datensatzworker", ende.Token,
    melde: m => Console.WriteLine("[DatensatzWorker] " + m));
