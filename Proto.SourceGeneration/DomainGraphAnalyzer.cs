using Abstractions.SourceGeneration;
using Microsoft.CodeAnalysis;

namespace Proto.SourceGeneration
{
    public class DomainGraphAnalyzer : IDomainAnalyzer
    {
        private readonly ITypeResolver _typeResolver;

        public DomainGraphAnalyzer(ITypeResolver typeResolver)
        {
            _typeResolver = typeResolver;
        }

        public static DomainGraphAnalyzer Create(Compilation compilation)
        {
            return new DomainGraphAnalyzer(new CompilationTypeResolver(compilation));
        }

        public List<TypeNode> AnalyzeMessagePayloads()
        {
            var results = new List<TypeNode>();
            const string markerInterface = "Abstractions.IMessagePayload";
            
            var payloadTypes = _typeResolver.GetTypesImplementing(markerInterface).ToList();
            
            Console.WriteLine($"\n[INFO] Gefunden: {payloadTypes.Count} IMessagePayload-Implementierungen");

            foreach (var payloadType in payloadTypes)
            {
                var symbol = (INamedTypeSymbol)payloadType;
                Console.WriteLine($"\n[ANALYSE] Erstelle Graph für: {symbol.Name}");
                var rootNode = ProcessTypeWithQueue(symbol);
                if (rootNode.DomainType != DomainType.Object)
                    Console.WriteLine($"  → Typ: {rootNode.DomainType}");
                results.Add(rootNode);
            }
            
            return results;
        }

        public TypeNode AnalyzeType(string typeName)
        {
            var type = _typeResolver.FindTypeByName(typeName) as INamedTypeSymbol;
            if (type == null)
                throw new ArgumentException($"Type '{typeName}' not found");
            
            return ProcessTypeWithQueue(type);
        }

        private TypeNode ProcessTypeWithQueue(INamedTypeSymbol startType)
        {
            var processedTypes = new Dictionary<string, TypeNode>();
            var queue = new Queue<(INamedTypeSymbol type, TypeNode node, string paramName)>();
            
            var rootNode = new TypeNode 
            { 
                Name = startType.Name,
                FullName = startType.ToDisplayString(),
                DomainType = GetDomainType(startType)
            };
            
            queue.Enqueue((startType, rootNode, startType.Name));
            
            while (queue.Count > 0)
            {
                var (currentType, currentNode, nodeName) = queue.Dequeue();
                var typeKey = currentType.ToDisplayString();
                
                if (currentNode.Name == null)
                    currentNode.Name = nodeName;
                
                if (processedTypes.ContainsKey(typeKey))
                {
                    var existingNode = processedTypes[typeKey];
                    currentNode.FullName = existingNode.FullName + " (Ref)";
                    currentNode.ConstructorParameters = new List<TypeNode>();
                    Console.WriteLine($"  [ZYKLUS] {currentType.Name} - bereits verarbeitet");
                    continue;
                }
                
                processedTypes[typeKey] = currentNode;
                Console.WriteLine($"  [PROCESS] {currentType.Name}");
                
                currentNode.DomainType = GetDomainType(currentType);
                if (currentNode.DomainType != DomainType.Object)
                    Console.WriteLine($"    → Typ: {currentNode.DomainType}");
                
                var constructor = currentType.GetMembers(".ctor")
                    .OfType<IMethodSymbol>()
                    .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                    .OrderByDescending(c => c.Parameters.Length)
                    .FirstOrDefault();
                
                if (constructor == null || constructor.Parameters.Length == 0)
                {
                    Console.WriteLine($"    → Keine Konstruktor-Parameter");
                    continue;
                }
                
                Console.WriteLine($"    → {constructor.Parameters.Length} Parameter gefunden");
                
                foreach (var param in constructor.Parameters)
                {
                    var paramNode = AnalyzeParameterType(param.Type, param.Name);
                    currentNode.ConstructorParameters.Add(paramNode);
                    
                    if (!_typeResolver.IsBaseType(paramNode.FullName) && !paramNode.IsCollection && !paramNode.IsDictionary)
                    {
                        var paramTypeSymbol = _typeResolver.FindTypeByName(paramNode.FullName) as INamedTypeSymbol;
                        if (paramTypeSymbol != null)
                        {
                            paramNode.DomainType = GetDomainType(paramTypeSymbol);
                            queue.Enqueue((paramTypeSymbol, paramNode, param.Name));
                        }
                    }
                    else if (paramNode.IsCollection && !_typeResolver.IsBaseType(paramNode.CollectionElementType))
                    {
                        var elementType = _typeResolver.FindTypeByName(paramNode.CollectionElementType) as INamedTypeSymbol;
                        if (elementType != null)
                        {
                            var elementNode = new TypeNode 
                            { 
                                Name = paramNode.CollectionElementType.Split('.').Last(),
                                FullName = paramNode.CollectionElementType,
                                DomainType = GetDomainType(elementType)
                            };
                            queue.Enqueue((elementType, elementNode, "Element"));
                            paramNode.ConstructorParameters.Add(elementNode);
                        }
                    }
                    else if (paramNode.IsDictionary)
                    {
                        if (!_typeResolver.IsBaseType(paramNode.DictionaryKeyType))
                        {
                            var keyType = _typeResolver.FindTypeByName(paramNode.DictionaryKeyType) as INamedTypeSymbol;
                            if (keyType != null)
                            {
                                var keyNode = new TypeNode 
                                { 
                                    Name = "Key",
                                    FullName = paramNode.DictionaryKeyType,
                                    DomainType = GetDomainType(keyType)
                                };
                                queue.Enqueue((keyType, keyNode, "Key"));
                                paramNode.ConstructorParameters.Add(keyNode);
                            }
                        }
                        if (!_typeResolver.IsBaseType(paramNode.DictionaryValueType))
                        {
                            var valueType = _typeResolver.FindTypeByName(paramNode.DictionaryValueType) as INamedTypeSymbol;
                            if (valueType != null)
                            {
                                var valueNode = new TypeNode 
                                { 
                                    Name = "Value",
                                    FullName = paramNode.DictionaryValueType,
                                    DomainType = GetDomainType(valueType)
                                };
                                queue.Enqueue((valueType, valueNode, "Value"));
                                paramNode.ConstructorParameters.Add(valueNode);
                            }
                        }
                    }
                }
            }
            
            return rootNode;
        }

