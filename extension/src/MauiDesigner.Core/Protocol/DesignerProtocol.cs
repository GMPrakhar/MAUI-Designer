using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using MauiDesigner.Core.Manifests;

namespace MauiDesigner.Core.Protocol
{
    /// <summary>
    /// The message contract between the IDE host and the Angular designer.
    /// Mirrors <c>src/app/services/host-bridge.ts</c>; changing one side requires
    /// changing the other.
    /// </summary>
    public static class MessageTypes
    {
        // Host -> designer
        public const string HostReady = "host.ready";
        public const string DocumentLoad = "document.load";
        public const string ManifestsPush = "manifests.push";
        public const string DocumentSaved = "document.saved";

        // Designer -> host
        public const string DesignerReady = "designer.ready";
        public const string DocumentChanged = "document.changed";
        public const string DocumentSave = "document.save";
        public const string ManifestsRequest = "manifests.request";
        public const string DesignerError = "designer.error";
    }

    /// <summary>A single message in either direction.</summary>
    public sealed class DesignerMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("host")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Host { get; set; }

        [JsonPropertyName("fileName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FileName { get; set; }

        [JsonPropertyName("xaml")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Xaml { get; set; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        [JsonPropertyName("manifests")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CustomControlManifest>? Manifests { get; set; }
    }

    /// <summary>Builds and parses <see cref="DesignerMessage"/> payloads.</summary>
    public static class DesignerProtocol
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string HostReady(string host, string? fileName = null) =>
            Serialize(new DesignerMessage { Type = MessageTypes.HostReady, Host = host, FileName = fileName });

        public static string DocumentLoad(string xaml, string? fileName = null) =>
            Serialize(new DesignerMessage { Type = MessageTypes.DocumentLoad, Xaml = xaml, FileName = fileName });

        public static string ManifestsPush(IEnumerable<CustomControlManifest> manifests) =>
            Serialize(new DesignerMessage
            {
                Type = MessageTypes.ManifestsPush,
                Manifests = new List<CustomControlManifest>(manifests)
            });

        public static string DocumentSaved() => Serialize(new DesignerMessage { Type = MessageTypes.DocumentSaved });

        public static string Serialize(DesignerMessage message) => JsonSerializer.Serialize(message, Options);

        /// <summary>
        /// Parses a message posted by the designer. Returns <c>null</c> instead of
        /// throwing so a malformed payload can never take down the IDE.
        /// </summary>
        public static DesignerMessage? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var message = JsonSerializer.Deserialize<DesignerMessage>(json!, Options);
                return string.IsNullOrEmpty(message?.Type) ? null : message;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
