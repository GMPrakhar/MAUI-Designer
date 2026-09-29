using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MauiDesigner.Core.Manifests;
using MauiDesigner.Core.Projects;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace MauiDesigner.Vsix.Projects
{
    /// <summary>
    /// Turns the NuGet packages a MAUI project restored into designer manifests,
    /// so third party controls appear in the toolbox with their real properties.
    /// </summary>
    public static class ProjectManifestProvider
    {
        private static readonly IReadOnlyDictionary<string, PackageStartupMethod> StartupMethods =
            new Dictionary<string, PackageStartupMethod>(StringComparer.OrdinalIgnoreCase)
            {
                ["Syncfusion.Maui.Core"] = new PackageStartupMethod
                {
                    Package = "Syncfusion.Maui.Core",
                    Assembly = "Syncfusion.Maui.Core",
                    Type = "Syncfusion.Maui.Core.Hosting.AppHostBuilderExtensions",
                    Method = "ConfigureSyncfusionCore"
                }
            };

        /// <summary>Finds the project file that owns an open document.</summary>
        public static string? FindProjectFile(IVsHierarchy? hierarchy, string documentMoniker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (hierarchy is IVsProject project &&
                ErrorHandler.Succeeded(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out var projectFile)) &&
                File.Exists(projectFile))
            {
                return projectFile;
            }

            // Fall back to walking up from the document until a project file appears.
            var directory = Path.GetDirectoryName(documentMoniker);
            while (!string.IsNullOrEmpty(directory))
            {
                var candidate = Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault();
                if (candidate is not null)
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        /// <summary>
        /// Scans every restored package of <paramref name="projectFile"/> and returns
        /// manifests for the ones that contain MAUI controls. Inspection failures
        /// are returned as package diagnostics instead of being silently ignored.
        /// </summary>
        public static ProjectControlManifest ForProject(string? projectFile)
        {
            if (projectFile is null)
            {
                return new ProjectControlManifest();
            }

            var assetsFile = ProjectAssetsReader.FindAssetsFile(projectFile);
            if (assetsFile is null)
            {
                return new ProjectControlManifest
                {
                    Diagnostics =
                    {
                        new ManifestDiagnostic
                        {
                            Message = "Restore the project before opening the designer; obj/project.assets.json is missing."
                        }
                    }
                };
            }

            var snapshot = ProjectAssetsReader.ReadWindows(assetsFile);
            var result = new ProjectControlManifest { Target = snapshot.Target };
            if (snapshot.Packages.Count == 0)
            {
                result.Diagnostics.Add(new ManifestDiagnostic
                {
                    Message = "No restored MAUI Windows target was found in project.assets.json."
                });
                return result;
            }

            var framework = MetadataReferenceLocator.ForTarget(snapshot.Target);
            var references = snapshot.Packages
                .SelectMany(package => package.AssemblyPaths)
                .Concat(framework.Paths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var generator = new ControlManifestGenerator();
            var controlPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in snapshot.Packages)
            {
                if (package.AssemblyPaths.Count == 0 || IsDesignerFrameworkPackage(package.Id))
                {
                    continue;
                }

                try
                {
                    var generated = generator.Generate(
                        package.AssemblyPaths,
                        references,
                        package.Id,
                        package.Version,
                        framework.CoreAssemblyName);
                    if (generated.Count == 0)
                    {
                        continue;
                    }

                    controlPackages.Add(package.Id);
                    result.Manifests.AddRange(generated);
                    foreach (var manifest in generated)
                    {
                        var marker = ";assembly=";
                        var index = manifest.Xmlns.Uri.IndexOf(marker, StringComparison.Ordinal);
                        if (index >= 0)
                        {
                            rootAssemblyNames.Add(manifest.Xmlns.Uri.Substring(index + marker.Length));
                        }
                    }
                }
                catch (Exception error)
                {
                    result.Diagnostics.Add(new ManifestDiagnostic
                    {
                        Package = package.Id,
                        Message = $"{error.GetType().Name}: {error.Message}"
                    });
                }
            }

            var closure = DependencyClosure(snapshot.Packages, controlPackages);
            foreach (var package in snapshot.Packages.Where(package =>
                         closure.Contains(package.Id) &&
                         !IsDesignerFrameworkPackage(package.Id)))
            {
                foreach (var path in package.RuntimeAssemblyPaths)
                {
                    result.Assemblies.Add(new RuntimeAssemblyDefinition
                    {
                        Path = path,
                        Package = package.Id,
                        IsRoot = rootAssemblyNames.Contains(Path.GetFileNameWithoutExtension(path))
                    });
                }

                if (StartupMethods.TryGetValue(package.Id, out var startup))
                {
                    result.StartupMethods.Add(startup);
                }
            }

            result.Assemblies = result.Assemblies
                .GroupBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(assembly => assembly.IsRoot).First())
                .ToList();
            return result;
        }

        private static HashSet<string> DependencyClosure(
            IReadOnlyList<PackageReferenceInfo> packages,
            HashSet<string> roots)
        {
            var byId = packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
            var closure = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>(roots);
            while (pending.Count > 0)
            {
                var id = pending.Dequeue();
                if (!byId.TryGetValue(id, out var package))
                {
                    continue;
                }

                foreach (var dependency in package.Dependencies)
                {
                    if (IsDesignerFrameworkPackage(dependency))
                    {
                        continue;
                    }

                    if (closure.Add(dependency))
                    {
                        pending.Enqueue(dependency);
                    }
                }
            }

            return closure;
        }

        private static bool IsDesignerFrameworkPackage(string packageId) =>
            packageId.StartsWith("Microsoft.Maui.", StringComparison.OrdinalIgnoreCase) ||
            packageId.Equals("Microsoft.Maui.Controls", StringComparison.OrdinalIgnoreCase) ||
            packageId.Equals("CommunityToolkit.Maui", StringComparison.OrdinalIgnoreCase);
    }
}