        private TypeNode AnalyzeParameterType(ITypeSymbol paramType, string paramName)
        {
            var node = new TypeNode { Name = paramName };
            
            if (paramType is IArrayTypeSymbol arrayType)
            {
                node.IsCollection = true;
                node.CollectionElementType = arrayType.ElementType.ToDisplayString();
                node.FullName = $"{node.CollectionElementType}[]";
                Console.WriteLine($"      • {paramName}: Array von {node.CollectionElementType}");
            }
            else if (paramType is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var genericDef = namedType.OriginalDefinition.ToDisplayString();
                
                if (genericDef.Contains("Dictionary") || genericDef.Contains("IDictionary"))
                {
                    node.IsDictionary = true;
                    node.DictionaryKeyType = namedType.TypeArguments[0].ToDisplayString();
                    node.DictionaryValueType = namedType.TypeArguments[1].ToDisplayString();
                    node.FullName = paramType.ToDisplayString();
                    Console.WriteLine($"      • {paramName}: Dictionary<{node.DictionaryKeyType}, {node.DictionaryValueType}>");
                }
                else if (genericDef.Contains("List") || genericDef.Contains("IList") || 
                         genericDef.Contains("IEnumerable") || genericDef.Contains("ICollection") ||
                         genericDef.Contains("IReadOnlyList") || genericDef.Contains("IReadOnlyCollection"))
                {
                    node.IsCollection = true;
                    node.CollectionElementType = namedType.TypeArguments[0].ToDisplayString();
                    node.FullName = paramType.ToDisplayString();
                    Console.WriteLine($"      • {paramName}: Collection von {node.CollectionElementType}");
                }
                else
                {
                    node.FullName = paramType.ToDisplayString();
                    Console.WriteLine($"      • {paramName}: {node.FullName}");
                }
            }
            else
            {
                node.FullName = paramType.ToDisplayString();
                Console.WriteLine($"      • {paramName}: {node.FullName}");
            }
            
            return node;
        }
        
        private DomainType GetDomainType(INamedTypeSymbol type)
        {
            var interfaces = type.AllInterfaces.Select(i => i.ToDisplayString()).ToHashSet();
            
            if (interfaces.Contains("Abstractions.IEvent"))
                return DomainType.Event;
                
            if (interfaces.Contains("Abstractions.IIdentifiedCommand") || 
                interfaces.Contains("Abstractions.ICreationCommand"))
                return DomainType.Command;
                
            return DomainType.Object;
        }
    }
}
