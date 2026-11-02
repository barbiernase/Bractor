using System.Reflection;

namespace Cqrs.Codegen;

/// <summary>
/// Die Commands ohne Ziel über die Metadaten der gebauten Assemblies — dieselbe Regel wie <c>Nachrichtenform</c> in den Generatoren: ein konkreter Typ mit
/// <c>ICommand</c>, aber ohne <c>ICommand&lt;T&gt;</c>. Sie reisen im Cluster (Wire-JSON) und über die Leitung (Nutzlast, {Typ}Dto-Message).
/// </summary>
public static class Nachrichtenlesart
{
    /// <summary>Alle Commands ohne Ziel, nach vollem Namen sortiert, jeder einmal.</summary>
    public static List<Type> Alle(IEnumerable<Assembly> assemblies) =>
        assemblies.SelectMany(PythonVertragsEmitter.TypenVon).Where(IstOhneZiel)
            .GroupBy(t => t.FullName!, StringComparer.Ordinal).Select(g => g.First()).OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();

    /// <summary>Die Fälle einer Rückgabe: IEnumerable/Task/Nullable abgewickelt, bei OneOf seine Typargumente.</summary>
    public static IEnumerable<Type> Faelle(Type t)
    {
        while (t.IsGenericType && t.GetGenericArguments().Length == 1
               && t.Name is "IEnumerable`1" or "IAsyncEnumerable`1" or "Task`1" or "ValueTask`1" or "Nullable`1")
            t = t.GetGenericArguments()[0];
        return t.IsGenericType && t.Namespace == "Abstractions" && t.Name.StartsWith("OneOf`", StringComparison.Ordinal) ? t.GetGenericArguments() : new[] { t };
    }

    /// <summary>Ein Command ohne Ziel: <c>ICommand</c>, aber kein <c>IAggregatCommand</c>.</summary>
    public static bool IstOhneZiel(Type t) =>
        t.IsClass && !t.IsAbstract && !t.IsGenericType
        && t.GetInterfaces().Any(i => i.FullName == "Abstractions.ICommand")
        && !t.GetInterfaces().Any(i => i.FullName == "Abstractions.IAggregatCommand");
}
