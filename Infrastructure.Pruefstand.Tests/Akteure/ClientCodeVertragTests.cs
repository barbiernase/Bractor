using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.SourceGeneration;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Client-Code = Vertrag (docs/konzept-akteure.md §4, GR-CLIENT-CODE): der Client-Generator nimmt Handshake-Name, Hash, Abos, gesendete Commands
/// und gestellte Queries aus <c>[assembly: IstClient&lt;IX&gt;]</c> — mit derselben Lesart wie der Server (gleicher Hash); der Analyzer hält den
/// Client-Code dagegen (CQRS093–097).
/// </summary>
public class ClientCodeVertragTests
{
    private static readonly ImmutableArray<MetadataReference> Refs = Referenzen();
    private static ImmutableArray<MetadataReference> Referenzen()
    {
        var dazu = new[] { typeof(Abstractions.IClientVertrag).Assembly.Location, typeof(Client.Infrastructure.Abstractions.MessageContext).Assembly.Location };
        return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => a.Location)
            .Concat(dazu).Distinct().Select(l => (MetadataReference)MetadataReference.CreateFromFile(l)).ToImmutableArray();
    }

    private const string Vertrag = @"namespace Probe {
public partial class ProbeZiel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    public sealed record Mach(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
    public sealed record Verboten(Id<ProbeZiel> AggregateId) : ICommand<ProbeZiel>;
    public sealed record Frag : IQuery;
    public sealed record FragNicht : IQuery;
    public sealed record Passiert(Guid Id) : IEvent;
    public sealed record Anders(Guid Id) : IEvent;
    public sealed record Gast : IMensch, IDarf<Mach>, IDarf<Frag>;
    public interface IPult : IClientVertrag, ISendet<Mach>, IFragt<Frag> { void Auf(Passiert e); }
    public sealed record Getippt : IClientEvent;
}
";

    private static CSharpCompilation Probe(string code, string? vertrag = "Probe.IPult")
    {
        var kopf = "using System; using System.Collections.Generic; using Abstractions; using Client.Infrastructure.Abstractions;\n";
        var text = kopf + (vertrag != null ? $"[assembly: IstClient<{vertrag}>]\n" : "") + Vertrag + code;
        var comp = CSharpCompilation.Create("ClientProbe", [CSharpSyntaxTree.ParseText(text)], Refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty("die Probe selbst muss kompilieren");
        return comp;
    }

    private static async Task<string[]> Befunde(string code)
    {
        var mit = Probe(code).WithAnalyzers([new ClientVertragAnalyzer()]);
        return (await mit.GetAnalyzerDiagnosticsAsync()).Select(d => d.Id + ":" + d.GetMessage()).ToArray();
    }

    private static (string Generat, ImmutableArray<Diagnostic> Diagnosen) Generiert(CSharpCompilation comp, params ISourceGenerator[] gen)
    {
        var driver = CSharpGeneratorDriver.Create(gen).RunGeneratorsAndUpdateCompilation(comp, out var neu, out var diag);
        return (string.Join("\n", neu.SyntaxTrees.Skip(1).Select(t => t.ToString())), diag);
    }

    private const string Handler = @"namespace Probe.Ui {
    public partial class PultHandler {
        IEnumerable<object> Handle(Probe.Passiert e, MessageContext c) { yield return new Probe.Mach(Guid.Empty); yield return new Probe.Frag(); }
        IEnumerable<object> Handle(Probe.Getippt e, MessageContext c) { yield break; }
    }
}";

    [Fact]
    public async Task Code_im_Vertrag_ist_ohne_Befund()
        => (await Befunde(Handler)).Should().BeEmpty();

    [Fact]
    public async Task Command_Query_und_Event_ausserhalb_des_Vertrags_sind_Build_Fehler()
    {
        var b = await Befunde(@"namespace Probe.Ui {
    public partial class Daneben {
        IEnumerable<object> Handle(Probe.Anders e, MessageContext c) { yield return new Probe.Verboten(Guid.Empty); yield return new Probe.FragNicht(); }
    }
}");
        b.Should().HaveCount(3);
        b.Should().Contain(x => x.StartsWith("CQRS095:") && x.Contains("'Verboten'") && x.Contains("'Pult'"));
        b.Should().Contain(x => x.StartsWith("CQRS096:") && x.Contains("'FragNicht'"));
        b.Should().Contain(x => x.StartsWith("CQRS097:") && x.Contains("Daneben.Handle(Anders)"));
    }

    [Fact]
    public void Das_Wiring_nimmt_den_Rand_aus_dem_Vertrag_mit_dem_Hash_des_Servers()
    {
        var comp = Probe(Handler);
        var (client, diag) = Generiert(comp, new WiringGenerator().AsSourceGenerator());
        diag.Should().BeEmpty();
        Regex.Match(client, "public const string Vertrag = \"(\\w+)\"").Groups[1].Value.Should().Be("Pult");
        var clientHash = Regex.Match(client, "public const string VertragHash = \"(\\w+)\"").Groups[1].Value;
        var (server, _) = Generiert(comp, new Infrastructure.SourceGeneration.AkteurRechteGenerator());
        Regex.Match(server, "\\[\"Pult\"\\] = new ClientVertrag\\(\"Pult\", typeof\\([^)]*\\), \"(\\w+)\"").Groups[1].Value
            .Should().Be(clientHash).And.HaveLength(16);
        // Abonniert wird die Kenntnis, gesendet nur ISendet, gefragt nur IFragt — keine Selbstauskunft über „alle Events/Commands".
        var events = Abschnitt(client, "ServerEventTypes");
        events.Should().Contain("typeof(Probe.Passiert)").And.NotContain("Probe.Anders").And.NotContain("Domain.");
        Abschnitt(client, "CommandTypes").Should().Contain("typeof(Probe.Mach)").And.NotContain("Probe.Verboten").And.NotContain("Domain.");
        client.Should().Contain("queryBridge.Register<Probe.Frag,").And.NotContain("Register<Probe.FragNicht,");
    }

    [Fact]
    public void Ohne_IstClient_meldet_der_Generator_CQRS093()
    {
        var (_, diag) = Generiert(Probe(Handler, vertrag: null), new WiringGenerator().AsSourceGenerator());
        diag.Select(d => d.Id).Should().Equal("CQRS093");
    }

    [Fact]
    public void Ein_CSharp_Client_traegt_keine_Vertrags_Teile_CQRS094()
    {
        var comp = Probe(Handler + @"
namespace Probe {
    public interface IGastTeil : IAkteurVertrag<Gast> { OneOf<Mach> Auf(Anders e); }
    public interface IPultMitTeil : IClientVertrag, IGastTeil { void Auf(Passiert e); }
}", vertrag: "Probe.IPultMitTeil");
        var (_, diag) = Generiert(comp, new WiringGenerator().AsSourceGenerator());
        diag.Select(d => d.Id).Should().Equal("CQRS094");
        diag[0].GetMessage().Should().Contain("PultMitTeil").And.Contain("Gast");
    }

    private static string Abschnitt(string generat, string name)
    {
        var i = generat.IndexOf("IReadOnlyList<Type> " + name, StringComparison.Ordinal);
        return i < 0 ? "" : generat[i..generat.IndexOf("};", i, StringComparison.Ordinal)];
    }
}
