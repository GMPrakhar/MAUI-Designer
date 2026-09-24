using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using MauiDesigner.Core.Manifests;

namespace MauiDesigner.Core.Protocol
{
    /// <summary>
    /// The message contract between an IDE host and a designer process.
    /// </summary>
    public static class MessageTypes
    {
        // Host -> designer
        public const string HostReady = "host.ready";
        public const string DocumentLoad = "document.load";
        public const string ManifestsPush = "manifests.push";
        public const string DocumentSaved = "document.saved";
        public const string HostClose = "host.close";
        public const string HostCommand = "host.command";
        public const string DocumentApplied = "document.applied";

        // Designer -> host
        public const string DesignerReady = "designer.ready";
        public const string DocumentChanged = "document.changed";
        public const string DocumentSave = "document.save";
        public const string ManifestsRequest = "manifests.request";
        public const string DesignerError = "designer.error";
        public const string DesignerClosed = "designer.closed";
        public const string DesignerFocusChanged = "designer.focusChanged";
        public const string DesignerToolboxChanged = "designer.toolboxChanged";
        public const string DesignerSelectionChanged = "designer.selectionChanged";
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

        [JsonPropertyName("requestId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RequestId { get; set; }

        [JsonPropertyName("revision")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Revision { get; set; }

        [JsonPropertyName("command")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Command { get; set; }

        [JsonPropertyName("textInputFocused")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? TextInputFocused { get; set; }

        [JsonPropertyName("manifests")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CustomControlManifest>? Manifests { get; set; }

        [JsonPropertyName("toolboxItems")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DesignerToolboxItem>? ToolboxItems { get; set; }

        [JsonPropertyName("selection")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DesignerSelectionSnapshot? Selection { get; set; }

        [JsonPropertyName("controlType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ControlType { get; set; }

        [JsonPropertyName("elementId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ElementId { get; set; }

        [JsonPropertyName("propertyName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PropertyName { get; set; }

        [JsonPropertyName("value")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Value { get; set; }
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

        public static string HostClose(string requestId) =>
            Serialize(new DesignerMessage { Type = MessageTypes.HostClose, RequestId = requestId });

        public static string HostCommand(string command) =>
            Serialize(new DesignerMessage { Type = MessageTypes.HostCommand, Command = command });

        public static string HostInsertControl(string controlType) =>
            Serialize(new DesignerMessage
            {
                Type = MessageTypes.HostCommand,
                Command = "insertControl",
                ControlType = controlType
            });

        public static string HostSetProperty(
            string elementId,
            string propertyName,
            string? value) =>
            Serialize(new DesignerMessage
            {
                Type = MessageTypes.HostCommand,
                Command = "setProperty",
                ElementId = elementId,
                PropertyName = propertyName,
                Value = value
            });

        public static string DocumentApplied(long revision) =>
            Serialize(new DesignerMessage
            {
                Type = MessageTypes.DocumentApplied,
                Revision = revision
            });

        public static string DesignerFocusChanged(bool textInputFocused) =>
            Serialize(new DesignerMessage
            {
                Type = MessageTypes.DesignerFocusChanged,
                TextInputFocused = textInputFocused
            });

        public static bool IsCloseResponseFor(DesignerMessage? message, string requestId) =>
            message?.Type == MessageTypes.DesignerClosed &&
            message.RequestId == requestId;

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

    public sealed class DesignerToolboxItem
    {
        [JsonPropertyName("controlType")]
        public string ControlType { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;
    }

    public sealed class DesignerSelectionSnapshot
    {
        [JsonPropertyName("selectionCount")]
        public int SelectionCount { get; set; }

        [JsonPropertyName("elementId")]
        public string? ElementId { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public List<DesignerPropertySnapshot> Properties { get; set; } =
            new List<DesignerPropertySnapshot>();

        [JsonPropertyName("canUndo")]
        public bool CanUndo { get; set; }

        [JsonPropertyName("canRedo")]
        public bool CanRedo { get; set; }

        [JsonPropertyName("canCopy")]
        public bool CanCopy { get; set; }

        [JsonPropertyName("canCut")]
        public bool CanCut { get; set; }

        [JsonPropertyName("canPaste")]
        public bool CanPaste { get; set; }

        [JsonPropertyName("canDuplicate")]
        public bool CanDuplicate { get; set; }

        [JsonPropertyName("canDelete")]
        public bool CanDelete { get; set; }
    }

    public sealed class DesignerPropertySnapshot
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("valueType")]
        public string ValueType { get; set; } = typeof(string).FullName;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("isReadOnly")]
        public bool IsReadOnly { get; set; }

        [JsonPropertyName("enumValues")]
        public List<string>? EnumValues { get; set; }
    }
}
