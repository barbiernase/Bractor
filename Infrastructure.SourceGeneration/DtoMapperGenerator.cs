using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Abstractions.SourceGeneration;
using Core.SourceGeneration;

namespace Infrastructure.SourceGeneration
{
    [Generator]
    public class DtoMapperSourceGenerator : ISourceGenerator
    {
        /// <summary>
        /// Bekannte Enum-Typnamen — wird in GenerateMapperCode befüllt.
        /// Wird von IsKnownEnumType() für Collection-Element-Erkennung benötigt.
        /// </summary>
        private HashSet<string> _enumTypeNames = new HashSet<string>();

        /// <summary>
        /// Ein Abbruch der Mapper-Generierung wird als Compiler-Fehler gemeldet (nicht still in
        /// die Debug-Ausgabe verschluckt). Sonst bricht der Build erst später mit verwirrenden
        /// Folgefehlern im Infrastructure-Code, weil der generierte Mapper fehlt.
        /// </summary>
        private static readonly DiagnosticDescriptor GeneratorFailed = new DiagnosticDescriptor(
            id: "CQRS030",
            title: "DTO-Mapper-Generierung fehlgeschlagen",
            messageFormat: "DtoMapperSourceGenerator brach ab: {0}",
            category: "Bractor.SourceGeneration",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context)
        {
            // Keine Initialisierung nötig
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Nur im Leitungs-Projekt der Anwendung (docs/07, Rolle Leitung): dort liegen die Domänen-DTOs, hier entsteht ihr Nutzlast-Mapper.
            if (Abstractions.Projektstruktur.Zerlege(context.Compilation.AssemblyName ?? "").Rolle != Abstractions.Projektstruktur.Rolle.Leitung) return;
            var debugInfo = new List<string>();
            debugInfo.Add($"DtoMapperSourceGenerator läuft in Assembly: {context.Compilation.AssemblyName}");
            debugInfo.Add($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            try
            {
                // 1. Nutze den existierenden DomainGraphAnalyzer aus Core.SourceGeneration
                var analyzer = DomainGraphAnalyzer.Create(context.Compilation, useGlobalNamespace: true);
                var graphs = analyzer.AnalyzeMessagePayloads();

                // Signale (StateChangeVia{Event}) ausschließen: sie laufen NUR auf der
                // internen PubSub-Ebene (rohe CLR), nie über die externe gRPC/Proto-Grenze.
                // Ohne Ausschluss würden sie als DomainType.Object Value-Object-Mapper auf
                // nicht existierende StateChangeVia*Dto erzeugen → Compile-Fehler.
                var signalGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IStateChangeSignal");
                var signalNames = new HashSet<string>(signalGraphs.Select(g => g.FullName));
                graphs = graphs.Where(g => !signalNames.Contains(g.FullName)).ToList();

                // Prozess-Maschinerie-Typen (generierte {Prozess}_StarteProzess/Melde…/Prozess-Events,
                // IProzessIntern) aus dem Proto-Mapping ausschließen: rein intern (single-node in-process /
                // Event-Store-JSON), nie über Proto. Für Marten/Replay bleiben sie in der Typ-Registry.
                var prozessGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IProzessIntern");
                var prozessNames = new HashSet<string>(prozessGraphs.Select(g => g.FullName));
                graphs = graphs.Where(g => !prozessNames.Contains(g.FullName)).ToList();

                // NEU: Query-Typen separat laden
                var queryGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IQuery");
                var queryResponseGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IQueryResponse");
                var triggerGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IPipelineTrigger");
                
                // Self-Messages explizit ausschließen — sind Pipeline-intern, kein Proto-Mapping
                var selfMessageGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IPipelineSelfMessage");
                var selfMessageNames = new HashSet<string>(selfMessageGraphs.Select(g => g.FullName));
                triggerGraphs = triggerGraphs.Where(g => !selfMessageNames.Contains(g.FullName)).ToList();
                
                // Aufträge der Katalog-Funktionen (IAuftrag<F>) — reisen zu externen Ausführern (als Nutzlast in ArbeitsAuftrag.auftrag, Cqrs.Leitung/cqrs.proto)
                var auftragGraphs = analyzer.AnalyzeTypesImplementing("Abstractions.IAuftrag");

                // Alle Graphen kombinieren
                var allGraphs = graphs.Concat(queryGraphs).Concat(queryResponseGraphs).Concat(triggerGraphs).Concat(auftragGraphs).ToList();
                
                debugInfo.Add($"Gefundene Type-Graphen: {allGraphs.Count}");

                // 2. Nutze den existierenden TypeAggregator aus Core.SourceGeneration
                var aggregator = new TypeAggregator();
                aggregator.AggregateGraphs(allGraphs);
                
                var objectTypes = aggregator.GetTypesSortedByDepth(DomainType.Object);
                var commandTypes = aggregator.GetTypesSortedByDepth(DomainType.Command);
                var eventTypes = aggregator.GetTypesSortedByDepth(DomainType.Event);
                var queryTypes = aggregator.GetTypesSortedByDepth(DomainType.Query);
                var queryResponseTypes = aggregator.GetTypesSortedByDepth(DomainType.QueryResponse);
                var triggerTypes = aggregator.GetTypesSortedByDepth(DomainType.Trigger);
                var auftragTypes = aggregator.GetTypesSortedByDepth(DomainType.Auftrag);

// FIX: Trigger-Typen können vom Aggregator als Object fehlklassifiziert werden,
// wenn sie als Abhängigkeit eines anderen Typs zuerst entdeckt wurden.
                var triggerFullNames = new HashSet<string>(triggerGraphs.Select(g => g.FullName));
                var misclassifiedTriggers = objectTypes
                    .Where(t => triggerFullNames.Contains(t.Node.FullName))
                    .ToList();
                foreach (var item in misclassifiedTriggers)
                {
                    objectTypes.Remove(item);
                    triggerTypes.Add(item);
                }

                debugInfo.Add($"Commands: {commandTypes.Count}");
                debugInfo.Add($"Events: {eventTypes.Count}");
                debugInfo.Add($"Queries: {queryTypes.Count}");
                debugInfo.Add($"QueryResponses: {queryResponseTypes.Count}");
                debugInfo.Add($"Triggers: {triggerTypes.Count}");
                debugInfo.Add($"Auftraege: {auftragTypes.Count}");
                debugInfo.Add($"Value Objects: {objectTypes.Count}");

                // 3. Generiere Debug Output
                GenerateDebugOutput(context, debugInfo, objectTypes, commandTypes, eventTypes, queryTypes, queryResponseTypes);

                // 4. Generiere echten Mapper Code
                GenerateMapperCode(context, objectTypes, commandTypes, eventTypes, queryTypes, queryResponseTypes, triggerTypes, auftragTypes);
            }
            catch (Exception ex)
            {
                // LAUT melden (Compiler-Fehler), nicht still in die Debug-.g.cs verstecken.
                context.ReportDiagnostic(Diagnostic.Create(GeneratorFailed, Location.None, ex.Message));
                debugInfo.Add($"FEHLER: {ex.Message}");
                debugInfo.Add($"StackTrace: {ex.StackTrace}");
                GenerateDebugOutput(context, debugInfo, null, null, null, null, null);
            }
        }

        private void GenerateMapperCode(
            GeneratorExecutionContext context,
            List<TypeAggregationResult> objectTypes,
            List<TypeAggregationResult> commandTypes,
            List<TypeAggregationResult> eventTypes,
            List<TypeAggregationResult> queryTypes,
            List<TypeAggregationResult> queryResponseTypes,
            List<TypeAggregationResult> triggerTypes,
            List<TypeAggregationResult> auftragTypes)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine($"// Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Globalization;");
            sb.AppendLine("using System.Linq;");
                        sb.AppendLine("using Abstractions;");
            
            // Collect all namespaces
            var namespaces = new HashSet<string>();
            var allTypes = objectTypes
                .Concat(commandTypes)
                .Concat(eventTypes)
                .Concat(queryTypes ?? Enumerable.Empty<TypeAggregationResult>())
                .Concat(queryResponseTypes ?? Enumerable.Empty<TypeAggregationResult>())
                .Concat(triggerTypes ?? Enumerable.Empty<TypeAggregationResult>())
                .Concat(auftragTypes ?? Enumerable.Empty<TypeAggregationResult>());
                
            foreach (var type in allTypes)
            {
                var ns = GetNamespace(type.Node.FullName);
                if (!string.IsNullOrEmpty(ns) && ns != "System" &&  ns != "Abstractions")
                    namespaces.Add(ns);
            }
            
            foreach (var ns in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }
            
            sb.AppendLine();
            sb.AppendLine($"namespace {context.Compilation.AssemblyName}");
            sb.AppendLine("{");
            
            // Build type lookup for determining helper classes
            var typeLookup = BuildTypeLookup(objectTypes, commandTypes, eventTypes, queryTypes, queryResponseTypes, triggerTypes, auftragTypes);
            
            // Enum-Typnamen sammeln für Collection-Element-Erkennung
            _enumTypeNames = new HashSet<string>();
            foreach (var obj in objectTypes.Where(t => t.Node.IsEnum))
            {
                _enumTypeNames.Add(GetSimpleTypeName(obj.Node.FullName));
                _enumTypeNames.Add(obj.Node.FullName);
            }
            
            // 1. Die Nutzlast-Tabelle der Anwendung (Cqrs.Leitung.INutzlastTabelle) — das Framework kennt nur sie.
            GenerateNutzlastKlasse(sb);

            // 2. Static Helper Classes — jede Kategorie Typname ↔ Domänen-Typ über die Nutzlast
            GenerateValueObjectHelpers(sb, objectTypes, typeLookup);
            GenerateStandaloneHelpers(sb, commandTypes, objectTypes, "Command", "ICommand", "ProtoCommandMappingHelpers", typeLookup);
            GenerateStandaloneHelpers(sb, eventTypes, objectTypes, "Event", "IEvent", "ProtoEventMappingHelpers", typeLookup);
            
            // 3. Standalone-Kategorien — generisch (neue Kategorie = ein Eintrag)
            GenerateStandaloneHelpers(sb, queryTypes ?? new List<TypeAggregationResult>(), objectTypes,
                "Query", "IQuery", "ProtoQueryMappingHelpers", typeLookup);
            
            GenerateStandaloneHelpers(sb, queryResponseTypes ?? new List<TypeAggregationResult>(), objectTypes,
                "QueryResponse", "IQueryResponse", "ProtoQueryResponseMappingHelpers", typeLookup);
            
            GenerateStandaloneHelpers(sb, triggerTypes ?? new List<TypeAggregationResult>(), objectTypes,
                "Trigger", "IPipelineTrigger", "ProtoTriggerMappingHelpers", typeLookup);

            GenerateStandaloneHelpers(sb, auftragTypes ?? new List<TypeAggregationResult>(), objectTypes,
                "Auftrag", "IAuftrag", "ProtoAuftragMappingHelpers", typeLookup);
            
            sb.AppendLine("}");
            
            context.AddSource("ProtoMessageMapper.Generated.cs", sb.ToString());
        }

