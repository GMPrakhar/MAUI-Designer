using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Xunit;

namespace MauiDesigner.Core.Tests
{
    /// <summary>
    /// Checks how the extension registers itself with Visual Studio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Visual Studio only runs on Windows, so nobody can install this VSIX on a
    /// build agent and click through it. The registration attributes are still
    /// worth testing, because getting them wrong fails in the most expensive way
    /// possible: the extension compiles, packages and installs cleanly, and then
    /// silently does nothing. Pointing <c>ProvideEditorExtension</c> at a GUID no
    /// class declares, for instance, is invisible to the compiler.
    /// </para>
    /// <para>
    /// The attributes are read straight out of the compiled assembly with
    /// <see cref="MetadataLoadContext"/>, which needs no Windows and no Visual
    /// Studio - it never executes the code, it only reads metadata.
    /// </para>
    /// </remarks>
    public sealed class RegistrationMetadataTests : IDisposable
    {
        private const string PackageTypeName = "MauiDesigner.Vsix.MauiDesignerPackage";
        private const string EditorFactoryTypeName = "MauiDesigner.Vsix.DesignerEditorFactory";

        private readonly MetadataLoadContext _context;
        private readonly Assembly _extension;

        public RegistrationMetadataTests()
        {
            var compileCheckOutput = CompileCheckOutputDirectory();
            var assemblyPath = Path.Combine(compileCheckOutput, "MauiDesigner.Vsix.CompileCheck.dll");

            // The reference list is written by the compile check project itself, so
            // the resolver always matches whatever MSBuild actually compiled against.
            var references = File.ReadAllLines(Path.Combine(compileCheckOutput, "compile-check-references.txt"))
                .Where(line => line.Length > 0 && File.Exists(line));

            var assemblies = new List<string>(references) { assemblyPath };

            _context = new MetadataLoadContext(new PathAssemblyResolver(assemblies));
            _extension = _context.LoadFromAssemblyPath(assemblyPath);
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public void The_registered_editor_factory_declares_the_guid_visual_studio_resolves_it_by()
        {
            var package = _extension.GetType(PackageTypeName, throwOnError: true)!;
            var factory = _extension.GetType(EditorFactoryTypeName, throwOnError: true)!;

            var registration = Attribute(package, "ProvideEditorExtensionAttribute");
            var registeredFactory = Assert.IsAssignableFrom<Type>(registration.ConstructorArguments[0].Value);
            Assert.Equal(factory.FullName, registeredFactory.FullName);

            // The attribute names the type, but Visual Studio writes its *GUID*
            // into the pkgdef and resolves the factory by that at runtime. Dropping
            // [Guid] still compiles and still packages; the editor just never
            // appears in Open With. Same for the package itself.
            Assert.True(
                Guid.TryParse(GuidOf(registeredFactory), out var factoryGuid) && factoryGuid != Guid.Empty,
                "The registered editor factory has no usable [Guid].");

            Assert.True(
                Guid.TryParse(GuidOf(package), out var packageGuid) && packageGuid != Guid.Empty,
                "The package has no usable [Guid].");

            Assert.NotEqual(packageGuid, factoryGuid);
        }

        [Fact]
        public void The_designer_is_registered_for_xaml_files()
        {
            var package = _extension.GetType(PackageTypeName, throwOnError: true)!;
            var registration = Attribute(package, "ProvideEditorExtensionAttribute");

            Assert.Equal(".xaml", registration.ConstructorArguments[1].Value);
        }

        [Fact]
        public void Every_registered_editor_name_has_a_managed_string_resource()
        {
            var package = _extension.GetType(PackageTypeName, throwOnError: true)!;
            var factoryRegistration = Attribute(package, "ProvideEditorFactoryAttribute");
            var extensionRegistration = Attribute(package, "ProvideEditorExtensionAttribute");
            var projectDirectory = Path.Combine(ExtensionDirectory(), "src", "MauiDesigner.Vsix");
            var project = XDocument.Load(Path.Combine(projectDirectory, "MauiDesigner.Vsix.csproj"));
            var useCodeBase = project.Descendants()
                .Single(element => element.Name.LocalName == "UseCodeBase")
                .Value;
            var resourceItem = project.Descendants()
                .Single(element => element.Name.LocalName == "EmbeddedResource"
                                   && element.Attribute("Include")?.Value == "VsPackage.resx");

            Assert.Equal("true", useCodeBase);
            Assert.Equal(
                "VSPackage",
                resourceItem.Elements().Single(element => element.Name.LocalName == "ManifestResourceName").Value);
            Assert.Equal(
                "true",
                resourceItem.Elements().Single(element => element.Name.LocalName == "MergeWithCTO").Value);

            var referencedIds = new[]
            {
                Convert.ToInt32(factoryRegistration.ConstructorArguments[1].Value),
                Convert.ToInt32(extensionRegistration.NamedArguments
                    .Single(argument => argument.MemberName == "NameResourceID")
                    .TypedValue.Value)
            };

            var resources = XDocument.Load(
                    Path.Combine(projectDirectory, "VsPackage.resx"))
                .Root!
                .Elements("data")
                .Select(element => element.Attribute("name")?.Value)
                .Where(name => name is not null)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var resourceId in referencedIds)
            {
                Assert.Contains(resourceId.ToString(), resources);
            }
        }

