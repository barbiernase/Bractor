using System.Collections.Generic;

namespace Core.SourceGeneration
{
    public static class TypeMappingHelper
    {
        private static readonly Dictionary<string, string> ProtoTypeMapping = new()
        {
            // Non-nullable types
            { "System.String", "string" }, { "string", "string" },
            { "System.Int32", "int32" }, { "int", "int32" },
            { "System.Int64", "int64" }, { "long", "int64" },
            { "System.Boolean", "bool" }, { "bool", "bool" },
            { "System.Double", "double" }, { "double", "double" },
            { "System.Single", "float" }, { "float", "float" },
            { "System.Guid", "string" },
            { "System.Decimal", "string" }, { "decimal", "string" },
            { "System.DateTime", "int64" },
            { "System.DateTimeOffset", "int64" },
            
            // Nullable types - diese MÜSSEN auch als Skalartypen erkannt werden!
            { "string?", "string" },
            { "System.String?", "string" },
            { "int?", "int32" },
            { "System.Int32?", "int32" },
            { "System.Nullable<System.Int32>", "int32" },
            { "long?", "int64" },
            { "System.Int64?", "int64" },
            { "System.Nullable<System.Int64>", "int64" },
            { "bool?", "bool" },
            { "System.Boolean?", "bool" },
            { "System.Nullable<System.Boolean>", "bool" },
            { "double?", "double" },
            { "System.Double?", "double" },
            { "System.Nullable<System.Double>", "double" },
            { "float?", "float" },
            { "System.Single?", "float" },
            { "System.Nullable<System.Single>", "float" },
            { "decimal?", "string" },
            { "System.Decimal?", "string" },
            { "System.Nullable<System.Decimal>", "string" },
            { "System.Guid?", "string" },
            { "System.Nullable<System.Guid>", "string" },
            { "System.DateTime?", "int64" },
            { "System.Nullable<System.DateTime>", "int64" },
            { "System.DateTimeOffset?", "int64" },
            { "System.Nullable<System.DateTimeOffset>", "int64" },
        };
        
        public static string GetProtoType(string csharpType)
        {
            // Normalisiere den Typnamen
            var normalized = NormalizeTypeName(csharpType);
            return ProtoTypeMapping.TryGetValue(normalized, out var protoType) 
                ? protoType 
                : null;
        }
        
        public static bool IsProtoScalarType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
                
            var normalized = NormalizeTypeName(typeName);
            return ProtoTypeMapping.ContainsKey(normalized);
        }
        
        /// <summary>
        /// Prüft ob ein Typ ein nullable Typ ist (string?, int?, etc.)
        /// </summary>
        public static bool IsNullableType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
                
            return typeName.EndsWith("?") || 
                   typeName.StartsWith("System.Nullable<") ||
                   typeName.Contains("Nullable<");
        }
        
        /// <summary>
        /// Extrahiert den Basistyp aus einem nullable Typ
        /// </summary>
        public static string GetUnderlyingType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeName;
                
            // Handle "Type?" syntax
            if (typeName.EndsWith("?"))
                return typeName.TrimEnd('?');
                
            // Handle "System.Nullable<Type>" syntax
            if (typeName.StartsWith("System.Nullable<") && typeName.EndsWith(">"))
                return typeName.Substring(16, typeName.Length - 17);
                
            // Handle "Nullable<Type>" syntax
            if (typeName.StartsWith("Nullable<") && typeName.EndsWith(">"))
                return typeName.Substring(9, typeName.Length - 10);
                
            return typeName;
        }
        
        /// <summary>
        /// Normalisiert einen Typnamen für konsistente Lookups
        /// </summary>
        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeName;
                
            // Entferne Whitespace
            return typeName.Trim();
        }
    }
}