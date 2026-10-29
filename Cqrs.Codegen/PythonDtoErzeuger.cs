using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Cqrs.Codegen;

/// <summary>
/// Die betterproto-DTOs des Python-Workers (<c>domain_client/generated/__init__.py</c>) aus dem eben geschriebenen <c>domain.proto</c> — im
/// Prepass, damit eine neue Domänen-Nachricht ohne manuellen Schritt auch in Python ankommt (docs/13 FB-20). Eine Quelle: dasselbe Proto,
/// das protoc für C# liest. Ob neu erzeugt wird, entscheidet der Hash des Protos (<c>generated/.proto_hash</c>, sha256 des Datei-Inhalts):
/// stimmt er, braucht der Build kein Python — ein frischer Checkout (Docker, Gate) baut damit ohne Interpreter. Nur eine echte Änderung des
/// Protos ruft protoc über Python; fehlt dann der Interpreter oder betterproto, scheitert der Build laut (nie still veraltete DTOs).
/// </summary>
public static class PythonDtoErzeuger
{
    /// <summary>Der Interpreter mit <c>grpc_tools</c> und betterproto: <c>CQRS_PYTHON</c>, sonst der der Server- und Entwickler-Maschinen.</summary>
    public static string Interpreter =>
        Environment.GetEnvironmentVariable("CQRS_PYTHON") is { Length: > 0 } e ? e : "/opt/miniconda3/envs/gpu_env/bin/python";

    /// <summary>Erzeugt die DTOs, wenn der Hash des Protos nicht zum Generat passt. Rückgabe: Fehlermeldung oder <c>null</c>.</summary>
    public static string? Erzeuge(string protoDatei, string generiertVerzeichnis, string dtoDatei, string hashDatei)
    {
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(protoDatei))).ToLowerInvariant();
        if (File.Exists(dtoDatei) && File.Exists(hashDatei) && File.ReadAllText(hashDatei).Trim() == hash)
        {
            Console.WriteLine($"·  Python-DTOs aktuell ({hash[..16]}…)");
            return null;
        }

        // Ohne package-Zeile erzeugen: betterproto legte sonst je Paket ein Unterverzeichnis an (mit kaputten Querverweisen). Ohne package
        // landet alles flach in __init__.py.
        var temp = Directory.CreateTempSubdirectory("cqrs-codegen-proto-");
        try
        {
            var proto = Path.Combine(temp.FullName, Path.GetFileName(protoDatei));
            File.WriteAllText(proto, Regex.Replace(File.ReadAllText(protoDatei), "^package .*;", "", RegexOptions.Multiline));
            Directory.CreateDirectory(generiertVerzeichnis);

            var psi = new ProcessStartInfo(Interpreter) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in new[] { "-m", "grpc_tools.protoc", "-I", temp.FullName, $"--python_betterproto_out={generiertVerzeichnis}", proto })
                psi.ArgumentList.Add(a);
            // protoc sucht das Plugin (protoc-gen-python_betterproto) im PATH: es liegt neben dem Interpreter.
            if (Path.GetDirectoryName(Interpreter) is { Length: > 0 } bin)
                psi.Environment["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            Process p;
            try { p = Process.Start(psi)!; }
            catch (Win32Exception ex)
            {
                return $"Python für die DTOs fehlt ({Interpreter}: {ex.Message}) — CQRS_PYTHON auf einen Interpreter mit grpcio-tools und betterproto setzen";
            }
            using (p)
            {
                var aus = p.StandardOutput.ReadToEndAsync();
                var fehler = p.StandardError.ReadToEndAsync();
                p.WaitForExit();
                if (p.ExitCode != 0 || !File.Exists(dtoDatei))
                    return $"protoc/betterproto über {Interpreter} gescheitert (Exit {p.ExitCode}): {(fehler.Result + aus.Result).Trim()}";
            }
        }
        finally { temp.Delete(recursive: true); }

        File.WriteAllText(hashDatei, hash + "\n");
        var typen = File.ReadLines(dtoDatei).Count(z => z.StartsWith("class ", StringComparison.Ordinal));
        Console.WriteLine($"✅ {dtoDatei} ({typen} Typen, Hash {hash[..16]}…)");
        return null;
    }
}
