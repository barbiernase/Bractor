using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions.SourceGeneration;

namespace Core.SourceGeneration
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
        }

        private void TraverseNode(TypeNode node, int currentDepth)
        {
            var typeKey = node.FullName ?? node.Name;
    
            if (BaseTypes.IsBaseType(typeKey))
            {
                // Collection/Dictionary-Container sind zwar selbst Base-Types
                // (z.B. IReadOnlyList<RegionBewertung>), aber ihre Kinder
                // (Element-Typen wie RegionBewertung, RegionPosition, etc.) müssen
                // trotzdem traversiert werden, damit sie als Value Objects entdeckt
                // und als Proto-Messages generiert werden.
                if (node.IsCollection || node.IsDictionary)
                {
                    foreach (var param in node.ConstructorParameters)
                    {
                        TraverseNode(param, currentDepth + 1);
                    }
                }
                return;
            }
    
            // FIX: Collection/Dictionary-Nodes dürfen NIEMALS selbst als Typ-Definitionen
            // registriert werden — sie sind Container, keine Domain-Typen.
            // Nur ihre Element-/Key-/Value-Typen (als ConstructorParameters angehängt)
            // werden traversiert und ggf. registriert.
            // Ohne diesen Guard wird z.B. IReadOnlyList<RegionBewertung> als Value Object
            // registriert, und der DtoMapper generiert kaputten Code dafür
            // ("ProtoValueObjectHelpers.dto": Instanzmember in statischer Klasse).
            if (node.IsCollection || node.IsDictionary)
            {
                foreach (var param in node.ConstructorParameters)
                {
                    TraverseNode(param, currentDepth + 1);
                }
                return;
            }
    
            if (!_typeDepths.ContainsKey(typeKey) || _typeDepths[typeKey] < currentDepth)
            {
                _typeDepths[typeKey] = currentDepth;
            }
    
            // Nur überschreiben wenn noch kein Eintrag existiert,
            // oder wenn der neue Node einen "echten" Typ-Namen hat
            var hasRealTypeName = IsRealTypeName(node);
    
            if (!_typeDefinitions.ContainsKey(typeKey))
            {
                _typeDefinitions[typeKey] = node;
            }
            else if (hasRealTypeName && !IsRealTypeName(_typeDefinitions[typeKey]))
            {
                // Ersetze Parameter-Node durch echten Typ-Node
                _typeDefinitions[typeKey] = node;
            }
    
            foreach (var param in node.ConstructorParameters)
            {
                TraverseNode(param, currentDepth + 1);
            }
        }

        /// <summary>
        /// Prüft ob der Node einen echten Typ-Namen hat (nicht einen Parameter-Namen).
        /// Ein echter Typ-Name ist der letzte Teil des FullName.
        /// </summary>
        private bool IsRealTypeName(TypeNode node)
        {
            if (string.IsNullOrEmpty(node.FullName) || string.IsNullOrEmpty(node.Name))
                return false;
    
            var expectedName = node.FullName.Split('.').Last();
            return node.Name == expectedName;
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