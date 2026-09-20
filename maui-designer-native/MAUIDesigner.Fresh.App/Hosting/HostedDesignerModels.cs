using System.Text.Json.Serialization;

namespace MAUIDesigner.Fresh.App.Hosting;

public sealed record HostedToolboxItem(
    [property: JsonPropertyName("controlType")]
    string ControlType,
    [property: JsonPropertyName("displayName")]
    string DisplayName,
    [property: JsonPropertyName("category")]
    string Category);

public sealed record HostedPropertySnapshot(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("value")]
    string? Value,
    [property: JsonPropertyName("valueType")]
    string ValueType,
    [property: JsonPropertyName("category")]
    string Category,
    [property: JsonPropertyName("isReadOnly")]
    bool IsReadOnly);

public sealed record HostedSelectionSnapshot(
    [property: JsonPropertyName("selectionCount")]
    int SelectionCount,
    [property: JsonPropertyName("elementId")]
    string? ElementId,
    [property: JsonPropertyName("displayName")]
    string DisplayName,
    [property: JsonPropertyName("properties")]
    IReadOnlyList<HostedPropertySnapshot> Properties,
    [property: JsonPropertyName("canUndo")]
    bool CanUndo = false,
    [property: JsonPropertyName("canRedo")]
    bool CanRedo = false,
    [property: JsonPropertyName("canCopy")]
    bool CanCopy = false,
    [property: JsonPropertyName("canCut")]
    bool CanCut = false,
    [property: JsonPropertyName("canPaste")]
    bool CanPaste = false,
    [property: JsonPropertyName("canDuplicate")]
    bool CanDuplicate = false,
    [property: JsonPropertyName("canDelete")]
    bool CanDelete = false);

public sealed record HostedDesignerCommand(
    string Name,
    string? ControlType = null,
    string? ElementId = null,
    string? PropertyName = null,
    string? Value = null);
