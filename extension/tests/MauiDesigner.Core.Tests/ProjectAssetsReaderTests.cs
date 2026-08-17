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
