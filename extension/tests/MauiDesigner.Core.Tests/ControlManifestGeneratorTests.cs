using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MauiDesigner.Core.Manifests;

using Xunit;

namespace MauiDesigner.Core.Tests
{
    /// <summary>
    /// Scans the fake `Contoso.Maui.Controls` package that is built alongside the
    /// tests, so the reflection path is exercised for real without the MAUI workload.
    /// </summary>
    public class ControlManifestGeneratorTests
    {
        private static readonly string PackageAssembly = FakeAssembly("Contoso.Maui.Controls.dll");
        private static readonly string MauiAssembly = FakeAssembly("Microsoft.Maui.Controls.dll");

        private static string FakeAssembly(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            Assert.True(File.Exists(path), $"Expected the test fake '{fileName}' next to the test assembly.");
            return path;
        }

        private static IReadOnlyList<string> RuntimeReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
            return trusted.Split(Path.PathSeparator).Where(path => path.Length > 0).ToList();
        }

        private static IReadOnlyList<CustomControlManifest> Generate()
        {
            var references = RuntimeReferences().Concat(new[] { MauiAssembly }).ToList();
            return new ControlManifestGenerator()
                .Generate(new[] { PackageAssembly }, references, "Contoso.Maui.Controls", "1.2.3");
        }

        [Fact]
        public void Produces_one_manifest_per_clr_namespace()
        {
            var manifests = Generate();

            Assert.Equal(2, manifests.Count);
            Assert.Contains(manifests, manifest => manifest.Xmlns.Uri ==
                "clr-namespace:Contoso.Maui.Controls;assembly=Contoso.Maui.Controls");
            Assert.Contains(manifests, manifest => manifest.Xmlns.Uri ==
                "clr-namespace:Contoso.Maui.Charts;assembly=Contoso.Maui.Controls");
        }

        [Fact]
        public void Carries_the_package_identity()
        {
            var manifest = Generate().First();

            Assert.Equal("Contoso.Maui.Controls", manifest.Package);
            Assert.Equal("1.2.3", manifest.Version);
            Assert.NotEmpty(manifest.Id);
        }

        [Fact]
        public void Only_public_concrete_views_become_controls()
        {
            var controls = Generate()
                .SelectMany(manifest => manifest.Controls)
                .Select(control => control.Tag)
                .ToList();

            Assert.Contains("RatingBar", controls);
            Assert.Contains("CardPanel", controls);
            Assert.Contains("SparkLine", controls);
            Assert.DoesNotContain("AbstractControl", controls);
            Assert.DoesNotContain("InternalControl", controls);
            Assert.DoesNotContain("NotAControl", controls);
        }

        [Fact]
        public void Layouts_accept_children_and_render_as_slots()
        {
            var controls = Generate().SelectMany(manifest => manifest.Controls).ToList();

            var panel = controls.Single(control => control.Tag == "CardPanel");
            Assert.True(panel.CanHaveChildren);
            Assert.Equal("slot", panel.Preview!.Kind);

            var rating = controls.Single(control => control.Tag == "RatingBar");
            Assert.Null(rating.CanHaveChildren);
            Assert.Equal("box", rating.Preview!.Kind);
        }

        [Fact]
        public void Bindable_properties_become_editable_properties_with_mapped_types()
        {
            var rating = Generate()
                .SelectMany(manifest => manifest.Controls)
                .Single(control => control.Tag == "RatingBar");

            var byName = rating.Properties.ToDictionary(property => property.Name, property => property.Type);

            Assert.Equal("number", byName["Value"]);
            Assert.Equal("boolean", byName["IsReadOnly"]);
            Assert.Equal("string", byName["Caption"]);
            Assert.Equal("enum", byName["Shape"]);
            Assert.Equal("color", byName["FillColor"]);

            // A public static readonly field that is not a BindableProperty is ignored
            Assert.DoesNotContain(rating.Properties, property => property.Name == "Documentation");
        }

        [Fact]
        public void Enum_properties_expose_their_values()
        {
            var shape = Generate()
                .SelectMany(manifest => manifest.Controls)
                .Single(control => control.Tag == "RatingBar")
                .Properties
                .Single(property => property.Name == "Shape");

            Assert.Equal(new[] { "Star", "Heart" }, shape.Options);
        }

        [Fact]
        public void Missing_assemblies_yield_no_manifests()
        {
            var manifests = new ControlManifestGenerator()
                .Generate(new[] { "/does/not/exist.dll" }, Array.Empty<string>(), "Ghost");

            Assert.Empty(manifests);
        }

        [Fact]
        public void Generated_manifests_round_trip_through_the_designer_json_shape()
        {
            var json = ManifestJson.Serialize(Generate());

            Assert.Contains("\"xmlns\"", json);
            Assert.Contains("\"prefix\"", json);
            Assert.Contains("\"controls\"", json);
            Assert.Contains("\"RatingBar\"", json);
        }

        [Theory]
        [InlineData("Syncfusion.Maui.Inputs", "Syncfusion.Maui.Inputs", "inputs")]
        [InlineData("Telerik.Maui.Controls", "Telerik.UI.for.Maui", "telerik")]
        [InlineData("", "CommunityToolkit.Maui", "communitytoolkit")]
        public void Prefixes_are_short_and_readable(string clrNamespace, string packageId, string expected)
        {
            Assert.Equal(expected, ControlManifestGenerator.SuggestPrefix(clrNamespace, packageId));
        }

        [Theory]
        [InlineData("SfComboBox", "Sf Combo Box")]
        [InlineData("RatingBar", "Rating Bar")]
        [InlineData("Label", "Label")]
        [InlineData("", "")]
        public void Display_names_are_humanized(string name, string expected)
        {
            Assert.Equal(expected, ControlManifestGenerator.Humanize(name));
        }

        [Fact]
        public void Unknown_clr_types_fall_back_to_string_editors()
        {
            Assert.Equal("string", ControlManifestGenerator.MapType(null));
            Assert.Equal("string", ControlManifestGenerator.MapType(typeof(Uri)));
            Assert.Equal("number", ControlManifestGenerator.MapType(typeof(int?)));
            Assert.Equal("boolean", ControlManifestGenerator.MapType(typeof(bool)));
        }
    }
}
