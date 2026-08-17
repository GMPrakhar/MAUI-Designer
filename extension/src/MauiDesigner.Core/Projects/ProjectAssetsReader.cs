using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MauiDesigner.Core.Projects
{
    /// <summary>A NuGet package restored for a project, with the assemblies it contributes.</summary>
    public sealed class PackageReferenceInfo
    {
        public PackageReferenceInfo(string id, string version, IReadOnlyList<string> assemblyPaths)
        {
            Id = id;
            Version = version;
            AssemblyPaths = assemblyPaths;
        }

        public string Id { get; }

        public string Version { get; }

        /// <summary>Absolute paths to the package's reference/lib assemblies that exist on disk.</summary>
        public IReadOnlyList<string> AssemblyPaths { get; }
    }

    /// <summary>
    /// Reads <c>obj/project.assets.json</c> — produced by every NuGet restore — to
    /// discover which packages a MAUI project references and where their
    /// assemblies live in the global packages folder.
    /// </summary>
    public static class ProjectAssetsReader
    {
        /// <summary>Locates <c>obj/project.assets.json</c> next to a project file.</summary>
        public static string? FindAssetsFile(string projectFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(projectFilePath));
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            var assets = Path.Combine(directory, "obj", "project.assets.json");
            return File.Exists(assets) ? assets : null;
        }

        /// <summary>Reads the packages of the first (or requested) target framework.</summary>
        public static IReadOnlyList<PackageReferenceInfo> Read(string assetsFilePath, string? targetFramework = null)
        {
            if (!File.Exists(assetsFilePath))
            {
                throw new FileNotFoundException("project.assets.json was not found.", assetsFilePath);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(assetsFilePath));
            return Read(document.RootElement, targetFramework);
        }

        /// <summary>Overload used by the tests to avoid touching the file system.</summary>
        public static IReadOnlyList<PackageReferenceInfo> ReadJson(string json, string? targetFramework = null)
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement, targetFramework);
        }

        private static IReadOnlyList<PackageReferenceInfo> Read(JsonElement root, string? targetFramework)
        {
            var packages = new List<PackageReferenceInfo>();

            if (!root.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object)
            {
                return packages;
            }

            var target = SelectTarget(targets, targetFramework);
            if (target is null)
            {
                return packages;
            }

            var roots = PackageFolders(root);

            foreach (var entry in target.Value.EnumerateObject())
            {
                // Keys look like "CommunityToolkit.Maui/9.0.3"
                var separator = entry.Name.LastIndexOf('/');
                if (separator <= 0)
                {
                    continue;
                }

                if (entry.Value.TryGetProperty("type", out var type) &&
                    !string.Equals(type.GetString(), "package", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = entry.Name.Substring(0, separator);
                var version = entry.Name.Substring(separator + 1);
                var relative = RelativeAssemblyPaths(entry.Value);
                var resolved = Resolve(roots, id, version, relative);

                packages.Add(new PackageReferenceInfo(id, version, resolved));
            }

            return packages;
        }

        private static JsonElement? SelectTarget(JsonElement targets, string? targetFramework)
        {
            foreach (var candidate in targets.EnumerateObject())
            {
                if (targetFramework is null ||
                    candidate.Name.IndexOf(targetFramework, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate.Value;
                }
            }

            return null;
        }

        private static IReadOnlyList<string> PackageFolders(JsonElement root)
        {
            var folders = new List<string>();

            if (root.TryGetProperty("packageFolders", out var packageFolders) &&
                packageFolders.ValueKind == JsonValueKind.Object)
            {
                folders.AddRange(packageFolders.EnumerateObject().Select(folder => folder.Name));
            }

            if (folders.Count == 0)
            {
                folders.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget",
                    "packages"));
            }

            return folders;
        }

        private static IReadOnlyList<string> RelativeAssemblyPaths(JsonElement package)
        {
            var paths = new List<string>();

            // "compile" is the reference assembly set; fall back to "runtime".
            foreach (var section in new[] { "compile", "runtime" })
            {
                if (!package.TryGetProperty(section, out var items) || items.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var item in items.EnumerateObject())
                {
                    // NuGet uses "_._" as a placeholder for "no assets".
                    if (item.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(item.Name.Replace('/', Path.DirectorySeparatorChar));
                    }
                }

                if (paths.Count > 0)
                {
                    break;
                }
            }

            return paths;
        }

        private static IReadOnlyList<string> Resolve(
            IReadOnlyList<string> roots,
            string id,
            string version,
            IReadOnlyList<string> relativePaths)
        {
            var resolved = new List<string>();

            foreach (var relative in relativePaths)
            {
                foreach (var root in roots)
                {
                    var full = Path.Combine(root, id.ToLowerInvariant(), version.ToLowerInvariant(), relative);
                    if (File.Exists(full))
                    {
                        resolved.Add(full);
                        break;
                    }

                    // Some feeds keep the original casing on disk.
                    var cased = Path.Combine(root, id, version, relative);
                    if (File.Exists(cased))
                    {
                        resolved.Add(cased);
                        break;
                    }
                }
            }

            return resolved;
        }
    }
}
