using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            var manifestPath = Path.Combine(ExtensionDirectory(), "src", "MauiDesigner.Vsix", "source.extension.vsixmanifest");
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
                var full = Path.Combine(Path.GetDirectoryName(manifestPath)!, relativePath!.Replace('\\', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(full), $"The manifest references '{relativePath}', which does not exist.");
            }
        }

        [Fact]
        public void The_manifest_targets_visual_studio_2022()
        {
            var manifestPath = Path.Combine(ExtensionDirectory(), "src", "MauiDesigner.Vsix", "source.extension.vsixmanifest");
            var manifest = XDocument.Load(manifestPath);
            XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";

            var identity = manifest.Root!.Element(ns + "Metadata")!.Element(ns + "Identity")!;
            Assert.True(Version.TryParse(identity.Attribute("Version")!.Value, out _));

            var targets = manifest.Root!.Element(ns + "Installation")!.Elements(ns + "InstallationTarget").ToList();
            Assert.NotEmpty(targets);
            Assert.All(targets, target => Assert.Equal("[17.0,18.0)", target.Attribute("Version")!.Value));
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
