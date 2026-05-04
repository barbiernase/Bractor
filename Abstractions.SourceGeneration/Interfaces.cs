using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions.SourceGeneration
{
    public class TypeNode
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public DomainType DomainType { get; set; } = DomainType.Object;
        public bool IsCollection { get; set; }
        public bool IsDictionary { get; set; }
        public bool IsEnum { get; set; }  // <-- NEU: Markiert C#-Enums
        public string CollectionElementType { get; set; }
        public string DictionaryKeyType { get; set; }
        public string DictionaryValueType { get; set; }
        public List<TypeNode> ConstructorParameters { get; set; } = new List<TypeNode>();
        
        public override string ToString() => IsCollection ? $"{Name}: List<{CollectionElementType}>" : 
            IsDictionary ? $"{Name}: Dictionary<{DictionaryKeyType},{DictionaryValueType}>" : 
            IsEnum ? $"{Name}: enum {FullName}" :
            $"{Name}: {FullName}";
    }

    public enum DomainType
    {
        Object,
        Command,
        Event,
        Query,
        QueryResponse
    }

    public interface IDomainAnalyzer
    {
        List<TypeNode> AnalyzeMessagePayloads();
        TypeNode AnalyzeType(string typeName);
    }

    public interface ITypeResolver
    {
        object FindTypeByName(string typeName);
        IEnumerable<object> GetAllTypes();
        bool IsBaseType(string typeName);
        IEnumerable<object> GetTypesImplementing(string interfaceFullName);
    }

    public interface ITypeGraphBuilder
    {
        TypeNode BuildGraph(object rootType);
        int MaxDepth { get; set; }
    }

    public interface IMappingStrategy
    {
        string GenerateMapping(TypeNode sourceType, MappingDirection direction);
        bool CanMap(TypeNode type);
    }
    
    public enum MappingDirection
    {
        DtoToDomain,
        DomainToDto
    }

    public interface ICodeBuilder
    {
        ICodeBuilder AddUsing(string namespaceName);
        ICodeBuilder BeginNamespace(string namespaceName);
        ICodeBuilder EndNamespace();
        ICodeBuilder BeginClass(string className, string modifiers = "public");
        ICodeBuilder EndClass();
        ICodeBuilder AddMethod(string methodSignature, string methodBody);
        ICodeBuilder AddDictionaryField(string fieldName, string keyType, string valueType, bool isStatic = true, bool isReadonly = true);
        ICodeBuilder AddStaticConstructor(string body);
        string Build();
    }

    public static class BaseTypes
    {
        public static readonly HashSet<string> PrimitiveTypes = new()
        {
            "string", "int", "long", "bool", "double", "decimal", "float",
            "DateTime", "DateTimeOffset", "TimeSpan", "Guid",
            "byte", "sbyte", "short", "ushort", "uint", "ulong", "char",
            "object", "void"
        };
        
        public static readonly HashSet<string> SystemNamespaces = new()
        {
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Threading",
            "System.Threading.Tasks",
            "Microsoft",
            "Microsoft.Extensions"
        };
        
        public static bool IsBaseType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return true;
            
            var cleanName = typeName.Split('<')[0].Split('.').Last();
            
            if (PrimitiveTypes.Contains(cleanName))
                return true;
            
            foreach (var ns in SystemNamespaces)
            {
                if (typeName.StartsWith(ns + "."))
                    return true;
            }
            
            return false;
        }
        
        public static string GetSimpleName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return string.Empty;
                
            var withoutGenerics = fullTypeName.Split('<')[0];
            return withoutGenerics.Split('.').Last();
        }
    }

    public class TypeAggregationResult
    {
        public string TypeName { get; set; }
        public string FullName { get; set; }
        public int MaxDepth { get; set; }
        public TypeNode Node { get; set; }
        public DomainType DomainType { get; set; }
    }

    public interface ITypeAggregator
    {
        void AggregateGraphs(List<TypeNode> graphs);
        List<TypeAggregationResult> GetTypesSortedByDepth(DomainType? domainTypeFilter = null);
        IEnumerable<TypeNode> GetUniqueTypes();
    }

    public class MappingConfiguration
    {
        public string TargetNamespace { get; set; } = "Infrastructure.Mappers";
        public string MapperClassName { get; set; } = "DtoMapper";
        public string DtoSuffix { get; set; } = "Dto";
        public bool UseDictionaryMapping { get; set; } = true;
        public bool IncludeComments { get; set; } = true;
    }

    public class GenerationResult
    {
        public string FileName { get; set; }
        public string Content { get; set; }
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}