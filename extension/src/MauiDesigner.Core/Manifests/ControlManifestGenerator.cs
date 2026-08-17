using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using MauiDesigner.Core.Manifests;

namespace MauiDesigner.Core.Manifests
{
    /// <summary>
    /// Scans NuGet assemblies for MAUI controls and produces designer manifests.
    /// Uses <see cref="MetadataLoadContext"/> so assemblies are inspected, never executed.
    /// </summary>
    public sealed class ControlManifestGenerator
    {
        private const string ViewTypeName = "Microsoft.Maui.Controls.View";
        private const string LayoutTypeName = "Microsoft.Maui.Controls.Layout";
        private const string BindablePropertyTypeName = "Microsoft.Maui.Controls.BindableProperty";

        /// <summary>
        /// Builds one manifest per CLR namespace found in <paramref name="assemblyPaths"/>.
        /// </summary>
        /// <param name="assemblyPaths">Assemblies to scan.</param>
        /// <param name="referencePaths">Additional assemblies needed to resolve base types (MAUI, BCL).</param>
        /// <param name="packageId">NuGet package the assemblies came from.</param>
        /// <param name="packageVersion">NuGet package version.</param>
        public IReadOnlyList<CustomControlManifest> Generate(
            IEnumerable<string> assemblyPaths,
            IEnumerable<string> referencePaths,
            string packageId,
            string? packageVersion = null)
        {
            var targets = assemblyPaths.Where(File.Exists).Distinct().ToList();
            if (targets.Count == 0)
            {
                return Array.Empty<CustomControlManifest>();
            }

            var all = targets
                .Concat(referencePaths.Where(File.Exists))
                .Distinct()
                .ToList();

            var resolver = new PathAssemblyResolver(all);
            using var context = new MetadataLoadContext(resolver);

            var manifests = new Dictionary<string, CustomControlManifest>(StringComparer.Ordinal);

            foreach (var path in targets)
            {
                Assembly assembly;
                try
                {
                    assembly = context.LoadFromAssemblyPath(path);
                }
                catch (BadImageFormatException)
                {
                    // Native or resource-only assembly: nothing to scan.
                    continue;
                }

                var assemblyName = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(path);

                foreach (var type in SafeGetTypes(assembly))
                {
                    if (!IsDesignableControl(type))
                    {
                        continue;
                    }

                    var clrNamespace = type.Namespace ?? string.Empty;
                    var key = clrNamespace + "|" + assemblyName;

                    if (!manifests.TryGetValue(key, out var manifest))
                    {
                        manifest = new CustomControlManifest
                        {
                            Id = $"{packageId}.{clrNamespace}".ToLowerInvariant(),
                            Package = packageId,
                            Version = packageVersion,
                            Description = $"Discovered in {packageId}",
                            Xmlns = new CustomNamespace
                            {
                                Prefix = SuggestPrefix(clrNamespace, packageId),
                                Uri = $"clr-namespace:{clrNamespace};assembly={assemblyName}"
                            }
                        };
                        manifests[key] = manifest;
                    }

                    manifest.Controls.Add(Describe(type));
                }
            }

            foreach (var manifest in manifests.Values)
            {
                manifest.Controls.Sort((left, right) => string.CompareOrdinal(left.Tag, right.Tag));
            }

            return manifests.Values
                .Where(manifest => manifest.Controls.Count > 0)
                .OrderBy(manifest => manifest.Xmlns.Uri, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>A type is designable when it is a public, concrete MAUI <c>View</c>.</summary>
        public static bool IsDesignableControl(Type type)
        {
            if (!type.IsPublic || type.IsAbstract || type.IsGenericTypeDefinition || type.IsInterface)
            {
                return false;
            }

            return InheritsFrom(type, ViewTypeName);
        }

        private static CustomControlDefinition Describe(Type type)
        {
            var isLayout = InheritsFrom(type, LayoutTypeName);

            return new CustomControlDefinition
            {
                Tag = type.Name,
                DisplayName = Humanize(type.Name),
                Description = $"{type.FullName}",
                Icon = isLayout ? "dashboard" : "widgets",
                CanHaveChildren = isLayout ? true : (bool?)null,
                DefaultWidth = isLayout ? 240 : 160,
                DefaultHeight = isLayout ? 160 : 40,
                Preview = new CustomPreview
                {
                    Kind = isLayout ? "slot" : "box",
                    Label = Humanize(type.Name)
                },
                Properties = BindableProperties(type)
            };
        }

        /// <summary>
        /// MAUI controls expose their editable surface as
        /// <c>public static readonly BindableProperty XxxProperty</c> fields.
        /// </summary>
        public static List<CustomPropertyDefinition> BindableProperties(Type type)
        {
            var properties = new List<CustomPropertyDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                if (!field.IsInitOnly ||
                    field.FieldType.FullName != BindablePropertyTypeName ||
                    !field.Name.EndsWith("Property", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = field.Name.Substring(0, field.Name.Length - "Property".Length);
                if (name.Length == 0 || !seen.Add(name))
                {
                    continue;
                }

                var clrProperty = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                properties.Add(new CustomPropertyDefinition
                {
                    Name = name,
                    Type = MapType(clrProperty?.PropertyType),
                    Options = EnumOptions(clrProperty?.PropertyType)
                });
            }

            properties.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return properties;
        }

        /// <summary>Maps a CLR type onto the designer's property editor kinds.</summary>
        public static string MapType(Type? type)
        {
            if (type is null)
            {
                return "string";
            }

            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying.IsEnum)
            {
                return "enum";
            }

            switch (underlying.FullName)
            {
                case "System.Boolean":
                    return "boolean";
                case "System.Double":
                case "System.Single":
                case "System.Int32":
                case "System.Int64":
                case "System.Decimal":
                    return "number";
                case "Microsoft.Maui.Graphics.Color":
                    return "color";
                default:
                    return "string";
            }
        }

        private static List<string>? EnumOptions(Type? type)
        {
            var underlying = type is null ? null : Nullable.GetUnderlyingType(type) ?? type;
            if (underlying is null || !underlying.IsEnum)
            {
                return null;
            }

            try
            {
                return underlying.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(field => field.Name)
                    .ToList();
            }
            catch (NotSupportedException)
            {
                // Enum values cannot be read in a metadata-only context for some shapes.
                return null;
            }
        }

        /// <summary>Derives a short, readable XML prefix, e.g. `Syncfusion.Maui.Inputs` -&gt; `inputs`.</summary>
        public static string SuggestPrefix(string clrNamespace, string packageId)
        {
            var source = string.IsNullOrWhiteSpace(clrNamespace) ? packageId : clrNamespace;
            var segments = source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !segment.Equals("Maui", StringComparison.OrdinalIgnoreCase) &&
                                  !segment.Equals("Controls", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var candidate = segments.Count > 0 ? segments[segments.Count - 1] : source;
            var cleaned = new string(candidate.Where(char.IsLetterOrDigit).ToArray());

            return cleaned.Length == 0 ? "custom" : cleaned.ToLowerInvariant();
        }

        /// <summary>`SfComboBox` -&gt; `Sf Combo Box`.</summary>
        public static string Humanize(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            var builder = new System.Text.StringBuilder();
            for (var index = 0; index < name.Length; index++)
            {
                var current = name[index];
                if (index > 0 && char.IsUpper(current) && !char.IsUpper(name[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool InheritsFrom(Type type, string baseTypeFullName)
        {
            try
            {
                for (var current = type.BaseType; current is not null; current = current.BaseType)
                {
                    if (current.FullName == baseTypeFullName)
                    {
                        return true;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // A base type lives in an assembly that was not supplied; treat as unrelated.
            }
            catch (TypeLoadException)
            {
            }

            return false;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException error)
            {
                return error.Types.Where(type => type is not null)!;
            }
        }
    }
}
