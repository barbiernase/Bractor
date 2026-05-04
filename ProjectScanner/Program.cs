using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ProjectScanner
{
    class Program
    {
        static void Main(string[] args)
        {
            string rootPath;
            
            if (args.Length > 0)
            {
                rootPath = args[0];
            }
            else
            {
                // Automatisch nach oben navigieren bis zur Solution
                rootPath = FindSolutionRoot(Directory.GetCurrentDirectory());
                Console.WriteLine($"Solution-Root gefunden: {rootPath}\n");
            }
            
            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine($"Pfad nicht gefunden: {rootPath}");
                return;
            }

            var scanner = new ProjectStructureScanner(rootPath);
            string output = scanner.GenerateLLMContext();
            
            Console.WriteLine(output);

            // Optional: In Datei speichern
            string outputPath = Path.Combine(rootPath, "PROJECT_STRUCTURE.md");
            File.WriteAllText(outputPath, output);
            Console.WriteLine($"\n---\nGespeichert unter: {outputPath}");
        }

        static string FindSolutionRoot(string startPath)
        {
            var current = new DirectoryInfo(startPath);
            
            while (current != null)
            {
                // .sln gefunden?
                if (current.GetFiles("*.sln").Any())
                {
                    return current.FullName;
                }
                
                // .git Ordner gefunden? (Repo-Root)
                if (current.GetDirectories(".git").Any())
                {
                    return current.FullName;
                }
                
                current = current.Parent;
            }
            
            // Fallback: 3 Ebenen nach oben (bin/Debug/net9.0 -> Projekt)
            var fallback = new DirectoryInfo(startPath);
            for (int i = 0; i < 3 && fallback.Parent != null; i++)
            {
                fallback = fallback.Parent;
            }
            
            return fallback.FullName;
        }
    }

    class ProjectStructureScanner
    {
        private static readonly HashSet<string> IgnoredFolders = new()
        {
            "bin", "obj", "node_modules", ".git", ".vs", ".idea",
            "packages", "TestResults", ".nuget", "artifacts"
        };

        private static readonly HashSet<string> ImportantFiles = new()
        {
            "Program.cs", "Startup.cs", "appsettings.json", "appsettings.Development.json",
            "launchSettings.json", "Directory.Build.props", "Directory.Packages.props",
            "global.json", "nuget.config", ".editorconfig", "Dockerfile", "docker-compose.yml"
        };

        private readonly string _rootPath;
        private readonly List<SolutionInfo> _solutions = new();
        private readonly List<ProjectInfo> _projects = new();
        private readonly StringBuilder _output = new();

        public ProjectStructureScanner(string rootPath)
        {
            _rootPath = Path.GetFullPath(rootPath);
        }

        public string GenerateLLMContext()
        {
            ScanSolutions();
            ScanProjects();

            GenerateHeader();
            GenerateOverview();
            GenerateFolderStructure();
            GenerateSolutionDetails();
            GenerateProjectDetails();
            GenerateDependencyGraph();
            GenerateEntryPoints();
            GenerateConfigFiles();

            return _output.ToString();
        }

        private void ScanSolutions()
        {
            foreach (var slnFile in Directory.GetFiles(_rootPath, "*.sln", SearchOption.AllDirectories))
            {
                if (IsIgnoredPath(slnFile)) continue;
                _solutions.Add(ParseSolution(slnFile));
            }
        }

        private void ScanProjects()
        {
            foreach (var csprojFile in Directory.GetFiles(_rootPath, "*.csproj", SearchOption.AllDirectories))
            {
                if (IsIgnoredPath(csprojFile)) continue;
                _projects.Add(ParseProject(csprojFile));
            }
        }

        private SolutionInfo ParseSolution(string slnPath)
        {
            var info = new SolutionInfo
            {
                Name = Path.GetFileNameWithoutExtension(slnPath),
                Path = GetRelativePath(slnPath),
                Projects = new List<string>()
            };

            try
            {
                var content = File.ReadAllText(slnPath);
                var projectMatches = Regex.Matches(content, 
                    @"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)"",\s*""([^""]+)""");
                
                foreach (Match match in projectMatches)
                {
                    var projectPath = match.Groups[2].Value;
                    if (projectPath.EndsWith(".csproj"))
                    {
                        info.Projects.Add(match.Groups[1].Value);
                    }
                }
            }
            catch { }

            return info;
        }

        private ProjectInfo ParseProject(string csprojPath)
        {
            var info = new ProjectInfo
            {
                Name = Path.GetFileNameWithoutExtension(csprojPath),
                Path = GetRelativePath(csprojPath),
                FolderPath = GetRelativePath(Path.GetDirectoryName(csprojPath)!),
                PackageReferences = new List<PackageRef>(),
                ProjectReferences = new List<string>(),
                Frameworks = new List<string>()
            };

            try
            {
                var doc = XDocument.Load(csprojPath);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                // Target Framework(s)
                var tfm = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;
                var tfms = doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
                
                if (!string.IsNullOrEmpty(tfms))
                    info.Frameworks.AddRange(tfms.Split(';'));
                else if (!string.IsNullOrEmpty(tfm))
                    info.Frameworks.Add(tfm);

                // Output Type
                info.OutputType = doc.Descendants("OutputType").FirstOrDefault()?.Value ?? "Library";

                // SDK
                info.Sdk = doc.Root?.Attribute("Sdk")?.Value ?? "Microsoft.NET.Sdk";

                // Package References
                foreach (var pkgRef in doc.Descendants("PackageReference"))
                {
                    info.PackageReferences.Add(new PackageRef
                    {
                        Name = pkgRef.Attribute("Include")?.Value ?? "",
                        Version = pkgRef.Attribute("Version")?.Value ?? 
                                  pkgRef.Element("Version")?.Value ?? "*"
                    });
                }

                // Project References
                foreach (var projRef in doc.Descendants("ProjectReference"))
                {
                    var include = projRef.Attribute("Include")?.Value ?? "";
                    info.ProjectReferences.Add(Path.GetFileNameWithoutExtension(include));
                }

                // Nullable
                info.Nullable = doc.Descendants("Nullable").FirstOrDefault()?.Value == "enable";

                // Implicit Usings
                info.ImplicitUsings = doc.Descendants("ImplicitUsings").FirstOrDefault()?.Value == "enable";

            }
            catch { }

            // Wichtige Dateien im Projekt finden
            info.KeyFiles = FindKeyFiles(Path.GetDirectoryName(csprojPath)!);

            // Ordnerstruktur
            info.Folders = GetProjectFolders(Path.GetDirectoryName(csprojPath)!);

            return info;
        }

        private List<string> FindKeyFiles(string projectDir)
        {
            var keyFiles = new List<string>();
            
            try
            {
                foreach (var file in Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories))
                {
                    if (IsIgnoredPath(file)) continue;
                    
                    var fileName = Path.GetFileName(file);
                    var relPath = GetRelativePath(file, projectDir);

                    // Wichtige benannte Dateien
                    if (ImportantFiles.Contains(fileName))
                    {
                        keyFiles.Add(relPath);
                        continue;
                    }

                    // Controller, Services, etc.
                    if (fileName.EndsWith("Controller.cs") ||
                        fileName.EndsWith("Service.cs") ||
                        fileName.EndsWith("Repository.cs") ||
                        fileName.EndsWith("Handler.cs") ||
                        fileName.EndsWith("Middleware.cs"))
                    {
                        keyFiles.Add(relPath);
                    }
                }
            }
            catch { }

            return keyFiles.Take(20).ToList();
        }

        private List<string> GetProjectFolders(string projectDir)
        {
            var folders = new List<string>();
            
            try
            {
                foreach (var dir in Directory.GetDirectories(projectDir, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(dir);
                    if (IgnoredFolders.Contains(name.ToLower())) continue;
                    
                    var fileCount = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length;
                    folders.Add($"{name}/ ({fileCount} .cs Dateien)");
                }
            }
            catch { }

            return folders;
        }

        private void GenerateHeader()
        {
            _output.AppendLine("# Projektstruktur-Dokumentation");
            _output.AppendLine();
            _output.AppendLine($"> Generiert am: {DateTime.Now:yyyy-MM-dd HH:mm}");
            _output.AppendLine($"> Wurzelverzeichnis: `{_rootPath}`");
            _output.AppendLine();
        }

        private void GenerateOverview()
        {
            _output.AppendLine("## Übersicht");
            _output.AppendLine();
            _output.AppendLine($"- **Solutions:** {_solutions.Count}");
            _output.AppendLine($"- **Projekte:** {_projects.Count}");
            
            var frameworks = _projects.SelectMany(p => p.Frameworks).Distinct().ToList();
            if (frameworks.Any())
            {
                _output.AppendLine($"- **Frameworks:** {string.Join(", ", frameworks)}");
            }

            var projectTypes = _projects.GroupBy(p => ClassifyProject(p)).ToList();
            _output.AppendLine();
            _output.AppendLine("### Projekttypen");
            foreach (var group in projectTypes.OrderBy(g => g.Key))
            {
                _output.AppendLine($"- {group.Key}: {string.Join(", ", group.Select(p => p.Name))}");
            }
            _output.AppendLine();
        }

        private string ClassifyProject(ProjectInfo p)
        {
            var name = p.Name.ToLower();
            var sdk = p.Sdk.ToLower();

            if (sdk.Contains("web")) return "🌐 Web/API";
            if (name.Contains("test") || p.PackageReferences.Any(r => r.Name.Contains("xunit") || r.Name.Contains("nunit"))) 
                return "🧪 Tests";
            if (name.Contains("shared") || name.Contains("common") || name.Contains("core")) 
                return "📦 Shared/Core";
            if (p.OutputType == "Exe") return "🚀 Executable";
            return "📚 Library";
        }

        private void GenerateFolderStructure()
        {
            _output.AppendLine("## Ordnerstruktur");
            _output.AppendLine();
            _output.AppendLine("```");
            PrintDirectory(_rootPath, "", true);
            _output.AppendLine("```");
            _output.AppendLine();
        }

        private void PrintDirectory(string path, string indent, bool isRoot)
        {
            var dirName = isRoot ? Path.GetFileName(path) ?? path : Path.GetFileName(path);
            
            if (!isRoot && IgnoredFolders.Contains(dirName.ToLower())) return;

            _output.AppendLine($"{indent}{dirName}/");

            try
            {
                // Wichtige Dateien in diesem Ordner
                var files = Directory.GetFiles(path)
                    .Select(Path.GetFileName)
                    .Where(f => f != null && (
                        f.EndsWith(".sln") || 
                        f.EndsWith(".csproj") || 
                        ImportantFiles.Contains(f!) ||
                        f == "README.md"
                    ))
                    .Take(10)
                    .ToList();

                foreach (var file in files)
                {
                    _output.AppendLine($"{indent}  {file}");
                }

                // Unterordner (max 3 Ebenen tief)
                if (indent.Length / 2 < 3)
                {
                    foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
                    {
                        PrintDirectory(dir, indent + "  ", false);
                    }
                }
            }
            catch { }
        }

        private void GenerateSolutionDetails()
        {
            if (!_solutions.Any()) return;

            _output.AppendLine("## Solutions");
            _output.AppendLine();

            foreach (var sln in _solutions)
            {
                _output.AppendLine($"### {sln.Name}");
                _output.AppendLine($"- Pfad: `{sln.Path}`");
                _output.AppendLine($"- Enthaltene Projekte ({sln.Projects.Count}):");
                foreach (var proj in sln.Projects)
                {
                    _output.AppendLine($"  - {proj}");
                }
                _output.AppendLine();
            }
        }

        private void GenerateProjectDetails()
        {
            _output.AppendLine("## Projekte im Detail");
            _output.AppendLine();

            foreach (var proj in _projects.OrderBy(p => p.Name))
            {
                _output.AppendLine($"### {proj.Name}");
                _output.AppendLine();
                _output.AppendLine($"| Eigenschaft | Wert |");
                _output.AppendLine($"|-------------|------|");
                _output.AppendLine($"| Pfad | `{proj.FolderPath}` |");
                _output.AppendLine($"| Typ | {proj.OutputType} |");
                _output.AppendLine($"| SDK | {proj.Sdk} |");
                _output.AppendLine($"| Framework(s) | {string.Join(", ", proj.Frameworks)} |");
                _output.AppendLine($"| Nullable | {(proj.Nullable ? "✅" : "❌")} |");
                _output.AppendLine();

                if (proj.ProjectReferences.Any())
                {
                    _output.AppendLine("**Projekt-Referenzen:**");
                    foreach (var r in proj.ProjectReferences)
                    {
                        _output.AppendLine($"- → {r}");
                    }
                    _output.AppendLine();
                }

                if (proj.PackageReferences.Any())
                {
                    _output.AppendLine("**NuGet-Pakete:**");
                    foreach (var pkg in proj.PackageReferences.Take(15))
                    {
                        _output.AppendLine($"- {pkg.Name} ({pkg.Version})");
                    }
                    if (proj.PackageReferences.Count > 15)
                    {
                        _output.AppendLine($"- ... +{proj.PackageReferences.Count - 15} weitere");
                    }
                    _output.AppendLine();
                }

                if (proj.Folders.Any())
                {
                    _output.AppendLine("**Ordnerstruktur:**");
                    foreach (var folder in proj.Folders)
                    {
                        _output.AppendLine($"- 📁 {folder}");
                    }
                    _output.AppendLine();
                }

                if (proj.KeyFiles.Any())
                {
                    _output.AppendLine("**Wichtige Dateien:**");
                    foreach (var file in proj.KeyFiles)
                    {
                        _output.AppendLine($"- `{file}`");
                    }
                    _output.AppendLine();
                }
            }
        }

        private void GenerateDependencyGraph()
        {
            var projectsWithRefs = _projects.Where(p => p.ProjectReferences.Any()).ToList();
            if (!projectsWithRefs.Any()) return;

            _output.AppendLine("## Abhängigkeiten (Mermaid)");
            _output.AppendLine();
            _output.AppendLine("```mermaid");
            _output.AppendLine("graph TD");

            foreach (var proj in _projects)
            {
                var nodeId = SanitizeForMermaid(proj.Name);
                var icon = ClassifyProject(proj).Split(' ')[0];
                _output.AppendLine($"    {nodeId}[\"{icon} {proj.Name}\"]");
            }

            _output.AppendLine();

            foreach (var proj in projectsWithRefs)
            {
                var fromId = SanitizeForMermaid(proj.Name);
                foreach (var refName in proj.ProjectReferences)
                {
                    var toId = SanitizeForMermaid(refName);
                    _output.AppendLine($"    {fromId} --> {toId}");
                }
            }

            _output.AppendLine("```");
            _output.AppendLine();
        }

        private void GenerateEntryPoints()
        {
            var executables = _projects.Where(p => p.OutputType == "Exe").ToList();
            var webProjects = _projects.Where(p => p.Sdk.Contains("Web")).ToList();

            if (!executables.Any() && !webProjects.Any()) return;

            _output.AppendLine("## Einstiegspunkte");
            _output.AppendLine();

            foreach (var proj in executables.Union(webProjects).Distinct())
            {
                _output.AppendLine($"- **{proj.Name}**");
                _output.AppendLine($"  - Pfad: `{proj.FolderPath}`");
                
                var programCs = proj.KeyFiles.FirstOrDefault(f => f.Contains("Program.cs"));
                if (programCs != null)
                {
                    _output.AppendLine($"  - Entry: `{programCs}`");
                }
                
                var startupCs = proj.KeyFiles.FirstOrDefault(f => f.Contains("Startup.cs"));
                if (startupCs != null)
                {
                    _output.AppendLine($"  - Startup: `{startupCs}`");
                }
            }
            _output.AppendLine();
        }

        private void GenerateConfigFiles()
        {
            _output.AppendLine("## Konfigurationsdateien");
            _output.AppendLine();

            try
            {
                var configFiles = new[] { "appsettings*.json", "*.config", "docker-compose*.yml", "Dockerfile*", ".env*" }
                    .SelectMany(pattern => Directory.GetFiles(_rootPath, pattern, SearchOption.AllDirectories))
                    .Where(f => !IsIgnoredPath(f))
                    .Select(f => GetRelativePath(f))
                    .Distinct()
                    .Take(20)
                    .ToList();

                if (configFiles.Any())
                {
                    foreach (var file in configFiles)
                    {
                        _output.AppendLine($"- `{file}`");
                    }
                }
            }
            catch { }

            _output.AppendLine();
        }

        private bool IsIgnoredPath(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Any(p => IgnoredFolders.Contains(p.ToLower()));
        }

        private string GetRelativePath(string fullPath, string? basePath = null)
        {
            basePath ??= _rootPath;
            return Path.GetRelativePath(basePath, fullPath);
        }

        private string SanitizeForMermaid(string name)
        {
            return Regex.Replace(name, @"[^a-zA-Z0-9]", "_");
        }
    }

    class SolutionInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public List<string> Projects { get; set; } = new();
    }

    class ProjectInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string FolderPath { get; set; } = "";
        public string OutputType { get; set; } = "Library";
        public string Sdk { get; set; } = "";
        public bool Nullable { get; set; }
        public bool ImplicitUsings { get; set; }
        public List<string> Frameworks { get; set; } = new();
        public List<PackageRef> PackageReferences { get; set; } = new();
        public List<string> ProjectReferences { get; set; } = new();
        public List<string> KeyFiles { get; set; } = new();
        public List<string> Folders { get; set; } = new();
    }

    class PackageRef
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
    }
}