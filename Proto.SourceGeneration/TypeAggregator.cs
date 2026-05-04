using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions.SourceGeneration;

namespace Infrastructure.SourceGeneration
{
    public class TypeAggregator : ITypeAggregator
    {
        private readonly Dictionary<string, int> _typeDepths = new();
        private readonly Dictionary<string, TypeNode> _typeDefinitions = new();

        public void AggregateGraphs(List<TypeNode> graphs)
        {
            foreach (var graph in graphs)
            {
                TraverseNode(graph, 0);
            }
            Console.WriteLine("Done.");
        }

        private void TraverseNode(TypeNode node, int currentDepth)
        {
            var typeKey = node.FullName ?? node.Name;
            
            if (BaseTypes.IsBaseType(typeKey))
            {
                return;
            }
            
            if (node.IsCollection && BaseTypes.IsBaseType(node.CollectionElementType))
            {
                return;
            }
            
            if (node.IsDictionary && 
                (BaseTypes.IsBaseType(node.DictionaryKeyType) || BaseTypes.IsBaseType(node.DictionaryValueType)))
            {
                return;
            }
            
            if (!_typeDepths.ContainsKey(typeKey) || _typeDepths[typeKey] < currentDepth)
            {
                _typeDepths[typeKey] = currentDepth;
            }
            
            _typeDefinitions[typeKey] = node;
            
            foreach (var param in node.ConstructorParameters)
            {
                TraverseNode(param, currentDepth + 1);
            }
        }

        public List<TypeAggregationResult> GetTypesSortedByDepth(DomainType? domainTypeFilter = null)
        {
            var query = _typeDepths.AsEnumerable();
            
            if (domainTypeFilter.HasValue)
            {
                query = query.Where(kvp => _typeDefinitions[kvp.Key].DomainType == domainTypeFilter.Value);
            }
            
            return query
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Select(kvp => new TypeAggregationResult
                {
                    TypeName = kvp.Key,
                    FullName = kvp.Key,
                    MaxDepth = kvp.Value,
                    Node = _typeDefinitions[kvp.Key],
                    DomainType = _typeDefinitions[kvp.Key].DomainType
                })
                .ToList();
        }

        public IEnumerable<TypeNode> GetUniqueTypes()
        {
            return _typeDefinitions.Values.Distinct();
        }
        
        // Alte Methode für Kompatibilität
        public List<(string TypeName, int Depth, TypeNode Node)> GetTypesSortedByDepth(string domainTypeFilter = null)
        {
            DomainType? filter = null;
            if (!string.IsNullOrEmpty(domainTypeFilter))
            {
                if (Enum.TryParse<DomainType>(domainTypeFilter, out var parsed))
                    filter = parsed;
            }
            
            var results = GetTypesSortedByDepth(filter);
            return results.Select(r => (r.TypeName, r.MaxDepth, r.Node)).ToList();
        }
    }
}
