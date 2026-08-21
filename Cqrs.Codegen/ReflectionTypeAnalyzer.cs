using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abstractions.SourceGeneration;

namespace Cqrs.Codegen;

/// <summary>
/// Reflexions-Pendant zum Roslyn-<c>MultiCompilationAnalyzer</c>. Baut dieselben
/// <see cref="TypeNode"/>-Graphen — aber aus den GEBAUTEN Domain-DLLs (via
/// <c>System.Reflection.MetadataLoadContext</c>), NICHT aus einer Roslyn-<c>Compilation</c>.
///
/// Warum: der bisherige Prepass öffnete die ganze Solution über <c>MSBuildWorkspace</c>
/// (geschachteltes MSBuild, fragil im Build, plattformabhängig). Diese Variante liest nur
/// Metadaten der drei Domain-Assemblies und läuft daher als schlankes MSBuild-Pre-Build-Target.
///
/// WICHTIG (MetadataLoadContext): geladene Typen sind INSPECTION-ONLY. <c>typeof(...)</c>/
/// <c>IsAssignableFrom</c>/<c>Nullable.GetUnderlyingType</c> funktionieren NICHT (verschiedene
/// Typ-Welten). Interface-Zugehörigkeit daher strikt über <see cref="Type.FullName"/>-Vergleich,
/// Nullable-Erkennung über die generische Definition <c>System.Nullable`1</c>.
///
/// Der erzeugte <see cref="TypeNode.FullName"/> bildet exakt Roslyns
/// <c>ToDisplayString()</c> nach (siehe <see cref="DisplayName"/>) — damit der weiterverwendete
/// <c>FileGenerator</c> byte-identische .proto liefert.
/// </summary>
public sealed class ReflectionTypeAnalyzer
{
    // Alle konkreten Typen der geladenen Domain-Assemblies, keyed über den Roslyn-kompatiblen
    // Anzeigenamen (identisch zum _allTypes-Cache im MultiCompilationAnalyzer). Dient der
    // Value-Object-Rekursion (Typ per FullName im Graphen nachschlagen).
    private readonly Dictionary<string, Type> _allTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeNode> _analyzedTypes = new(StringComparer.Ordinal);

    public ReflectionTypeAnalyzer(IEnumerable<Assembly> assemblies)
    {
        foreach (var asm in assemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                // Deckungsgleich mit TypeCollector.VisitNamedType: keine Interfaces, keine
                // abstrakten Typen (statische Klassen sind in Reflection abstract+sealed → raus).
                if (t.IsInterface || t.IsAbstract)
                    continue;
                _allTypes.TryAdd(DisplayName(t), t);
            }
        }

