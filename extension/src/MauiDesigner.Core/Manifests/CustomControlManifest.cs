using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiDesigner.Core.Manifests
{
    /// <summary>
    /// C# mirror of <c>src/app/models/custom-control.ts</c>. The designer running
    /// inside the WebView consumes exactly this JSON shape.
    /// </summary>
    public sealed class CustomControlManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>NuGet package id; shown as the toolbox group title.</summary>
        [JsonPropertyName("package")]
        public string Package { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Version { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        [JsonPropertyName("xmlns")]
        public CustomNamespace Xmlns { get; set; } = new CustomNamespace();

        [JsonPropertyName("controls")]
        public List<CustomControlDefinition> Controls { get; set; } = new List<CustomControlDefinition>();
    }

    public sealed class CustomNamespace
    {
        /// <summary>XML prefix used in the document, e.g. <c>toolkit</c>.</summary>
        [JsonPropertyName("prefix")]
        public string Prefix { get; set; } = string.Empty;

        /// <summary>A <c>clr-namespace:</c> declaration.</summary>
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = string.Empty;
    }

    public sealed class CustomControlDefinition
    {
        /// <summary>Local XAML tag without the prefix, e.g. <c>AvatarView</c>.</summary>
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DisplayName { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        [JsonPropertyName("icon")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Icon { get; set; }

        [JsonPropertyName("canHaveChildren")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CanHaveChildren { get; set; }

        [JsonPropertyName("defaultWidth")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DefaultWidth { get; set; }

        [JsonPropertyName("defaultHeight")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DefaultHeight { get; set; }

        [JsonPropertyName("preview")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CustomPreview? Preview { get; set; }

        [JsonPropertyName("properties")]
        public List<CustomPropertyDefinition> Properties { get; set; } = new List<CustomPropertyDefinition>();
    }

    public sealed class CustomPreview
    {
        /// <summary>One of <c>box</c>, <c>text</c>, <c>image</c>, <c>list</c>, <c>slot</c>.</summary>
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "box";

        /// <summary>Supports <c>{PropertyName}</c> placeholders.</summary>
        [JsonPropertyName("label")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Label { get; set; }

        [JsonPropertyName("icon")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Icon { get; set; }
    }

    public sealed class CustomPropertyDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>One of <c>string</c>, <c>number</c>, <c>boolean</c>, <c>color</c>, <c>enum</c>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        [JsonPropertyName("options")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Options { get; set; }

        [JsonPropertyName("bindable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Bindable { get; set; }
    }

    /// <summary>Shared serializer options so the host and the designer agree on casing.</summary>
    public static class ManifestJson
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
    }
}
