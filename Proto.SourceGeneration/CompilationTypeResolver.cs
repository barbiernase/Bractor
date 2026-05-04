using Abstractions.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Proto.SourceGeneration
{
    public class CompilationTypeResolver : ITypeResolver
    {
        private readonly Compilation _compilation;
        private readonly Dictionary<string, INamedTypeSymbol> _allTypes = new Dictionary<string, INamedTypeSymbol>();

        public CompilationTypeResolver(Compilation compilation)
        {
            _compilation = compilation;
            CacheAllTypes();
        }

        private void CacheAllTypes()
        {
            Console.WriteLine("[INFO] Sammle alle Typen im Projekt...");
            
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
            
            Console.WriteLine($"[INFO] {_allTypes.Count} Typen im Projekt gefunden.");
        }

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
    }
}