        [Fact]
        public void The_built_in_xaml_editor_keeps_priority()
        {
            var package = _extension.GetType(PackageTypeName, throwOnError: true)!;
            var registration = Attribute(package, "ProvideEditorExtensionAttribute");

            // Deliberately low: double-clicking a .xaml file must keep opening the
            // editor the user already knows. Raising this would hijack every XAML
            // file in every solution, which is exactly the kind of change that
            // should have to break a test first.
            var priority = Convert.ToInt32(registration.ConstructorArguments[2].Value);
            Assert.True(priority < 0x40, $"Expected a priority below the built-in editor, but found 0x{priority:X}.");
        }

        [Fact]
        public void Background_loading_is_matched_by_an_async_package()
        {
            var package = _extension.GetType(PackageTypeName, throwOnError: true)!;
            var registration = Attribute(package, "PackageRegistrationAttribute");

            var allowsBackgroundLoading = registration.NamedArguments
                .Where(argument => argument.MemberName == "AllowsBackgroundLoading")
                .Select(argument => (bool)argument.TypedValue.Value!)
                .FirstOrDefault();

            if (allowsBackgroundLoading)
            {
                // Promising background loading without deriving from AsyncPackage
                // makes Visual Studio refuse to load the package at all.
                Assert.True(
                    InheritsFrom(package, "Microsoft.VisualStudio.Shell.AsyncPackage"),
                    "The package declares AllowsBackgroundLoading but does not derive from AsyncPackage.");
            }
        }

        [Fact]
        public void The_editor_factory_implements_the_interface_visual_studio_calls()
        {
            var factory = _extension.GetType(EditorFactoryTypeName, throwOnError: true)!;

            Assert.Contains(
                factory.GetInterfaces(),
                candidate => candidate.FullName == "Microsoft.VisualStudio.Shell.Interop.IVsEditorFactory");
        }

        [Fact]
        public void The_designer_pane_tracks_the_shared_text_buffer()
        {
            var pane = _extension.GetType("MauiDesigner.Vsix.DesignerPane", throwOnError: true)!;

            Assert.Contains(
                pane.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
                field => field.FieldType.FullName == "Microsoft.VisualStudio.Text.ITextBuffer");
        }

        [Fact]
        public void Designer_edits_are_written_through_the_shared_text_buffer()
        {
            var source = File.ReadAllText(Path.Combine(
                ExtensionDirectory(),
                "src",
                "MauiDesigner.Vsix",
                "Editor",
                "DesignerPane.cs"));

            Assert.Contains("textBuffer.Replace(new Span(0, snapshot.Length), xaml)", source);
            Assert.DoesNotContain("_textLines.ReplaceLines", source);
        }

        [Fact]
        public void Standalone_designer_buffers_are_sited_before_visual_studio_loads_them()
        {
            var source = File.ReadAllText(Path.Combine(
                ExtensionDirectory(),
                "src",
                "MauiDesigner.Vsix",
                "Editor",
                "DesignerEditorFactory.cs"));

            Assert.Contains("objectWithSite.SetSite(_oleServiceProvider)", source);
        }

        [Fact]
        public void The_logical_view_offered_is_one_the_factory_maps()
        {
            var package = _extension.GetType(PackageTypeName, throwOnError: true)!;
            var registration = Attribute(package, "ProvideEditorLogicalViewAttribute");

            // {7651a702-06e5-11d1-8ebd-00a0c90f26ea} is LOGVIEWID_Designer, which
            // DesignerEditorFactory.MapLogicalView accepts. Registering a view the
            // factory rejects makes Open With fail with an unhelpful error.
            var logicalView = Assert.IsType<string>(registration.ConstructorArguments[1].Value);
            Assert.Equal(
                new Guid("7651a702-06e5-11d1-8ebd-00a0c90f26ea"),
                Guid.Parse(logicalView));
        }

