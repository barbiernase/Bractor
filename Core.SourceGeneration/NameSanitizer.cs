using System.Text;

namespace Core.SourceGeneration
{
    public static class NameSanitizer
    {
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