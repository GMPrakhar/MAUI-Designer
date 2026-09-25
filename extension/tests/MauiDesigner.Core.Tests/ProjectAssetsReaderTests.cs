using System;
using System.IO;
using System.Linq;

using MauiDesigner.Core.Projects;

using Xunit;

namespace MauiDesigner.Core.Tests
{
    public class ProjectAssetsReaderTests
    {
        private const string Assets = @"{
  ""version"": 3,
  ""targets"": {
    ""net8.0-android34.0"": {
      ""CommunityToolkit.Maui/9.0.3"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/net8.0/CommunityToolkit.Maui.dll"": {},
          ""lib/net8.0/CommunityToolkit.Maui.Core.dll"": {}
        },
        ""runtime"": {
          ""lib/net8.0/CommunityToolkit.Maui.dll"": {}
        }
      },
      ""Newtonsoft.Json/13.0.3"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/netstandard2.0/_._"": {}
        }
      },
      ""MyApp.Shared/1.0.0"": {
        ""type"": ""project""
      }
    }
  },
  ""packageFolders"": {
    ""PACKAGES_ROOT"": {}
  }
}";

        private static string WithRoot(string root) => Assets.Replace("PACKAGES_ROOT", root.Replace(@"\", @"\\"));

        [Fact]
        public void Reads_packages_from_the_first_target()
        {
            var packages = ProjectAssetsReader.ReadJson(Assets);

            Assert.Equal(2, packages.Count);
            Assert.Contains(packages, package => package.Id == "CommunityToolkit.Maui" && package.Version == "9.0.3");
            Assert.Contains(packages, package => package.Id == "Newtonsoft.Json");
        }

        [Fact]
        public void Project_references_are_not_packages()
        {
            var packages = ProjectAssetsReader.ReadJson(Assets);

            Assert.DoesNotContain(packages, package => package.Id == "MyApp.Shared");
        }

        [Fact]
        public void Resolves_assembly_paths_that_exist_on_disk()
        {
            var root = Path.Combine(Path.GetTempPath(), "maui-designer-assets-" + Guid.NewGuid().ToString("N"));
            var libraryDirectory = Path.Combine(root, "communitytoolkit.maui", "9.0.3", "lib", "net8.0");
            Directory.CreateDirectory(libraryDirectory);
            File.WriteAllText(Path.Combine(libraryDirectory, "CommunityToolkit.Maui.dll"), string.Empty);

            try
            {
                var package = ProjectAssetsReader.ReadJson(WithRoot(root))
                    .Single(candidate => candidate.Id == "CommunityToolkit.Maui");

                // Only the assembly that is actually on disk is returned
                Assert.Equal(
                    new[] { Path.Combine(libraryDirectory, "CommunityToolkit.Maui.dll") },
                    package.AssemblyPaths);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void Placeholder_assets_contribute_no_assemblies()
        {
            var package = ProjectAssetsReader.ReadJson(Assets).Single(candidate => candidate.Id == "Newtonsoft.Json");

            Assert.Empty(package.AssemblyPaths);
        }

        [Fact]
        public void A_target_framework_can_be_selected()
        {
            var packages = ProjectAssetsReader.ReadJson(Assets, "net8.0-android");

            Assert.NotEmpty(packages);
            Assert.Empty(ProjectAssetsReader.ReadJson(Assets, "net472"));
        }

        [Fact]
        public void Windows_runtime_target_is_selected_instead_of_first_android_target()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "maui-designer-windows-assets-" + Guid.NewGuid().ToString("N"));
            var android = Path.Combine(root, "contoso.controls", "1.0.0", "lib", "net10.0-android");
            var windows = Path.Combine(
                root,
                "contoso.controls",
                "1.0.0",
                "lib",
                "net10.0-windows10.0.19041");
            Directory.CreateDirectory(android);
            Directory.CreateDirectory(windows);
            File.WriteAllText(Path.Combine(android, "Contoso.Controls.dll"), string.Empty);
            File.WriteAllText(Path.Combine(windows, "Contoso.Controls.dll"), string.Empty);

            var json = $$"""
                {
                  "targets": {
                    "net10.0-android": {
                      "Contoso.Controls/1.0.0": {
                        "type": "package",
                        "compile": { "lib/net10.0-android/Contoso.Controls.dll": {} }
                      }
                    },
                    "net10.0-windows10.0.19041.0/win-x64": {
                      "Contoso.Controls/1.0.0": {
                        "type": "package",
                        "dependencies": { "Contoso.Core": "1.0.0" },
                        "compile": { "lib/net10.0-windows10.0.19041/Contoso.Controls.dll": {} },
                        "runtime": { "lib/net10.0-windows10.0.19041/Contoso.Controls.dll": {} }
                      }
                    }
                  },
                  "packageFolders": { "{{root.Replace(@"\", @"\\")}}": {} }
                }
                """;

            try
            {
                var snapshot = ProjectAssetsReader.ReadWindowsJson(json);
                var package = Assert.Single(snapshot.Packages);

                Assert.Equal("net10.0-windows10.0.19041.0/win-x64", snapshot.Target);
                Assert.Contains("net10.0-windows10.0.19041", package.AssemblyPaths.Single());
                Assert.Equal(package.AssemblyPaths, package.RuntimeAssemblyPaths);
                Assert.Equal(new[] { "Contoso.Core" }, package.Dependencies);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void Windows_runtime_target_excludes_native_runtime_assets()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "maui-designer-runtime-assets-" + Guid.NewGuid().ToString("N"));
            var runtime = Path.Combine(root, "contoso.controls", "1.0.0", "runtimes", "win-x64");
            Directory.CreateDirectory(Path.Combine(runtime, "lib", "net10.0"));
            Directory.CreateDirectory(Path.Combine(runtime, "native"));
            var managedPath = Path.Combine(runtime, "lib", "net10.0", "Contoso.Controls.dll");
            var nativePath = Path.Combine(runtime, "native", "Contoso.Native.dll");
            File.WriteAllText(managedPath, string.Empty);
            File.WriteAllText(nativePath, string.Empty);

            var json = $$"""
                {
                  "targets": {
                    "net10.0-windows10.0.19041.0/win-x64": {
                      "Contoso.Controls/1.0.0": {
                        "type": "package",
                        "runtimeTargets": {
                          "runtimes/win-x64/lib/net10.0/Contoso.Controls.dll": {
                            "assetType": "runtime",
                            "rid": "win-x64"
                          },
                          "runtimes/win-x64/native/Contoso.Native.dll": {
                            "assetType": "native",
                            "rid": "win-x64"
                          }
                        }
                      }
                    }
                  },
                  "packageFolders": { "{{root.Replace(@"\", @"\\")}}": {} }
                }
                """;

            try
            {
                var package = Assert.Single(ProjectAssetsReader.ReadWindowsJson(json).Packages);

                Assert.Equal(new[] { managedPath }, package.RuntimeAssemblyPaths);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void An_assets_file_without_targets_is_empty_rather_than_fatal()
        {
            Assert.Empty(ProjectAssetsReader.ReadJson("{}"));
        }

        [Fact]
        public void Finds_the_assets_file_next_to_a_project()
        {
            var root = Path.Combine(Path.GetTempPath(), "maui-designer-project-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "obj"));
            var project = Path.Combine(root, "MyApp.csproj");
            File.WriteAllText(project, "<Project />");

            try
            {
                Assert.Null(ProjectAssetsReader.FindAssetsFile(project));

                var assets = Path.Combine(root, "obj", "project.assets.json");
                File.WriteAllText(assets, "{}");

                Assert.Equal(assets, ProjectAssetsReader.FindAssetsFile(project));
                Assert.Null(ProjectAssetsReader.FindAssetsFile(""));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void A_missing_assets_file_is_reported_clearly()
        {
            Assert.Throws<FileNotFoundException>(
                () => ProjectAssetsReader.Read(Path.Combine(Path.GetTempPath(), "nope.assets.json")));
        }
    }
}