        [Fact]
        public void Every_file_the_manifest_references_exists()
        {
            var projectDirectory = Path.Combine(ExtensionDirectory(), "src", "MauiDesigner.Vsix");
            var manifestPath = Path.Combine(projectDirectory, "source.extension.vsixmanifest");
            var manifest = XDocument.Load(manifestPath);
            XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";

            var metadata = manifest.Root!.Element(ns + "Metadata")!;

            // A manifest naming a file that isn't there fails during packaging on
            // Windows, which is the slowest possible place to find out. It has
            // happened once already: <License>LICENSE.txt</License> with no such
            // file anywhere in the repository.
            var referenced = new[] { "License", "Icon", "PreviewImage", "ReleaseNotes", "GettingStartedGuide" }
                .Select(name => metadata.Element(ns + name)?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Where(value => !value!.StartsWith("http", StringComparison.OrdinalIgnoreCase));

            foreach (var relativePath in referenced)
            {
                var full = Path.Combine(projectDirectory, relativePath!.Replace('\\', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    continue;
                }

                // Not every referenced file is checked in. LICENSE.txt is copied
                // out of the repository's LICENSE.md during the build, because a
                // VSIX cannot package a file from outside the project directory.
                // Such a file is only as safe as the thing that generates it, so
                // follow the build back to a real source rather than assuming.
                var generatedFrom = SourceOfGeneratedVsixFile(projectDirectory, relativePath!);
                Assert.True(
                    generatedFrom is not null,
                    $"The manifest references '{relativePath}', which is neither in {projectDirectory} nor produced by the project file.");
                Assert.True(
                    File.Exists(generatedFrom),
                    $"The manifest references '{relativePath}', which the build copies from '{generatedFrom}' - and that file does not exist.");
            }
        }

        /// <summary>
        /// Follows a file the VSIX packages back to the file the build copies it
        /// from, or <c>null</c> when nothing in the project produces it.
        /// </summary>
        private static string? SourceOfGeneratedVsixFile(string projectDirectory, string vsixName)
        {
            var projectPath = Path.Combine(projectDirectory, "MauiDesigner.Vsix.csproj");
            var project = XDocument.Load(projectPath);

            // The SDK-style project file here declares no namespace.
            var items = project.Descendants()
                .Where(element => element.Elements().Any(child => child.Name.LocalName == "IncludeInVSIX"
                                                                 && child.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)));

            var item = items.FirstOrDefault(candidate =>
            {
                var link = candidate.Elements().FirstOrDefault(child => child.Name.LocalName == "Link")?.Value;
                var name = link ?? candidate.Attribute("Include")?.Value;
                return Path.GetFileName(name?.Replace('\\', '/')) == Path.GetFileName(vsixName.Replace('\\', '/'));
            });

            var include = item?.Attribute("Include")?.Value;
            if (include is null)
            {
                return null;
            }

            // The item is packaged from an intermediate copy, so the source of
            // truth is whatever the Copy task reads. Match on the unexpanded
            // property so this does not have to reimplement MSBuild.
            var copy = project.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Copy"
                                           && element.Attribute("DestinationFiles")?.Value == include);

            var source = copy?.Attribute("SourceFiles")?.Value;
            return source is null ? null : ExpandProperties(project, source, projectDirectory);
        }

