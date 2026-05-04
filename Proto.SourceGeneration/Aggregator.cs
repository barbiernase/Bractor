using System;
using System.Collections.Generic;
using System.Linq;

public class TypeAggregator
{
    // Key: TypeName (FullName), Value: Maximale Tiefe
    private readonly Dictionary<string, int> _typeDepths = new();
    
    // Key: TypeName (FullName), Value: TypeNode mit Properties
    private readonly Dictionary<string, TypeNode> _typeDefinitions = new();

    public void AggregateGraphs(List<TypeNode> graphs)
    {
        foreach (var graph in graphs)
        {
            // Tiefensuche für jeden Graph starten
            TraverseNode(graph, 0);
        }
        Console.WriteLine("Done.");
    }

    private void TraverseNode(TypeNode node, int currentDepth)
    {
        var typeKey = node.FullName ?? node.Name;
        
        // Basis-Typen ignorieren
        if (IsBaseType(typeKey))
        {
            return;
        }
        
        // Collections/Dictionaries von Basis-Typen auch ignorieren
        if (node.IsCollection && IsBaseType(node.CollectionElementType))
        {
            return;
        }
        
        if (node.IsDictionary && 
            (IsBaseType(node.DictionaryKeyType) || IsBaseType(node.DictionaryValueType)))
        {
            return;
        }
        
        // Maximale Tiefe aktualisieren
        if (!_typeDepths.ContainsKey(typeKey) || _typeDepths[typeKey] < currentDepth)
        {
            _typeDepths[typeKey] = currentDepth;
        }
        
        // TypeNode speichern (überschreibt wenn bereits vorhanden - sollte aber identisch sein)
        _typeDefinitions[typeKey] = node;
        
        // Rekursiv alle Constructor-Parameter durchgehen
        foreach (var param in node.ConstructorParameters)
        {
            TraverseNode(param, currentDepth + 1);
        }
    }

    public List<(string TypeName, int Depth, TypeNode Node)> GetTypesSortedByDepth(string domainTypeFilter = null)
    {
        // Nach Tiefe sortieren (tiefste zuerst = höchste Zahl zuerst)
        var query = _typeDepths.AsEnumerable();
        
        // Filter anwenden wenn angegeben
        if (!string.IsNullOrEmpty(domainTypeFilter))
        {
            query = query.Where(kvp => _typeDefinitions[kvp.Key].DomainType == domainTypeFilter);
        }
        
        return query
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key) // Bei gleicher Tiefe alphabetisch
            .Select(kvp => (kvp.Key, kvp.Value, _typeDefinitions[kvp.Key]))
            .ToList();
    }

    
    
    private bool IsBaseType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return true;
        
        var baseTypes = new[] { "string", "int", "long", "bool", "double", "decimal", 
                               "float", "DateTime", "Guid", "TimeSpan", "byte", "char" };
        var cleanName = typeName.Split('.').Last();
        
        return baseTypes.Contains(cleanName) || typeName.StartsWith("System.");
    }
}