        private void GenerateNutzlastKlasse(StringBuilder sb)
        {
            const string N = "global::Cqrs.Leitung.Nutzlast";
            sb.AppendLine("    /// <summary>Die Nutzlast-Tabelle dieser Anwendung: Domänen-Nachricht ↔ Nutzlast (Typname + Bytes der {Typ}Dto-Message).</summary>");
            sb.AppendLine("    public sealed class GeneratedNutzlast : global::Cqrs.Leitung.INutzlastTabelle");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly GeneratedNutzlast Instanz = new GeneratedNutzlast();");
            foreach (var (art, iface, helper) in new[] { ("Command", "ICommand", "ProtoCommandMappingHelpers"), ("Event", "IEvent", "ProtoEventMappingHelpers"),
                         ("Query", "IQuery", "ProtoQueryMappingHelpers"), ("Antwort", "IQueryResponse", "ProtoQueryResponseMappingHelpers"),
                         ("Trigger", "IPipelineTrigger", "ProtoTriggerMappingHelpers"), ("Auftrag", "IAuftrag", "ProtoAuftragMappingHelpers") })
            {
                sb.AppendLine($"        public {iface} {art}({N} n) => {helper}.MapToDomain(n);");
                sb.AppendLine($"        public {N} Packe{art}({iface} x) => {helper}.MapToDto(x);");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private void GenerateValueObjectHelpers(StringBuilder sb, List<TypeAggregationResult> objectTypes, Dictionary<string, DomainType> typeLookup)
        {
            // Filtere primitive, nullable Typen, nullable Domain-Typen und Enums heraus
            // - Primitive Typen brauchen keine Mapper
            // - Nullable Domain-Typen (z.B. Klassifikation?) nutzen den Mapper des Base-Typs
            // - Enums werden via Cast gemappt
            var filteredTypes = objectTypes
                .Where(t => !IsPrimitiveOrNullableType(t.Node.FullName) 
                         && !IsNullableDomainType(t.Node.FullName)
                         && !t.Node.IsEnum)
                .ToList();
            
            if (!filteredTypes.Any()) return;
            
            sb.AppendLine("    internal static class ProtoValueObjectHelpers");
            sb.AppendLine("    {");
            
            foreach (var objType in filteredTypes)
            {
                var typeName = GetSimpleTypeName(objType.Node.FullName);
                var sanitizedTypeName = NameSanitizer.SanitizeName(typeName);
                var dtoName = sanitizedTypeName + "Dto";
                
                // MapToDomain method
                sb.AppendLine($"        public static {typeName} Map{sanitizedTypeName}({dtoName} dto)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (dto == null) return null;");
                var parameters = objType.Node.ConstructorParameters;
                EmitNeu(sb, typeName, objType.Node, typeLookup);
                sb.AppendLine("        }");
                sb.AppendLine();
                
                // MapToDto: Initializer + AddRange + optionale Post-Zuweisungen (siehe EmitMapToDtoBody).
                sb.AppendLine($"        public static {dtoName} MapToDto({typeName} obj)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (obj == null) return null;");
                EmitMapToDtoBody(sb, parameters, dtoName, "obj", typeLookup);
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        /// <summary>
        /// Generiert Mapping-Helpers für Standalone-PayloadDto-Kategorien.
        /// Identisches Muster für Query, QueryResponse, Trigger — nur Name, Interface, DTO unterscheiden sich.
        /// Command und Event sind Sonderfälle (Envelope-Wrapper) und nutzen eigene Methoden.
        /// </summary>
        private void GenerateStandaloneHelpers(
            StringBuilder sb,
            List<TypeAggregationResult> types,
            List<TypeAggregationResult> objectTypes,
            string categoryName,       // "Query", "QueryResponse", "Trigger", "Auftrag"
            string interfaceName,      // "IQuery", "IQueryResponse", "IPipelineTrigger", "IAuftrag"
            string helperClassName,    // "ProtoQueryMappingHelpers", etc.
            Dictionary<string, DomainType> typeLookup)
        {
            // Die Leitung trägt jede Kategorie als Nutzlast (Typname + Bytes, Cqrs.Leitung/cqrs.proto); ohne Typen kennt der Mapper keinen.
            if (!types.Any())
            {
                sb.AppendLine($"    internal static class {helperClassName}");
                sb.AppendLine("    {");
                sb.AppendLine($"        public static {interfaceName} MapToDomain(global::Cqrs.Leitung.Nutzlast n) => throw new NotSupportedException($\"Unknown {categoryName.ToLower()} type: {{n.Typ}}\");");
                sb.AppendLine($"        public static global::Cqrs.Leitung.Nutzlast MapToDto({interfaceName} payload) => throw new NotSupportedException($\"Unknown {categoryName.ToLower()} type: {{payload.GetType().Name}}\");");
                sb.AppendLine("    }");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"    internal static class {helperClassName}");
            sb.AppendLine("    {");

            GenerateLookupDictionaries(sb, types, categoryName);

            sb.AppendLine($"        public static {interfaceName} MapToDomain(global::Cqrs.Leitung.Nutzlast n)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (!{categoryName}Mappers.TryGetValue(n.Typ, out var mapper))");
            sb.AppendLine($"                throw new NotSupportedException($\"Unknown {categoryName.ToLower()} type: {{n.Typ}}\");");
            sb.AppendLine("            return mapper(n);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static global::Cqrs.Leitung.Nutzlast MapToDto({interfaceName} payload)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (!{categoryName}ReverseMappers.TryGetValue(payload.GetType(), out var mapper))");
            sb.AppendLine($"                throw new NotSupportedException($\"Unknown {categoryName.ToLower()} type: {{payload.GetType().Name}}\");");
            sb.AppendLine("            return mapper(payload);");
            sb.AppendLine("        }");
            sb.AppendLine();

            GenerateIndividualMappers(sb, types, objectTypes, categoryName, typeLookup);

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        /// <summary>
        /// Die Nutzlast-Tabellen einer Kategorie: Typname → (Bytes → Domänen-Typ) und Domänen-Typ → Nutzlast. Der Typname ist der Name des DTO
        /// ohne „Dto“ (Cqrs.Leitung/cqrs.proto, <c>Nutzlast.typ</c>); die Bytes sind die {Typ}Dto-Message des Anwendungs-Protos.
        /// </summary>
        private void GenerateLookupDictionaries(StringBuilder sb, List<TypeAggregationResult> types, string typeName)
        {
            var interfaceName = typeName switch
            {
                "Command" => "ICommand",
                "Event" => "IEvent",
                "Query" => "IQuery",
                "QueryResponse" => "IQueryResponse",
                "Trigger" => "IPipelineTrigger",
                _ => $"I{typeName}"
            };

            sb.AppendLine($"        private static readonly Dictionary<string, Func<global::Cqrs.Leitung.Nutzlast, {interfaceName}>> {typeName}Mappers =");
            sb.AppendLine($"            new Dictionary<string, Func<global::Cqrs.Leitung.Nutzlast, {interfaceName}>>");
            sb.AppendLine("            {");
            foreach (var type in types)
            {
                var sanitizedName = NameSanitizer.NutzlastTyp(type.Node.FullName);
                sb.AppendLine($"                [\"{sanitizedName}\"] = n => Map{sanitizedName}({sanitizedName}Dto.Parser.ParseFrom(n.Daten)),");
            }
            sb.AppendLine("            };");
            sb.AppendLine();

            sb.AppendLine($"        private static readonly Dictionary<Type, Func<{interfaceName}, global::Cqrs.Leitung.Nutzlast>> {typeName}ReverseMappers =");
            sb.AppendLine($"            new Dictionary<Type, Func<{interfaceName}, global::Cqrs.Leitung.Nutzlast>>");
            sb.AppendLine("            {");
            foreach (var type in types)
            {
                var simpleName = GetSimpleTypeName(type.Node.FullName);
                var sanitizedName = NameSanitizer.NutzlastTyp(type.Node.FullName);
                sb.AppendLine($"                [typeof({simpleName})] = p => new global::Cqrs.Leitung.Nutzlast {{ Typ = \"{sanitizedName}\", Daten = global::Google.Protobuf.MessageExtensions.ToByteString(MapToDto(({simpleName})p)) }},");
            }
            sb.AppendLine("            };");
            sb.AppendLine();
        }

        private void GenerateIndividualMappers(StringBuilder sb, List<TypeAggregationResult> types, List<TypeAggregationResult> objectTypes, string typeName, Dictionary<string, DomainType> typeLookup = null)
        {
            foreach (var type in types)
            {
                var simpleName = GetSimpleTypeName(type.Node.FullName);
                var sanitizedName = NameSanitizer.SanitizeName(simpleName);
                var dtoName = sanitizedName + "Dto";
                
                // MapToDomain
                sb.AppendLine($"        private static {simpleName} Map{sanitizedName}({dtoName} dto)");
                sb.AppendLine("        {");
                
                var parameters = type.Node.ConstructorParameters;
                EmitNeu(sb, simpleName, type.Node, typeLookup);
                sb.AppendLine("        }");
                sb.AppendLine();
                
                // MapToDto: Initializer + AddRange + optionale Post-Zuweisungen (siehe EmitMapToDtoBody).
                sb.AppendLine($"        private static {dtoName} MapToDto({simpleName} obj)");
                sb.AppendLine("        {");
                EmitMapToDtoBody(sb, parameters, dtoName, "obj", typeLookup);
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }
        
        /// <summary>
        /// Baut den MapToDto-Rumpf (Domain → DTO), gemeinsam für Value Objects und die Einzel-Mapper:
        /// nicht-nullable Skalare/komplexe Felder im Objekt-Initializer; Collections via AddRange;
        /// nullable Skalare als bedingte Post-Zuweisung (proto3 optional setzt bei JEDER Zuweisung
        /// Presence — null muss das Feld also auslassen, nicht im Initializer setzen).
        /// </summary>
        private void EmitMapToDtoBody(StringBuilder sb, List<TypeNode> parameters, string dtoName, string sourceVar, Dictionary<string, DomainType> typeLookup)
        {
            var collectionParams  = parameters.Where(p => p.IsCollection).ToList();
            var optionalScalars   = parameters.Where(IsOptionalScalar).ToList();
            var initializerParams = parameters.Where(p => !p.IsCollection && !IsOptionalScalar(p)).ToList();
            var needsVar = collectionParams.Any() || optionalScalars.Any();

            sb.AppendLine($"            {(needsVar ? "var dto = " : "return ")}new {dtoName}");
            sb.AppendLine("            {");
            foreach (var param in initializerParams)
            {
                var propName = ToPascalCase(param.Name);
                var mapping = GeneratePropertyMapping(param, sourceVar, propName, typeLookup);
                sb.AppendLine($"                {propName} = {mapping},");
            }
            sb.AppendLine("            };");

            if (needsVar)
            {
                sb.AppendLine();
                foreach (var param in collectionParams)
                {
                    var propName = ToPascalCase(param.Name);
                    sb.AppendLine($"            {GenerateCollectionAddRangeMapping(param, sourceVar, propName, typeLookup)}");
                }
                foreach (var param in optionalScalars)
                {
                    var propName = ToPascalCase(param.Name);
                    sb.AppendLine($"            {GenerateOptionalScalarAssignment(param, sourceVar, propName)}");
                }
                sb.AppendLine();
                sb.AppendLine("            return dto;");
            }
        }

        /// <summary>
        /// Ein Value-Type-Nullable-Proto-Skalar (proto3 optional) — reist als bedingte Post-Zuweisung,
        /// nicht im Initializer. string? (IsRefType) NICHT: das bleibt Sentinel im Initializer.
        /// </summary>
        private static bool IsOptionalScalar(TypeNode p) =>
            !p.IsEnum && !p.IsCollection && ProtoScalarSpecs.TryGet(p.FullName, out var s) && s.IsNullable && !s.IsRefType;

        /// <summary>Bedingte Zuweisung eines nullable Proto-Skalars: nur bei Presence, sonst Feld ungesetzt (= null).</summary>
        private string GenerateOptionalScalarAssignment(TypeNode param, string sourceVar, string propName)
        {
            ProtoScalarSpecs.TryGet(param.FullName, out var s);
            var access = $"{sourceVar}.{propName}";
            return s.IsRefType
                ? $"if ({access} != null) dto.{propName} = {s.Encode(access)};"
                : $"if ({access}.HasValue) dto.{propName} = {s.Encode(access + ".Value")};";
        }

        /// <summary>
        /// Generates AddRange call for collection properties (Protobuf RepeatedField).
        /// </summary>
        private string GenerateCollectionAddRangeMapping(TypeNode param, string sourceVar, string propName, Dictionary<string, DomainType> typeLookup)
        {
            var elementType = param.CollectionElementType;
            
            // Collection von nullable Enums (z.B. IReadOnlyList<Klassifikation?>)
            // In Proto als repeated string → Enum?.ToString(), null → leerer String
            if (IsNullableEnumType(elementType))
            {
                return $"if ({sourceVar}.{propName} != null) dto.{propName}.AddRange({sourceVar}.{propName}.Select(x => x?.ToString() ?? string.Empty));";
            }
            
            // Collection von non-nullable Enums (z.B. IReadOnlyList<Klassifikation>)
            // In Proto als repeated int32 → (int)enum
            if (IsKnownEnumType(elementType))
            {
                return $"if ({sourceVar}.{propName} != null) dto.{propName}.AddRange({sourceVar}.{propName}.Select(x => (int)x));";
            }
            
            // Collection von Guids oder typisierten Ids (IReadOnlyList<Guid>, IReadOnlyList<Id<T>>) → in Proto als repeated string.
            if ((elementType == "System.Guid" || Abstractions.Idvertrag.IstId(elementType)) && ProtoScalarSpecs.TryGet(elementType, out var idSpec))
            {
                return $"if ({sourceVar}.{propName} != null) dto.{propName}.AddRange({sourceVar}.{propName}.Select(x => {idSpec.Encode("x")}));";
            }

            if (TypeMappingHelper.IsProtoScalarType(elementType))
            {
                return $"if ({sourceVar}.{propName} != null) dto.{propName}.AddRange({sourceVar}.{propName});";
            }
            else
            {
                var elementTypeName = GetSimpleTypeName(elementType);

                // Determine correct helper class based on element's DomainType
                var elementDomainType = typeLookup != null ? GetDomainTypeForTypeName(elementTypeName, typeLookup) : DomainType.Object;
                var helperClass = GetHelperClassName(elementDomainType);

                return $"if ({sourceVar}.{propName} != null) dto.{propName}.AddRange({sourceVar}.{propName}.Select(x => {helperClass}.MapToDto(x)));";
            }
        }

        /// <summary>
        /// <c>return new T(…)</c> über den Konstruktor — oder, wenn der Typ keine Konstruktor-Parameter hat (<see cref="TypeNode.ViaInitialisierer"/>),
        /// <c>return new T { P = …, … }</c> über seine init-Properties (auch geerbte <c>required</c>-Glieder).
        /// </summary>
        private void EmitNeu(StringBuilder sb, string typeName, TypeNode node, Dictionary<string, DomainType> typeLookup)
        {
            var parameters = node.ConstructorParameters;
            if (parameters.Count == 0)
            {
                sb.AppendLine($"            return new {typeName}();");
                return;
            }
            sb.AppendLine(node.ViaInitialisierer ? $"            return new {typeName}" : $"            return new {typeName}(");
            if (node.ViaInitialisierer) sb.AppendLine("            {");
            for (int i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];
                var mapping = GenerateParameterMapping(param, "dto", typeLookup);
                sb.Append(node.ViaInitialisierer ? $"                {ToPascalCase(param.Name)} = {mapping}" : $"                {mapping}");
                if (i < parameters.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine(node.ViaInitialisierer ? "            };" : "            );");
        }

        private string GenerateParameterMapping(TypeNode param, string sourceVar, Dictionary<string, DomainType> typeLookup = null)
        {
            var fieldName = ToPascalCase(param.Name);
            
            // Enum-Handling — unterscheide zwischen nullable und non-nullable
            if (param.IsEnum)
            {
                var typeName = GetSimpleTypeName(param.FullName);
                var sanitizedTypeName = NameSanitizer.SanitizeName(typeName);
                
                // Nullable Enum (z.B. Klassifikation?) - in Proto als string dargestellt
                if (param.FullName.EndsWith("?"))
                {
                    // Proto string → C# Nullable Enum
                    // Leerer String = null, sonst Enum.Parse
                    var baseEnumType = sanitizedTypeName; // ohne ?
                    return $"string.IsNullOrEmpty({sourceVar}.{fieldName}) ? ({baseEnumType}?)null : Enum.Parse<{baseEnumType}>({sourceVar}.{fieldName})";
                }
                else
                {
                    // Non-nullable Enum - in Proto als int32 dargestellt
                    // Proto int32 → C# Enum via Cast
                    return $"({sanitizedTypeName}){sourceVar}.{fieldName}";
                }
            }
            
            // ── Skalare aus der EINEN Quelle. Nullable = proto3 optional → über Has…-Presence
            //    dekodieren (kein Sentinel mehr: null und Grenzwert wie 0/Epoche sind unterscheidbar). ──
            if (ProtoScalarSpecs.TryGet(param.FullName, out var scalar))
            {
                // Value-Type-Nullables reisen als proto3 optional → über Has…-Presence dekodieren.
                // string? (IsRefType) behält die Sentinel-Kodierung (leer=null) via scalar.Decode.
                if (scalar.IsNullable && !scalar.IsRefType)
                    return $"{sourceVar}.Has{fieldName} ? {scalar.Decode($"{sourceVar}.{fieldName}")} : ({scalar.NullableCSharp})null";
                return scalar.Decode($"{sourceVar}.{fieldName}");
            }

            // Superset-Primitive (short/byte/char/object/TimeSpan) + Nicht-Spec-Nullables:
            // Bestandsverhalten — direkt übernehmen.
            if (IsPrimitiveOrNullableType(param.FullName))
                return $"{sourceVar}.{fieldName}";
            
            // NEU: Handle nullable Domain-Typen (z.B. Klassifikation?)
            // Diese nutzen den Mapper des Base-Typs
            if (IsNullableDomainType(param.FullName))
            {
                var baseTypeName = GetNullableBaseType(param.FullName);
                var simpleBaseType = GetSimpleTypeName(baseTypeName);
                var sanitizedBaseType = NameSanitizer.SanitizeName(simpleBaseType);
                
                // Determine correct helper class based on DomainType
                var domainType = typeLookup != null ? GetDomainTypeForTypeName(simpleBaseType, typeLookup) : DomainType.Object;
                var helperClass = GetHelperClassName(domainType);
                
                return $"{helperClass}.Map{sanitizedBaseType}({sourceVar}.{fieldName})";
            }
            
            // Handle complex types (nur wenn NICHT primitiv/nullable und NICHT enum)
            if (!TypeMappingHelper.IsProtoScalarType(param.FullName) && !param.IsCollection && !param.IsDictionary && !param.IsEnum)
            {
                var typeName = GetSimpleTypeName(param.FullName);
                var sanitizedTypeName = NameSanitizer.SanitizeName(typeName);
                
                // Determine correct helper class based on DomainType
                var domainType = typeLookup != null ? GetDomainTypeForTypeName(typeName, typeLookup) : DomainType.Object;
                var helperClass = GetHelperClassName(domainType);
                
                return $"{helperClass}.Map{sanitizedTypeName}({sourceVar}.{fieldName})";
            }
            
            // Handle collections
            if (param.IsCollection)
            {
                var elementType = param.CollectionElementType;
                
                // NEU: Collection von nullable Enums (z.B. IReadOnlyList<Klassifikation?>)
                // In Proto als repeated string dargestellt
                if (IsNullableEnumType(elementType))
                {
                    var baseEnumType = GetSimpleTypeName(GetNullableBaseType(elementType));
                    var sanitizedEnumType = NameSanitizer.SanitizeName(baseEnumType);
                    // string → Klassifikation?, leerer String = null
                    return $"{sourceVar}.{fieldName}?.Select(x => string.IsNullOrEmpty(x) ? ({sanitizedEnumType}?)null : Enum.Parse<{sanitizedEnumType}>(x)).ToList()";
                }
                
                // Collection von non-nullable Enums (z.B. IReadOnlyList<Klassifikation>)
                // In Proto als repeated int32 dargestellt → Cast
                if (IsKnownEnumType(elementType))
                {
                    var enumTypeName = GetSimpleTypeName(elementType);
                    var sanitizedEnumType = NameSanitizer.SanitizeName(enumTypeName);
                    return $"{sourceVar}.{fieldName}?.Select(x => ({sanitizedEnumType})x).ToList()";
                }
                
                // Collection von Guids oder typisierten Ids (repeated string in Proto) → je Element zurück.
                if ((elementType == "System.Guid" || Abstractions.Idvertrag.IstId(elementType)) && ProtoScalarSpecs.TryGet(elementType, out var idSpec))
                {
                    return $"{sourceVar}.{fieldName}?.Select(x => {idSpec.Decode("x")}).ToList()";
                }

                if (TypeMappingHelper.IsProtoScalarType(elementType) || IsPrimitiveOrNullableType(elementType))
                {
                    return $"{sourceVar}.{fieldName}?.ToList()";
                }
                else
                {
                    var elementTypeName = GetSimpleTypeName(elementType);
                    var sanitizedElementType = NameSanitizer.SanitizeName(elementTypeName);
                    
                    // Determine correct helper class based on element's DomainType
                    var elementDomainType = typeLookup != null ? GetDomainTypeForTypeName(elementTypeName, typeLookup) : DomainType.Object;
                    var helperClass = GetHelperClassName(elementDomainType);
                    
                    return $"{sourceVar}.{fieldName}?.Select(x => {helperClass}.Map{sanitizedElementType}(x)).ToList()";
                }
            }
            
            // Scalar types
            return $"{sourceVar}.{fieldName}";
        }

        private string GeneratePropertyMapping(TypeNode param, string sourceVar, string propName, Dictionary<string, DomainType> typeLookup = null)
        {
            // Enum-Handling — unterscheide zwischen nullable und non-nullable
            if (param.IsEnum)
            {
                var typeName = GetSimpleTypeName(param.FullName);
                var sanitizedTypeName = NameSanitizer.SanitizeName(typeName);
                
                // Nullable Enum (z.B. Klassifikation?) - in Proto als string dargestellt
                if (param.FullName.EndsWith("?"))
                {
                    // C# Nullable Enum → Proto string
                    // null = leerer String, sonst Enum.ToString()
                    return $"{sourceVar}.{propName}?.ToString() ?? string.Empty";
                }
                else
                {
                    // Non-nullable Enum - in Proto als int32 dargestellt
                    // C# Enum → Proto int32 via Cast
                    return $"(int){sourceVar}.{propName}";
                }
            }
            
            // ── Skalare aus der EINEN Quelle: genau eine Encode-Regel je Typ (ProtoScalarSpec). ──
            if (ProtoScalarSpecs.TryGet(param.FullName, out var scalar))
                return scalar.Encode($"{sourceVar}.{propName}");

            // Superset-Primitive + Nicht-Spec-Nullables: Bestandsverhalten.
            if (IsPrimitiveOrNullableType(param.FullName))
            {
                if (param.FullName.EndsWith("?")
                    || param.FullName.StartsWith("System.Nullable<")
                    || param.FullName.StartsWith("Nullable<"))
                    return $"{sourceVar}.{propName} ?? default";

                // Non-nullable primitive → direkt
                return $"{sourceVar}.{propName}";
            }
            
            // NEU: Handle nullable Domain-Typen (z.B. Klassifikation?)
            // Diese nutzen den Mapper des Base-Typs
            if (IsNullableDomainType(param.FullName))
            {
                var baseTypeName = GetNullableBaseType(param.FullName);
                var simpleBaseType = GetSimpleTypeName(baseTypeName);
                
                // Determine correct helper class based on DomainType
                var domainType = typeLookup != null ? GetDomainTypeForTypeName(simpleBaseType, typeLookup) : DomainType.Object;
                var helperClass = GetHelperClassName(domainType);
                
                return $"{helperClass}.MapToDto({sourceVar}.{propName})";
            }
            
            // Handle complex types (nur wenn NICHT primitiv/nullable und NICHT enum)
            if (!TypeMappingHelper.IsProtoScalarType(param.FullName) && !param.IsCollection && !param.IsDictionary && !param.IsEnum)
            {
                var typeName = GetSimpleTypeName(param.FullName);
                
                // Determine correct helper class based on DomainType
                var domainType = typeLookup != null ? GetDomainTypeForTypeName(typeName, typeLookup) : DomainType.Object;
                var helperClass = GetHelperClassName(domainType);
                
                return $"{helperClass}.MapToDto({sourceVar}.{propName})";
            }
            
            // Handle collections
            if (param.IsCollection)
            {
                var elementType = param.CollectionElementType;
                
                // NEU: Collection von nullable Enums (z.B. IReadOnlyList<Klassifikation?>)
                // In Proto als repeated string dargestellt
                if (IsNullableEnumType(elementType))
                {
                    // Klassifikation? → string, null = leerer String
                    return $"{sourceVar}.{propName}?.Select(x => x?.ToString() ?? string.Empty).ToList()";
                }
                
                // Collection von non-nullable Enums (z.B. IReadOnlyList<Klassifikation>)
                // In Proto als repeated int32 → (int)enum
                if (IsKnownEnumType(elementType))
                {
                    return $"{sourceVar}.{propName}?.Select(x => (int)x).ToList()";
                }
                
                if (TypeMappingHelper.IsProtoScalarType(elementType) || IsPrimitiveOrNullableType(elementType))
                {
                    return $"{sourceVar}.{propName}?.ToList()";
                }
                else
                {
                    var elementTypeName = GetSimpleTypeName(elementType);
                    
                    // Determine correct helper class based on element's DomainType
                    var elementDomainType = typeLookup != null ? GetDomainTypeForTypeName(elementTypeName, typeLookup) : DomainType.Object;
                    var helperClass = GetHelperClassName(elementDomainType);
                    
                    return $"{sourceVar}.{propName}?.Select(x => {helperClass}.MapToDto(x)).ToList()";
                }
            }
            
            // Scalar types
            return $"{sourceVar}.{propName}";
        }

        private string GetSimpleTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;

            var withoutGenerics = fullName.Split('<')[0];
            var parts = withoutGenerics.Split('.');
            return parts[parts.Length - 1];
        }

        private string GetNamespace(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            
            var lastDot = fullName.LastIndexOf('.');
            if (lastDot > 0)
                return fullName.Substring(0, lastDot);
            
            return "";
        }

        /// <summary>
        /// Builds a lookup dictionary from type name to DomainType for all known types.
        /// </summary>
        private Dictionary<string, DomainType> BuildTypeLookup(
            List<TypeAggregationResult> objectTypes,
            List<TypeAggregationResult> commandTypes,
            List<TypeAggregationResult> eventTypes,
            List<TypeAggregationResult> queryTypes,
            List<TypeAggregationResult> queryResponseTypes,
            List<TypeAggregationResult> triggerTypes,
            List<TypeAggregationResult> auftragTypes = null)
        {
            var lookup = new Dictionary<string, DomainType>();
            
            void AddTypes(List<TypeAggregationResult> types, DomainType domainType)
            {
                if (types == null) return;
                foreach (var type in types)
                {
                    var simpleName = GetSimpleTypeName(type.Node.FullName);
                    lookup[simpleName] = domainType;
                    lookup[type.Node.FullName] = domainType;
                }
            }
            
            AddTypes(objectTypes, DomainType.Object);
            AddTypes(commandTypes, DomainType.Command);
            AddTypes(eventTypes, DomainType.Event);
            AddTypes(queryTypes, DomainType.Query);
            AddTypes(queryResponseTypes, DomainType.QueryResponse);
            AddTypes(triggerTypes, DomainType.Trigger);
            AddTypes(auftragTypes, DomainType.Auftrag);
            
            return lookup;
        }

        /// <summary>
        /// Gets the helper class name for a given DomainType.
        /// </summary>
        private string GetHelperClassName(DomainType domainType)
        {
            return domainType switch
            {
                DomainType.Object => "ProtoValueObjectHelpers",
                DomainType.Command => "ProtoCommandMappingHelpers",
                DomainType.Event => "ProtoEventMappingHelpers",
                DomainType.Query => "ProtoQueryMappingHelpers",
                DomainType.QueryResponse => "ProtoQueryResponseMappingHelpers",
                DomainType.Trigger => "ProtoTriggerMappingHelpers",
                DomainType.Auftrag => "ProtoAuftragMappingHelpers",
                _ => "ProtoValueObjectHelpers"
            };
        }

        /// <summary>
        /// Gets the DomainType for a type name using the lookup, defaulting to Object.
        /// </summary>
        private DomainType GetDomainTypeForTypeName(string typeName, Dictionary<string, DomainType> typeLookup)
        {
            if (string.IsNullOrEmpty(typeName)) return DomainType.Object;
            
            var simpleName = GetSimpleTypeName(typeName);
            
            if (typeLookup.TryGetValue(simpleName, out var domainType))
                return domainType;
            
            if (typeLookup.TryGetValue(typeName, out domainType))
                return domainType;
            
            return DomainType.Object;
        }

        /// <summary>
        /// Prüft ob ein Typ ein nullable DateTime/DateTimeOffset ist.
        /// Diese brauchen spezielle Konvertierung: int64 ↔ DateTimeOffset? mit 0 = null.
        /// </summary>
        private bool IsNullableDateTimeType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            
            var nullableDateTimeTypes = new HashSet<string>
            {
                "System.DateTime?",
                "System.DateTimeOffset?",
                "DateTime?",
                "DateTimeOffset?",
                "System.Nullable<System.DateTime>",
                "System.Nullable<System.DateTimeOffset>",
                "Nullable<DateTime>",
                "Nullable<DateTimeOffset>",
            };
            
            return nullableDateTimeTypes.Contains(typeName);
        }

        /// <summary>
        /// Prüft ob ein Typ primitiv oder ein nullable primitiver Typ ist und daher KEINEN Mapper braucht.
        /// Diese Typen sollten nicht als Value Objects behandelt werden!
        /// 
        /// WICHTIG: Diese Methode gibt NUR für primitive Typen true zurück.
        /// Nullable Domain-Typen (z.B. Klassifikation?) brauchen den Mapper des Base-Typs!
        /// </summary>
        private bool IsPrimitiveOrNullableType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            
            // Nullable<T> Pattern (System.Nullable<X>)
            if (typeName.StartsWith("System.Nullable<") || typeName.StartsWith("Nullable<"))
            {
                // Extrahiere den inneren Typ und prüfe ob dieser primitiv ist
                var innerType = typeName
                    .Replace("System.Nullable<", "")
                    .Replace("Nullable<", "")
                    .TrimEnd('>');
                return IsPrimitiveOrNullableType(innerType);
            }
            
            // Bekannte primitive/system Typen
            var primitiveTypes = new HashSet<string>
            {
                // Non-nullable
                "string", "System.String",
                "int", "System.Int32",
                "long", "System.Int64",
                "short", "System.Int16",
                "byte", "System.Byte",
                "bool", "System.Boolean",
                "double", "System.Double",
                "float", "System.Single",
                "decimal", "System.Decimal",
                "char", "System.Char",
                "object", "System.Object",
                "System.Guid",
                "System.DateTime",
                "System.DateTimeOffset",
                "System.TimeSpan",
            };
            
            // Direkter Match (non-nullable); eine typisierte Id ist ein Skalar wie Guid.
            if (primitiveTypes.Contains(typeName) || Abstractions.Idvertrag.IstId(typeName))
                return true;
            
            // Endet mit ? (nullable) - prüfe ob der Base-Typ primitiv ist
            if (typeName.EndsWith("?"))
            {
                var baseType = typeName.TrimEnd('?');
                return primitiveTypes.Contains(baseType);
            }
            
            return false;
        }
        
        /// <summary>
        /// Prüft ob ein Typ ein nullable Domain-Typ ist (z.B. SomeValueObject?).
        /// Diese brauchen den Mapper des Base-Typs, sollen aber nicht als eigenständige
        /// Typen mit eigenem Mapper behandelt werden.
        /// 
        /// WICHTIG: Nullable Enums (z.B. Klassifikation?) sind KEINE nullable Domain-Typen!
        /// Sie haben IsEnum = true und werden via string-Parsing konvertiert.
        /// </summary>
        private bool IsNullableDomainType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            
            // Prüfe ob es ein nullable Typ ist (endet auf ? oder ist Nullable<T>)
            bool isNullable = typeName.EndsWith("?") || 
                              typeName.StartsWith("System.Nullable<") || 
                              typeName.StartsWith("Nullable<");
            
            if (!isNullable)
                return false;
            
            // Ist NICHT primitiv und NICHT ein Enum
            if (!IsPrimitiveOrNullableType(typeName) && !IsNullableEnumType(typeName))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Prüft ob ein Typ ein nullable Enum ist (z.B. Klassifikation? oder Nullable&lt;Klassifikation&gt;).
        /// Diese werden in Proto als string dargestellt und brauchen spezielle Konvertierung.
        /// </summary>
        private bool IsNullableEnumType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;

            // Nur nullable Typen (Type? bzw. Nullable<Type>) kommen infrage.
            bool isNullable = typeName.EndsWith("?")
                || typeName.StartsWith("System.Nullable<")
                || typeName.StartsWith("Nullable<");
            if (!isNullable)
                return false;

            // Base-Typ gegen die DYNAMISCH aus dem Domain-Graph gesammelten Enums prüfen
            // (objectTypes mit IsEnum → _enumTypeNames, siehe GenerateMapperCode). KEINE
            // Hardcoded-Liste mehr: ein neuer Enum in einer beliebigen Domäne wird automatisch
            // erkannt statt still als Nicht-Enum fehlgemappt.
            var baseType = GetNullableBaseType(typeName);
            var simpleBaseType = GetSimpleTypeName(baseType);
            return _enumTypeNames.Contains(baseType) || _enumTypeNames.Contains(simpleBaseType);
        }
        
        /// <summary>
        /// Prüft ob ein Typname ein bekannter non-nullable Enum-Typ ist.
        /// Nutzt das dynamisch gesammelte _enumTypeNames Set statt einer Hardcoded-Liste.
        /// Wird für Collection-Element-Typen benötigt, wo nur ein String vorliegt
        /// (z.B. "Domain.ImagePair.Klassifikation" als Element von IReadOnlyList&lt;Klassifikation&gt;).
        /// </summary>
        private bool IsKnownEnumType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            
            // Nullable Typen sind hier NICHT gemeint — dafür gibt es IsNullableEnumType
            if (typeName.EndsWith("?") || typeName.StartsWith("System.Nullable<") || typeName.StartsWith("Nullable<"))
                return false;
            
            var simple = GetSimpleTypeName(typeName);
            return _enumTypeNames.Contains(typeName) || _enumTypeNames.Contains(simple);
        }
        
        /// <summary>
        /// Extrahiert den Base-Typ aus einem nullable Typ.
        /// Beispiel: "Klassifikation?" → "Klassifikation"
        /// </summary>
        private string GetNullableBaseType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeName;
            
            if (typeName.EndsWith("?"))
                return typeName.TrimEnd('?');
            
            
            
            
            // Nullable<T> Pattern
            if (typeName.StartsWith("System.Nullable<") || typeName.StartsWith("Nullable<"))
            {
                return typeName
                    .Replace("System.Nullable<", "")
                    .Replace("Nullable<", "")
                    .TrimEnd('>');
            }
            
            return typeName;
        }

        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            var parts = input.Split('_');
            var result = new StringBuilder();
            
            foreach (var part in parts)
            {
                if (part.Length > 0)
                {
                    result.Append(char.ToUpper(part[0]));
                    if (part.Length > 1)
                        result.Append(part.Substring(1));
                }
            }
            
            return result.ToString();
        }

