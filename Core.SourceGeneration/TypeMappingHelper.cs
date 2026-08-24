using System.Collections.Generic;

namespace Core.SourceGeneration
{
    public static class TypeMappingHelper
    {
        // Die Skalar-Regeln leben ausschließlich in ProtoScalarSpecs (die EINE Quelle).
        // Diese Fassade bleibt als bequemer Abfragepunkt bestehen und delegiert nur.

        public static string GetProtoType(string csharpType) =>
            ProtoScalarSpecs.TryGet(csharpType, out var spec) ? spec.ProtoType : null;

        public static bool IsProtoScalarType(string typeName) =>
            !string.IsNullOrEmpty(typeName) && ProtoScalarSpecs.TryGet(typeName, out _);
        
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