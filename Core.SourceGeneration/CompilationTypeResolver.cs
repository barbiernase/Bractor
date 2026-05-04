using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions.SourceGeneration;

namespace Core.SourceGeneration
{
    public class CompilationTypeResolver : ITypeResolver
    {
        private readonly Compilation _compilation;
        private readonly Dictionary<string, INamedTypeSymbol> _allTypes = new Dictionary<string, INamedTypeSymbol>();
        private readonly bool _useGlobalNamespace;

        public CompilationTypeResolver(Compilation compilation, bool useGlobalNamespace = false)
        {
            _compilation = compilation;
            _useGlobalNamespace = useGlobalNamespace;
            CacheAllTypes();
        }

        private void CacheAllTypes()
        {
            Console.WriteLine("[INFO] Sammle alle Typen im Projekt...");
            
            if (_useGlobalNamespace)
            {
                // Für Source Generators: Durchsuche ALLE Namespaces
                var visitor = new TypeCollectorVisitor(_allTypes);
                visitor.Visit(_compilation.GlobalNamespace);
            }
            else
            {
                // Für Console App: Nur SyntaxTrees (schneller)
                var allTypes = _compilation.SyntaxTrees
                    .SelectMany(tree => tree.GetRoot()
                        .DescendantNodes()
                        .OfType<TypeDeclarationSyntax>()
                        .Select(node => _compilation.GetSemanticModel(tree).GetDeclaredSymbol(node)))
                    .OfType<INamedTypeSymbol>()
                    .Where(t => t != null);

                foreach (var type in allTypes)
                {
                    _allTypes[type.ToDisplayString()] = type;
                    _allTypes[type.Name] = type;
                }
            }
            
            Console.WriteLine($"[INFO] {_allTypes.Count} Typen gefunden.");
        }

        // Rest bleibt gleich...
        public object FindTypeByName(string typeName)
        {
            var cleanName = typeName.Split('<')[0];
            
            if (_allTypes.TryGetValue(typeName, out var type))
                return type;
            
            if (_allTypes.TryGetValue(cleanName, out type))
                return type;
            
            return _compilation.GetTypeByMetadataName(typeName) as INamedTypeSymbol;
        }

        public IEnumerable<object> GetAllTypes()
        {
            return _allTypes.Values;
        }

        public bool IsBaseType(string typeName)
        {
            if (BaseTypes.IsBaseType(typeName))
                return true;
                
            return FindTypeByName(typeName) == null;
        }

        public IEnumerable<object> GetTypesImplementing(string interfaceFullName)
        {
            return _allTypes.Values
                .Where(t => t.AllInterfaces.Any(i => i.ToDisplayString() == interfaceFullName));
        }

        // Symbol Visitor für GlobalNamespace
        private class TypeCollectorVisitor : SymbolVisitor
        {
            private readonly Dictionary<string, INamedTypeSymbol> _types;

            public TypeCollectorVisitor(Dictionary<string, INamedTypeSymbol> types)
            {
                _types = types;
            }

            public override void VisitNamespace(INamespaceSymbol symbol)
            {
                foreach (var member in symbol.GetMembers())
                {
                    member.Accept(this);
                }
            }

            public override void VisitNamedType(INamedTypeSymbol symbol)
            {
                if (!symbol.IsAbstract && symbol.TypeKind != TypeKind.Interface)
                {
                    _types[symbol.ToDisplayString()] = symbol;
                    _types[symbol.Name] = symbol;
                }
            }
        }
    }
}