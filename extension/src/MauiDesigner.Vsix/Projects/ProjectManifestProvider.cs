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
        /// manifests for the ones that contain MAUI controls. Never throws for a
        /// package that cannot be inspected: it is simply skipped.
        /// </summary>
        public static IReadOnlyList<CustomControlManifest> ForProject(string? projectFile)
        {
            if (projectFile is null)
            {
                return Array.Empty<CustomControlManifest>();
            }

            var assetsFile = ProjectAssetsReader.FindAssetsFile(projectFile);
            if (assetsFile is null)
            {
                return Array.Empty<CustomControlManifest>();
            }

            var packages = ProjectAssetsReader.Read(assetsFile);

            // Base types must resolve, so every package assembly is offered as a reference.
            var references = packages.SelectMany(package => package.AssemblyPaths).Distinct().ToList();

            var generator = new ControlManifestGenerator();
            var manifests = new List<CustomControlManifest>();

            foreach (var package in packages)
            {
                if (package.AssemblyPaths.Count == 0)
                {
                    continue;
                }

                try
                {
                    manifests.AddRange(
                        generator.Generate(package.AssemblyPaths, references, package.Id, package.Version));
                }
                catch (Exception)
                {
                    // A package that cannot be inspected must not break the designer.
                }
            }

            return manifests;
        }
    }
}
