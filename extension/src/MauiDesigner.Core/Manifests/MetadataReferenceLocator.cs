using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MauiDesigner.Core.Manifests
{
    public sealed class MetadataReferenceSet
    {
        public MetadataReferenceSet(IReadOnlyList<string> paths, string coreAssemblyName)
        {
            Paths = paths;
            CoreAssemblyName = coreAssemblyName;
        }

        public IReadOnlyList<string> Paths { get; }

        public string CoreAssemblyName { get; }
    }

    public static class MetadataReferenceLocator
    {
        public static MetadataReferenceSet ForTarget(string target)
        {
            var framework = TargetFramework(target);
            var references = FindReferencePack(framework);
            if (references.Count > 0)
            {
                return new MetadataReferenceSet(references, "System.Runtime");
            }

            var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                .Where(File.Exists)
                .ToList();
            if (trusted.Count > 0)
            {
                var core = trusted.FirstOrDefault(path =>
                    string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        "System.Private.CoreLib",
                        StringComparison.OrdinalIgnoreCase));
                return new MetadataReferenceSet(
                    trusted,
                    core is null ? "System.Runtime" : "System.Private.CoreLib");
            }

            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                .Select(assembly => assembly.Location)
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new MetadataReferenceSet(loaded, "mscorlib");
        }

        private static string TargetFramework(string target)
        {
            var slash = target.IndexOf('/');
            var framework = slash >= 0 ? target.Substring(0, slash) : target;
            var platform = framework.IndexOf('-');
            return platform >= 0 ? framework.Substring(0, platform) : framework;
        }

        private static IReadOnlyList<string> FindReferencePack(string framework)
        {
            if (!framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<string>();
            }

            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (string.IsNullOrWhiteSpace(dotnetRoot))
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                dotnetRoot = Path.Combine(programFiles, "dotnet");
            }

            var packs = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
            if (!Directory.Exists(packs))
            {
                return Array.Empty<string>();
            }

            var majorText = framework.Substring(3).Split('.')[0];
            var versionDirectory = Directory.EnumerateDirectories(packs)
                .Select(path => new { Path = path, Name = Path.GetFileName(path) })
                .Where(candidate => candidate.Name.StartsWith(majorText + ".", StringComparison.Ordinal))
                .OrderByDescending(candidate => ParseVersion(candidate.Name))
                .Select(candidate => candidate.Path)
                .FirstOrDefault();
            if (versionDirectory is null)
            {
                return Array.Empty<string>();
            }

            var referenceDirectory = Path.Combine(versionDirectory, "ref", framework);
            return Directory.Exists(referenceDirectory)
                ? Directory.EnumerateFiles(referenceDirectory, "*.dll").ToList()
                : Array.Empty<string>();
        }

        private static Version ParseVersion(string value) =>
            Version.TryParse(value.Split('-')[0], out var version)
                ? version
                : new Version();
    }
}