        private void GenerateDebugOutput(
            GeneratorExecutionContext context, 
            List<string> debugInfo,
            List<TypeAggregationResult> objectTypes,
            List<TypeAggregationResult> commandTypes,
            List<TypeAggregationResult> eventTypes,
            List<TypeAggregationResult> queryTypes,
            List<TypeAggregationResult> queryResponseTypes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// Debug output from DtoMapperSourceGenerator");
            sb.AppendLine();
            sb.AppendLine("/*");
            
            foreach (var line in debugInfo)
            {
                sb.AppendLine($"  {line}");
            }
            
            if (commandTypes != null)
            {
                sb.AppendLine();
                sb.AppendLine("  Commands:");
                foreach (var cmd in commandTypes)
                {
                    sb.AppendLine($"    - {cmd.TypeName}");
                }
            }
            
            if (eventTypes != null)
            {
                sb.AppendLine();
                sb.AppendLine("  Events:");
                foreach (var evt in eventTypes)
                {
                    sb.AppendLine($"    - {evt.TypeName}");
                }
            }
            
            if (queryTypes != null)
            {
                sb.AppendLine();
                sb.AppendLine("  Queries:");
                foreach (var q in queryTypes)
                {
                    sb.AppendLine($"    - {q.TypeName}");
                }
            }
            
            if (queryResponseTypes != null)
            {
                sb.AppendLine();
                sb.AppendLine("  QueryResponses:");
                foreach (var qr in queryResponseTypes)
                {
                    sb.AppendLine($"    - {qr.TypeName}");
                }
            }
            
            if (objectTypes != null)
            {
                sb.AppendLine();
                sb.AppendLine("  Value Objects:");
                foreach (var obj in objectTypes)
                {
                    sb.AppendLine($"    - {obj.TypeName}");
                }
            }
            
            sb.AppendLine("*/");
            
            context.AddSource("DtoMapper.Debug.g.cs", sb.ToString());
        }
    }
}