        /// <summary>
        /// Expands the handful of MSBuild properties these paths use.
        /// </summary>
        private static string ExpandProperties(XDocument project, string value, string projectDirectory)
        {
            var properties = project.Descendants()
                .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
                .GroupBy(element => element.Name.LocalName)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

            properties["MSBuildThisFileDirectory"] = projectDirectory + Path.DirectorySeparatorChar;

            // Properties can be defined in terms of each other, so keep going
            // until the value stops changing.
            for (var pass = 0; pass < 10 && value.Contains("$("); pass++)
            {
                foreach (var property in properties)
                {
                    value = value.Replace($"$({property.Key})", property.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            return Path.GetFullPath(value.Replace('\\', Path.DirectorySeparatorChar));
        }

        [Fact]
        public void The_manifest_installs_on_every_supported_visual_studio()
        {
            var manifestPath = Path.Combine(ExtensionDirectory(), "src", "MauiDesigner.Vsix", "source.extension.vsixmanifest");
            var manifest = XDocument.Load(manifestPath);
            XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";

            var identity = manifest.Root!.Element(ns + "Metadata")!.Element(ns + "Identity")!;
            Assert.True(Version.TryParse(identity.Attribute("Version")!.Value, out _));

            var targets = manifest.Root!.Element(ns + "Installation")!.Elements(ns + "InstallationTarget").ToList();
            Assert.NotEmpty(targets);

            // Every architecture has to accept the same range, or the extension
            // silently installs on some machines and not others.
            var ranges = targets.Select(target => target.Attribute("Version")!.Value).Distinct().ToList();
            Assert.True(ranges.Count == 1, $"The installation targets disagree on a version range: {string.Join(", ", ranges)}.");

            // A prerequisite that excludes a version the target allows makes the
            // install fail on that version, which is only visible at install time.
            foreach (var prerequisite in manifest.Root!.Element(ns + "Prerequisites")?.Elements(ns + "Prerequisite") ?? Enumerable.Empty<XElement>())
            {
                Assert.Equal(ranges[0], prerequisite.Attribute("Version")!.Value);
            }

            // 17.x is Visual Studio 2022 and 18.x is Visual Studio 2026. Excluding
            // either one means a whole generation of Visual Studio cannot install
            // the extension at all -- the exact bug this range already had once.
            Assert.True(Covers(ranges[0], new Version(17, 0)), $"{ranges[0]} excludes Visual Studio 2022.");
            Assert.True(Covers(ranges[0], new Version(18, 8)), $"{ranges[0]} excludes Visual Studio 2026.");
        }

        [Fact]
        public void The_manifest_and_the_assembly_agree_on_the_version()
        {
            var manifestPath = Path.Combine(ExtensionDirectory(), "src", "MauiDesigner.Vsix", "source.extension.vsixmanifest");
            var manifest = XDocument.Load(manifestPath);
            XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";

            var manifestVersion = Version.Parse(
                manifest.Root!.Element(ns + "Metadata")!.Element(ns + "Identity")!.Attribute("Version")!.Value);

            var assemblyInfo = File.ReadAllText(
                Path.Combine(ExtensionDirectory(), "src", "MauiDesigner.Vsix", "Properties", "AssemblyInfo.cs"));

            // Visual Studio decides whether a VSIX is an upgrade purely from the
            // manifest version. Shipping a fix without bumping it leaves everyone
            // who already installed the extension stuck on the broken build, and
            // nothing fails loudly when that happens -- the install is simply
            // refused as "already installed".
            foreach (var attribute in new[] { "AssemblyVersion", "AssemblyFileVersion" })
            {
                var match = Regex.Match(assemblyInfo, $@"{attribute}\(""([^""]+)""\)");
                Assert.True(match.Success, $"AssemblyInfo.cs is missing [assembly: {attribute}].");

                var declared = Version.Parse(match.Groups[1].Value);
                Assert.True(
                    declared.Major == manifestVersion.Major
                        && declared.Minor == manifestVersion.Minor
                        && declared.Build == manifestVersion.Build,
                    $"{attribute} is {declared} but the manifest ships {manifestVersion}.");
            }
        }

        private static bool Covers(string range, Version version)
        {
            var inclusiveLower = range.StartsWith("[", StringComparison.Ordinal);
            var inclusiveUpper = range.EndsWith("]", StringComparison.Ordinal);
            var bounds = range.Trim('[', ']', '(', ')').Split(',');

            var lower = Version.Parse(bounds[0].Trim());
            var lowerOk = inclusiveLower ? version >= lower : version > lower;

            if (bounds.Length == 1 || string.IsNullOrWhiteSpace(bounds[1]))
            {
                return lowerOk;
            }

            var upper = Version.Parse(bounds[1].Trim());
            return lowerOk && (inclusiveUpper ? version <= upper : version < upper);
        }

        private static CustomAttributeData Attribute(Type type, string attributeTypeName)
        {
            var attribute = type.GetCustomAttributesData()
                .FirstOrDefault(candidate => candidate.AttributeType.Name == attributeTypeName);

            Assert.True(attribute is not null, $"{type.Name} is missing [{attributeTypeName}].");
            return attribute!;
        }

        private static string GuidOf(Type type)
        {
            var guid = Attribute(type, "GuidAttribute");
            return (string)guid.ConstructorArguments[0].Value!;
        }

        private static bool InheritsFrom(Type type, string baseTypeFullName)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.FullName == baseTypeFullName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string CompileCheckOutputDirectory()
        {
            var configuration = Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)))!;
            return Path.Combine(ExtensionDirectory(), "tests", "MauiDesigner.Vsix.CompileCheck", "bin", configuration, "net472");
        }

        private static string ExtensionDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && directory.Name != "extension")
            {
                directory = directory.Parent;
            }

            Assert.True(directory is not null, "Could not locate the extension directory from the test output path.");
            return directory!.FullName;
        }
    }
}
