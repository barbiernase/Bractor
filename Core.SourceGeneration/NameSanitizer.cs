using System.Text;

namespace Core.SourceGeneration
{
    public static class NameSanitizer
    {
        /// <summary>Die Endung der Proto-Message eines Domänen-Typs: <c>{Typ}Dto</c>.</summary>
        public const string DtoSuffix = "Dto";

        /// <summary>
        /// Die Namensregel der Leitung (Cqrs.Leitung/cqrs.proto, <c>Nutzlast.typ</c>) — EINE Quelle für den C#-Nutzlast-Mapper
        /// (<c>DtoMapperGenerator</c>), das Anwendungs-Proto (<c>FileGenerator</c>) und die Python-Tabelle (<c>Cqrs.Codegen</c>):
        /// der einfache Typname (ohne Namespace und Generika), bereinigt. Die Proto-Message heißt <see cref="DtoName"/>.
        /// </summary>
        public static string NutzlastTyp(string typName)
        {
            if (string.IsNullOrEmpty(typName)) return typName;
            var teile = typName.Split('<')[0].Split('.');
            return SanitizeName(teile[teile.Length - 1]);
        }

        /// <summary>Der Name der Proto-Message eines Domänen-Typs: <see cref="NutzlastTyp"/> + <see cref="DtoSuffix"/>.</summary>
        public static string DtoName(string typName) => NutzlastTyp(typName) + DtoSuffix;

        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
                
            return name
                // WICHTIG: Entferne nullable Suffix - das ? ist in C# Identifier ungültig!
                .TrimEnd('?')
                // Echte Unicode-Umlaute (encoding-sicher via Escapes)
                .Replace("\u00e4", "ae").Replace("\u00f6", "oe").Replace("\u00fc", "ue")
                .Replace("\u00c4", "Ae").Replace("\u00d6", "Oe").Replace("\u00dc", "Ue")
                .Replace("\u00df", "ss");
        }

        public static string ToSnakeCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;
            
            var sanitizedText = SanitizeName(text);
            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(sanitizedText[0]));
            
            for (int i = 1; i < sanitizedText.Length; ++i)
            {
                char c = sanitizedText[i];
                if (char.IsUpper(c))
                {
                    sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            
            return sb.ToString();
        }
        
        public static string ToPascalCase(string snakeCase)
        {
            if (string.IsNullOrEmpty(snakeCase))
                return snakeCase;
                
            var parts = snakeCase.Split('_');
            var sb = new StringBuilder();
            
            foreach (var part in parts)
            {
                if (part.Length > 0)
                {
                    sb.Append(char.ToUpperInvariant(part[0]));
                    if (part.Length > 1)
                        sb.Append(part.Substring(1).ToLowerInvariant());
                }
            }
            
            return sb.ToString();
        }
    }
}