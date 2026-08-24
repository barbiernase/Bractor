using System;
using System.Collections.Generic;

namespace Core.SourceGeneration
{
    /// <summary>
    /// Die EINE Quelle der C#↔Proto-Skalarabbildung: je C#-Typ der Proto-Feldtyp plus die
    /// WERT-EBENEN Encode/Decode-Ausdrücke. Ersetzt die zuvor DREIFACH duplizierten Regeln
    /// (<c>FileGenerator._typeMapping</c>, <c>TypeMappingHelper.ProtoTypeMapping</c> und die
    /// Inline-Zweige im <c>DtoMapperGenerator</c>). Eine Änderung liegt jetzt an genau EINER Stelle.
    ///
    /// <see cref="Encode"/>/<see cref="Decode"/> arbeiten auf dem NICHT-NULL-Wert (bei nullable also
    /// dem bereits ausgepackten <c>.Value</c>). Die Null-/Presence-Behandlung macht der Aufrufer:
    /// nullable Skalare reisen seit Phase B als proto3 <c>optional</c> (native Presence, kein
    /// Sentinel mehr) — Encode wird zur bedingten Zuweisung, Decode zum <c>Has…</c>-Ternär.
    ///
    /// ns2.0-Zwang: Diese Datei wird per Glob in <c>Infrastructure.SourceGeneration</c>
    /// (netstandard2.0) source-linked — daher KEIN <c>record</c>/<c>init</c>.
    /// </summary>
    public sealed class ProtoScalarSpec
    {
        /// <summary>Der BARE Proto-Skalartyp: "int32"/"int64"/"bool"/"double"/"float"/"string".</summary>
        public string ProtoType { get; }
        /// <summary>Feld-Ebene nullable? Dann wird das Proto-Feld als <c>optional</c> emittiert.</summary>
        public bool IsNullable { get; }
        /// <summary>Referenztyp (nur <c>string?</c>): Presence via <c>!= null</c> statt <c>.HasValue</c>/<c>.Value</c>.</summary>
        public bool IsRefType { get; }
        /// <summary>C#-Nullable-Typname für die <c>(T?)null</c>-Rückgabe im Decode-Ternär (nur nullable).</summary>
        public string NullableCSharp { get; }
        /// <summary>Nicht-null-Wert (Domain) → Proto-Wert.</summary>
        public Func<string, string> Encode { get; }
        /// <summary>Präsenter Proto-Wert → Domain-Wert.</summary>
        public Func<string, string> Decode { get; }

        public ProtoScalarSpec(string protoType, bool isNullable, bool isRefType, string nullableCSharp,
                               Func<string, string> encode, Func<string, string> decode)
        {
            ProtoType = protoType;
            IsNullable = isNullable;
            IsRefType = isRefType;
            NullableCSharp = nullableCSharp;
            Encode = encode;
            Decode = decode;
        }
    }

    /// <summary>Registry aller Skalar-Specs, per C#-Typname (voll qualifiziert und Alias).</summary>
    public static class ProtoScalarSpecs
    {
        private static readonly Func<string, string> Identity = x => x;

        public static readonly Dictionary<string, ProtoScalarSpec> ByCSharpType = Build();

        public static bool TryGet(string csharpType, out ProtoScalarSpec spec) =>
            ByCSharpType.TryGetValue((csharpType ?? "").Trim(), out spec);

        private static Dictionary<string, ProtoScalarSpec> Build()
        {
            var d = new Dictionary<string, ProtoScalarSpec>();

            // Registriert eine Typ-Familie: der Basistyp (non-null) und seine Nullable-Variante teilen
            // sich Proto-Typ und WERT-Encode/Decode; sie unterscheiden sich nur in Presence-Behandlung.
            void Family(string protoType, bool isRef, string nullableCSharp,
                        Func<string, string> encode, Func<string, string> decode,
                        string[] nonNullKeys, string[] nullableKeys)
            {
                var nn = new ProtoScalarSpec(protoType, false, isRef, null, encode, decode);
                foreach (var k in nonNullKeys) d[k] = nn;
                var nu = new ProtoScalarSpec(protoType, true, isRef, nullableCSharp, encode, decode);
                foreach (var k in nullableKeys) d[k] = nu;
            }

            // string: non-null = Identity. string? ist ein REFERENCE-Type-Nullable — dessen
            // Nullability ist zur Reflection-Zeit NICHT sichtbar (der Prepass sieht nur System.String).
            // Damit Prepass-Proto und Roslyn-Mapper übereinstimmen, bleibt string? proto `string`
            // (KEIN optional) mit der Sentinel-Kodierung leer=null. IsRefType markiert genau das.
            d["System.String"] = new ProtoScalarSpec("string", false, false, null, Identity, Identity);
            d["string"] = d["System.String"];
            var stringNullable = new ProtoScalarSpec("string", true, true, "string",
                x => $"{x} ?? string.Empty",
                x => $"string.IsNullOrEmpty({x}) ? null : {x}");
            d["string?"] = stringNullable;
            d["System.String?"] = stringNullable;
            Family("int32", false, "int?", Identity, Identity,
                new[] { "System.Int32", "int" },
                new[] { "int?", "System.Int32?", "System.Nullable<System.Int32>" });
            Family("int64", false, "long?", Identity, Identity,
                new[] { "System.Int64", "long" },
                new[] { "long?", "System.Int64?", "System.Nullable<System.Int64>" });
            Family("bool", false, "bool?", Identity, Identity,
                new[] { "System.Boolean", "bool" },
                new[] { "bool?", "System.Boolean?", "System.Nullable<System.Boolean>" });
            Family("double", false, "double?", Identity, Identity,
                new[] { "System.Double", "double" },
                new[] { "double?", "System.Double?", "System.Nullable<System.Double>" });
            Family("float", false, "float?", Identity, Identity,
                new[] { "System.Single", "float" },
                new[] { "float?", "System.Single?", "System.Nullable<System.Single>" });
            Family("string", false, "Guid?",
                x => $"{x}.ToString()",
                x => $"Guid.Parse({x})",
                new[] { "System.Guid" },
                new[] { "System.Guid?", "System.Nullable<System.Guid>" });
            Family("string", false, "decimal?",
                x => $"{x}.ToString(CultureInfo.InvariantCulture)",
                x => $"decimal.Parse({x}, CultureInfo.InvariantCulture)",
                new[] { "System.Decimal", "decimal" },
                new[] { "decimal?", "System.Decimal?", "System.Nullable<System.Decimal>" });
            Family("int64", false, "DateTimeOffset?",
                x => $"{x}.ToUnixTimeMilliseconds()",
                x => $"DateTimeOffset.FromUnixTimeMilliseconds({x})",
                new[] { "System.DateTimeOffset" },
                new[] { "System.DateTimeOffset?", "System.Nullable<System.DateTimeOffset>" });
            Family("int64", false, "DateTime?",
                x => $"{x}.ToUnixTimeMilliseconds()",
                x => $"DateTimeOffset.FromUnixTimeMilliseconds({x})",
                new[] { "System.DateTime" },
                new[] { "System.DateTime?", "System.Nullable<System.DateTime>" });

            return d;
        }
    }
}
