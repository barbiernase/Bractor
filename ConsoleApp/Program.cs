using System;
using System.Threading.Tasks;
using Abstractions;
using Core;
using Domain.Profil;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        
        Console.WriteLine("=== CQRS/ES Demo ===");
        
        var messenger = serviceProvider.GetRequiredService<IAggregateMessenger>();
        var repository = serviceProvider.GetRequiredService<IAggregateRepository>();
        var handlerFactory = serviceProvider.GetRequiredService<IAggregateHandlerFactory>();
        
        var profilId = Guid.NewGuid();

        // 1. Profil erstellen
        Console.WriteLine("\n[1] Erstelle Profil...");
        var session1 = await Session<Profil>.LoadAsync(profilId, messenger, repository, handlerFactory);
        var createCommand = new ErstelleProfil(profilId, "Max Mustermann");
        var createEnvelope = new CommandEnvelope
        {
            Payload = createCommand,
            ExpectedVersion = 0
        };
        session1.Dispatch<ErstelleProfil>(createEnvelope);
        var result1 = await session1.SaveChangesAsync();
        PrintResult(result1);

        // 2. Namen ändern
        Console.WriteLine("\n[2] Ändere Namen...");
        var session2 = await Session<Profil>.LoadAsync(profilId, messenger, repository, handlerFactory);
        var changeNameCommand = new ÄndereProfilnamen(profilId, "Erika Musterfrau");
        var changeNameEnvelope = new CommandEnvelope{
            Payload =changeNameCommand,
            ExpectedVersion = 1
            
        }; // Basiert auf Version 1
        session2.Dispatch<ÄndereProfilnamen>(changeNameEnvelope);
        var result2 = await session2.SaveChangesAsync();
        PrintResult(result2);

        // 3. Concurrency-Konflikt provozieren
        Console.WriteLine("\n[3] Probiere, den Namen erneut mit veralteter Version 1 zu ändern...");
        var session3 = await Session<Profil>.LoadAsync(profilId, messenger, repository, handlerFactory);
        var staleChangeCommand = new ÄndereProfilnamen(profilId, "Veralteter Name");
        var staleEnvelope = new CommandEnvelope{
            Payload = staleChangeCommand,
            ExpectedVersion =  1}; // Basiert auf veralteter Version 1
        
        try
        {
            session3.Dispatch<ÄndereProfilnamen>(staleEnvelope);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"-> Fehler wie erwartet: {ex.Message}");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<InMemoryAggregateRepository>();
        services.AddSingleton<IAggregateRepository>(sp => sp.GetRequiredService<InMemoryAggregateRepository>());
        services.AddSingleton<IAggregateHandlerFactory, AggregateHandlerFactory>();
        services.AddSingleton<IAggregateMessenger, InMemoryAggregateMessenger>();
    }
    
    private static void PrintResult(CommandResult result)
    {
        if (result.Success)
        {
            Console.WriteLine($"-> Erfolg! {result.Events.Count} Event(s) erzeugt.");
        }
        else
        {
            Console.WriteLine($"-> FEHLER: {result.ErrorMessage}");
        }
    }
}