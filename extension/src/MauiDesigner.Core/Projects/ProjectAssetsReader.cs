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
        public PackageReferenceInfo(
            string id,
            string version,
            IReadOnlyList<string> assemblyPaths,
            IReadOnlyList<string>? runtimeAssemblyPaths = null,
            IReadOnlyList<string>? dependencies = null)
        {
            Id = id;
            Version = version;
            AssemblyPaths = assemblyPaths;
            RuntimeAssemblyPaths = runtimeAssemblyPaths ?? assemblyPaths;
            Dependencies = dependencies ?? Array.Empty<string>();
        }

        public string Id { get; }

        public string Version { get; }

        /// <summary>Absolute paths to the package's reference/lib assemblies that exist on disk.</summary>
        public IReadOnlyList<string> AssemblyPaths { get; }

        public IReadOnlyList<string> RuntimeAssemblyPaths { get; }

        public IReadOnlyList<string> Dependencies { get; }
    }

    public sealed class ProjectAssetsSnapshot
    {
        public ProjectAssetsSnapshot(string target, IReadOnlyList<PackageReferenceInfo> packages)
        {
            Target = target;
            Packages = packages;
        }

        public string Target { get; }

        public IReadOnlyList<PackageReferenceInfo> Packages { get; }
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

        public static ProjectAssetsSnapshot ReadWindows(string assetsFilePath, string runtimeIdentifier = "win-x64")
        {
            if (!File.Exists(assetsFilePath))
            {
                throw new FileNotFoundException("project.assets.json was not found.", assetsFilePath);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(assetsFilePath));
            return ReadWindows(document.RootElement, runtimeIdentifier);
        }

        public static ProjectAssetsSnapshot ReadWindowsJson(string json, string runtimeIdentifier = "win-x64")
        {
            using var document = JsonDocument.Parse(json);
            return ReadWindows(document.RootElement, runtimeIdentifier);
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

            var selected = SelectTarget(targets, targetFramework);
            var target = selected?.Value;
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
                var compile = Resolve(roots, id, version, RelativeAssemblyPaths(entry.Value, "compile"));
                var runtime = Resolve(roots, id, version, RelativeAssemblyPaths(entry.Value, "runtime"));
                packages.Add(new PackageReferenceInfo(
                    id,
                    version,
                    compile.Count > 0 ? compile : runtime,
                    runtime.Count > 0 ? runtime : compile,
                    Dependencies(entry.Value)));
            }

            return packages;
        }

        private static ProjectAssetsSnapshot ReadWindows(JsonElement root, string runtimeIdentifier)
        {
            if (!root.TryGetProperty("targets", out var targets) ||
                targets.ValueKind != JsonValueKind.Object)
            {
                return new ProjectAssetsSnapshot(string.Empty, Array.Empty<PackageReferenceInfo>());
            }

            var selected = SelectWindowsTarget(targets, runtimeIdentifier);
            if (selected is null)
            {
                return new ProjectAssetsSnapshot(string.Empty, Array.Empty<PackageReferenceInfo>());
            }

            var roots = PackageFolders(root);
            var packages = new List<PackageReferenceInfo>();
            foreach (var entry in selected.Value.Value.EnumerateObject())
            {
                var separator = entry.Name.LastIndexOf('/');
                if (separator <= 0 ||
                    entry.Value.TryGetProperty("type", out var type) &&
                    !string.Equals(type.GetString(), "package", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = entry.Name.Substring(0, separator);
                var version = entry.Name.Substring(separator + 1);
                var compile = Resolve(roots, id, version, RelativeAssemblyPaths(entry.Value, "compile"));
                var runtime = Resolve(roots, id, version, RuntimeAssemblyPaths(entry.Value, runtimeIdentifier));
                packages.Add(new PackageReferenceInfo(
                    id,
                    version,
                    compile.Count > 0 ? compile : runtime,
                    runtime.Count > 0 ? runtime : compile,
                    Dependencies(entry.Value)));
            }

            return new ProjectAssetsSnapshot(selected.Value.Name, packages);
        }

        private static JsonProperty? SelectTarget(JsonElement targets, string? targetFramework)
        {
            foreach (var candidate in targets.EnumerateObject())
            {
                if (targetFramework is null ||
                    candidate.Name.IndexOf(targetFramework, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static JsonProperty? SelectWindowsTarget(JsonElement targets, string runtimeIdentifier)
        {
            var candidates = targets.EnumerateObject()
                .Where(candidate =>
                    candidate.Name.IndexOf("-windows", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var ridSuffix = "/" + runtimeIdentifier;
            foreach (var candidate in candidates)
            {
                if (candidate.Name.EndsWith(ridSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            foreach (var candidate in candidates)
            {
                if (candidate.Name.IndexOf('/') < 0)
                {
                    return candidate;
                }
            }

            return candidates[0];
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

        private static IReadOnlyList<string> RelativeAssemblyPaths(JsonElement package, string section)
        {
            var paths = new List<string>();
            if (!package.TryGetProperty(section, out var items) || items.ValueKind != JsonValueKind.Object)
            {
                return paths;
            }

            foreach (var item in items.EnumerateObject())
            {
                if (item.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(item.Name.Replace('/', Path.DirectorySeparatorChar));
                }
            }

            return paths;
        }

        private static IReadOnlyList<string> RuntimeAssemblyPaths(
            JsonElement package,
            string runtimeIdentifier)
        {
            var paths = RelativeAssemblyPaths(package, "runtime").ToList();
            if (package.TryGetProperty("runtimeTargets", out var targets) &&
                targets.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in targets.EnumerateObject())
                {
                    if (!item.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (item.Value.TryGetProperty("assetType", out var assetType) &&
                        !string.Equals(
                            assetType.GetString(),
                            "runtime",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (item.Value.TryGetProperty("rid", out var rid) &&
                        string.Equals(rid.GetString(), runtimeIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(item.Name.Replace('/', Path.DirectorySeparatorChar));
                    }
                }
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IReadOnlyList<string> Dependencies(JsonElement package)
        {
            if (!package.TryGetProperty("dependencies", out var dependencies) ||
                dependencies.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            return dependencies.EnumerateObject().Select(item => item.Name).ToList();
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