        Console.WriteLine($"   {_allTypes.Count} Typen gecached");
    }

    /// <summary>Funktionaler Ersatz für <c>MultiCompilationAnalyzer.AnalyzeTypesImplementing</c>.</summary>
    public List<TypeNode> AnalyzeTypesImplementing(string interfaceFullName)
    {
        var results = new List<TypeNode>();
        foreach (var type in _allTypes.Values)
        {
            if (type.GetInterfaces().Any(i => i.FullName == interfaceFullName))
                results.Add(BuildTypeNode(type));
        }

        Console.WriteLine($"   {results.Count} Typen implementieren {interfaceFullName.Split('.').Last()}");
        return results;
    }

    private TypeNode BuildTypeNode(Type type)
    {
        var fullName = DisplayName(type);
        if (_analyzedTypes.TryGetValue(fullName, out var cached))
            return CloneTypeNode(cached);

        return BuildTypeNodeRecursive(type, new HashSet<string>(StringComparer.Ordinal));
    }

    private TypeNode BuildTypeNodeRecursive(Type type, HashSet<string> processingStack)
    {
        var fullName = DisplayName(type);

        if (_analyzedTypes.TryGetValue(fullName, out var cached))
            return CloneTypeNode(cached);

        // Zyklen abfangen (Selbst-Referenz im Value-Object-Graphen).
        if (processingStack.Contains(fullName))
        {
            return new TypeNode
            {
                Name = type.Name,
                FullName = fullName,
                DomainType = GetDomainType(type)
            };
        }

        processingStack.Add(fullName);

        var node = new TypeNode
        {
            Name = type.Name,
            FullName = fullName,
            DomainType = GetDomainType(type),
            IsEnum = type.IsEnum
        };

        if (node.IsEnum)
        {
            _analyzedTypes[fullName] = node;
            processingStack.Remove(fullName);
            return node;
        }

        var constructor = GetPrimaryConstructor(type);
        if (constructor != null && constructor.GetParameters().Length > 0)
        {
            foreach (var param in constructor.GetParameters())
                node.ConstructorParameters.Add(AnalyzeParameter(param, processingStack));
        }

        _analyzedTypes[fullName] = node;
        processingStack.Remove(fullName);
        return node;
    }

    /// <summary>
    /// Primärer Konstruktor = öffentlich mit den meisten Parametern (Record-Primary-Ctor).
    /// Fallback auf nicht-öffentliche, exakt wie <c>MultiCompilationAnalyzer.GetPrimaryConstructor</c>
    /// (Strategien 1 + 2). Die Record-Copy-Ctors sind protected/private → nicht in der Public-Menge.
    /// </summary>
    private static ConstructorInfo? GetPrimaryConstructor(Type type)
    {
        var pub = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();
        if (pub.Count > 0 && pub[0].GetParameters().Length > 0)
            return pub[0];

        var all = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();
        if (all.Count > 0 && all[0].GetParameters().Length > 0)
            return all[0];

        return null;
    }

    private TypeNode AnalyzeParameter(ParameterInfo param, HashSet<string> processingStack)
    {
        var paramType = param.ParameterType;
        var node = new TypeNode { Name = param.Name };

        // Enum → Skalar
        if (paramType.IsEnum)
        {
            node.FullName = DisplayName(paramType);
            node.IsEnum = true;
            return node;
        }

        // Nullable<Enum> → auch als Enum behandeln (FullName trägt das "?"-Suffix).
        var underlying = GetUnderlyingNullable(paramType);
        if (underlying != null && underlying.IsEnum)
        {
            node.FullName = DisplayName(paramType);
            node.IsEnum = true;
            return node;
        }

        if (paramType.IsArray)
        {
            var elementType = paramType.GetElementType()!;
            node.IsCollection = true;
            node.CollectionElementType = DisplayName(elementType);
            node.FullName = $"{node.CollectionElementType}[]";
            AnalyzeCollectionElementType(node, elementType, processingStack);
        }
        else if (paramType.IsGenericType)
        {
            var genericDef = DisplayName(paramType.GetGenericTypeDefinition());
            var typeArgs = paramType.GetGenericArguments();

            if (genericDef.Contains("List") || genericDef.Contains("IList") ||
                genericDef.Contains("IEnumerable") || genericDef.Contains("ICollection") ||
                genericDef.Contains("IReadOnlyList") || genericDef.Contains("IReadOnlyCollection"))
            {
                node.IsCollection = true;
                node.CollectionElementType = DisplayName(typeArgs[0]);
                node.FullName = DisplayName(paramType);
                AnalyzeCollectionElementType(node, typeArgs[0], processingStack);
            }
            else if (genericDef.Contains("Dictionary") || genericDef.Contains("IDictionary"))
            {
                node.IsDictionary = true;
                node.DictionaryKeyType = DisplayName(typeArgs[0]);
                node.DictionaryValueType = DisplayName(typeArgs[1]);
                node.FullName = DisplayName(paramType);
            }
            else
            {
                node.FullName = DisplayName(paramType);
                AnalyzeComplexType(node, paramType, processingStack);
            }
        }
        else
        {
            node.FullName = DisplayName(paramType);
            AnalyzeComplexType(node, paramType, processingStack);
        }

        return node;
    }

    private void AnalyzeComplexType(TypeNode node, Type type, HashSet<string> processingStack)
    {
        if (BaseTypes.IsBaseType(node.FullName))
            return;

        if (_allTypes.TryGetValue(node.FullName, out var referencedType))
        {
            node.DomainType = GetDomainType(referencedType);

            var fullNode = BuildTypeNodeRecursive(referencedType, processingStack);
            foreach (var param in fullNode.ConstructorParameters)
                node.ConstructorParameters.Add(CloneTypeNode(param));
        }
    }

    private void AnalyzeCollectionElementType(TypeNode parentNode, Type elementType, HashSet<string> processingStack)
    {
        var elementFullName = DisplayName(elementType);
        if (BaseTypes.IsBaseType(elementFullName))
            return;

        if (_allTypes.TryGetValue(elementFullName, out var elementSymbol))
        {
            var elementNode = BuildTypeNodeRecursive(elementSymbol, processingStack);
            parentNode.ConstructorParameters.Add(CloneTypeNode(elementNode));
        }
    }

    private static TypeNode CloneTypeNode(TypeNode source)
    {
        var clone = new TypeNode
        {
            Name = source.Name,
            FullName = source.FullName,
            DomainType = source.DomainType,
            IsCollection = source.IsCollection,
            IsDictionary = source.IsDictionary,
            IsEnum = source.IsEnum,
            CollectionElementType = source.CollectionElementType,
            DictionaryKeyType = source.DictionaryKeyType,
            DictionaryValueType = source.DictionaryValueType
        };
        foreach (var param in source.ConstructorParameters)
            clone.ConstructorParameters.Add(CloneTypeNode(param));
        return clone;
    }

    private static DomainType GetDomainType(Type type)
    {
        var interfaces = type.GetInterfaces().Select(i => i.FullName).ToHashSet(StringComparer.Ordinal);

        if (interfaces.Contains("Abstractions.IEvent"))
            return DomainType.Event;
        if (interfaces.Contains("Abstractions.ICommand") ||
            interfaces.Contains("Abstractions.ICreationCommand"))
            return DomainType.Command;
        if (interfaces.Contains("Abstractions.IPipelineTrigger"))
            return DomainType.Trigger;
        if (interfaces.Contains("Abstractions.IQuery"))
            return DomainType.Query;
        if (interfaces.Contains("Abstractions.IQueryResponse"))
            return DomainType.QueryResponse;

        return DomainType.Object;
    }

    // ── Roslyn-kompatibler Anzeigename ──────────────────────────────────────────────────────
    // Bildet ToDisplayString() nach: Special-Types als C#-Keywords, Nullable als "?"-Suffix,
    // Arrays als "[]", Generics als "Ns.Name<Arg, ...>", sonst voll namespace-qualifiziert.
    private static readonly Dictionary<string, string> SpecialTypeKeywords = new(StringComparer.Ordinal)
    {
        ["System.String"] = "string",
        ["System.Int32"] = "int",
        ["System.Int64"] = "long",
        ["System.Boolean"] = "bool",
        ["System.Double"] = "double",
        ["System.Single"] = "float",
        ["System.Decimal"] = "decimal",
        ["System.Object"] = "object",
        ["System.Void"] = "void",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.UInt32"] = "uint",
        ["System.UInt64"] = "ulong",
        ["System.Char"] = "char",
    };

    public static string DisplayName(Type t)
    {
        // Nullable<T> → DisplayName(T) + "?"
        var underlying = GetUnderlyingNullable(t);
        if (underlying != null)
            return DisplayName(underlying) + "?";

        if (t.IsArray)
            return DisplayName(t.GetElementType()!) + "[]";

        if (t.IsGenericParameter)
            return t.Name;

        if (t.IsGenericType)
        {
            var name = FullNameWithoutArity(t.GetGenericTypeDefinition());
            var args = t.GetGenericArguments().Select(DisplayName);
            return $"{name}<{string.Join(", ", args)}>";
        }

        var fn = FullNameWithoutArity(t);
        return SpecialTypeKeywords.TryGetValue(fn, out var kw) ? kw : fn;
    }

    /// <summary>Namespace-qualifizierter Name mit '.'-getrennten geschachtelten Typen, ohne `n-Arity.</summary>
    private static string FullNameWithoutArity(Type t)
    {
        var name = t.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name.Substring(0, tick);

        if (t.IsNested && t.DeclaringType != null)
            return FullNameWithoutArity(t.DeclaringType) + "." + name;

        return string.IsNullOrEmpty(t.Namespace) ? name : t.Namespace + "." + name;
    }

    /// <summary>MLC-sicheres <c>Nullable.GetUnderlyingType</c> (per FullName, kein typeof-Vergleich).</summary>
    private static Type? GetUnderlyingNullable(Type t)
    {
        if (t.IsGenericType && !t.IsGenericTypeDefinition &&
            t.GetGenericTypeDefinition().FullName == "System.Nullable`1")
        {
            return t.GetGenericArguments()[0];
        }
        return null;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Select(t => t!);
        }
    }
}
