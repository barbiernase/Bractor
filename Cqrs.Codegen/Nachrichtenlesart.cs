using System.Reflection;

namespace Cqrs.Codegen;

/// <summary>
/// Die Nachrichten ohne Marker über die Metadaten der gebauten Assemblies — dieselbe Regel wie <c>Nachrichtenform</c> in den Generatoren: die Folgen
/// der Decides (schlichte Records ihrer Signatur, weder Event noch Frist) und die schlichten Ein- und Ausgaben der <c>Auf</c> von Funktionen
/// (Zwischenergebnisse). Sie reisen im Cluster (Wire-JSON) und über die Leitung (Nutzlast, {Typ}Dto-Message).
/// </summary>
public static class Nachrichtenlesart
{
    /// <summary>Alle Nachrichten ohne Marker, nach vollem Namen sortiert, jede einmal.</summary>
    public static List<Type> Alle(IEnumerable<Assembly> assemblies)
    {
        var aus = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var t in assemblies.SelectMany(PythonFunktionsform.TypenVon))
        {
            var basen = t.GetInterfaces();
            if (t.IsClass && basen.Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == "Abstractions.IDecider`1"))
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (m.Name == "Decide" && m.GetParameters() is { Length: > 0 } ps && ps[0].ParameterType.GetInterfaces().Any(i => i.FullName == "Abstractions.ICommand"))
                        foreach (var f in Faelle(m.ReturnType).Where(IstSchlicht)) aus.TryAdd(f.FullName!, f);
            if ((t.IsClass || t.IsInterface) && t.FullName != "Abstractions.IFunktion" && basen.Any(i => i.FullName == "Abstractions.IFunktion"))
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (m.Name == Abstractions.Funktionsvertrag.Auf && m.GetParameters() is { Length: > 0 } ps)
                        foreach (var f in Faelle(m.ReturnType).Prepend(ps[0].ParameterType).Where(IstSchlicht)) aus.TryAdd(f.FullName!, f);
        }
        return aus.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value).ToList();
    }

    /// <summary>Die Fälle einer Rückgabe: IEnumerable/Task/Nullable abgewickelt, bei OneOf seine Typargumente.</summary>
    public static IEnumerable<Type> Faelle(Type t)
    {
        while (t.IsGenericType && t.GetGenericArguments().Length == 1
               && t.Name is "IEnumerable`1" or "IAsyncEnumerable`1" or "Task`1" or "ValueTask`1" or "Nullable`1")
            t = t.GetGenericArguments()[0];
        return t.IsGenericType && t.Namespace == "Abstractions" && t.Name.StartsWith("OneOf`", StringComparison.Ordinal) ? t.GetGenericArguments() : new[] { t };
    }

    /// <summary>Ein schlichter Record ohne Marker des Vertrags.</summary>
    public static bool IstSchlicht(Type t) =>
        t.IsClass && !t.IsAbstract && !t.IsGenericType && t.Namespace != "System"
        && !t.GetInterfaces().Any(i => i.Namespace == "Abstractions" && i.Name is "IEvent" or "IFrist" or "ICommand" or "IQuery" or "IQueryResponse" or "IAusgabe");
}
