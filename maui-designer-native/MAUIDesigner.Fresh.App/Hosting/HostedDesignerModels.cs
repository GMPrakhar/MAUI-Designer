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
    bool IsReadOnly,
    [property: JsonPropertyName("enumValues")]
    IReadOnlyList<string>? EnumValues = null);

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

public sealed record HostedProjectControls(
    [property: JsonPropertyName("target")]
    string Target,
    [property: JsonPropertyName("manifests")]
    IReadOnlyList<HostedControlManifest> Manifests,
    [property: JsonPropertyName("assemblies")]
    IReadOnlyList<HostedRuntimeAssembly> Assemblies,
    [property: JsonPropertyName("startupMethods")]
    IReadOnlyList<HostedStartupMethod> StartupMethods,
    [property: JsonPropertyName("diagnostics")]
    IReadOnlyList<HostedManifestDiagnostic> Diagnostics);

public sealed record HostedControlManifest(
    [property: JsonPropertyName("package")]
    string Package,
    [property: JsonPropertyName("xmlns")]
    HostedControlNamespace Xmlns);

public sealed record HostedControlNamespace(
    [property: JsonPropertyName("uri")]
    string Uri);

public sealed record HostedRuntimeAssembly(
    [property: JsonPropertyName("path")]
    string Path,
    [property: JsonPropertyName("package")]
    string Package,
    [property: JsonPropertyName("isRoot")]
    bool IsRoot);

public sealed record HostedStartupMethod(
    [property: JsonPropertyName("package")]
    string Package,
    [property: JsonPropertyName("assembly")]
    string Assembly,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("method")]
    string Method);

public sealed record HostedManifestDiagnostic(
    [property: JsonPropertyName("package")]
    string Package,
    [property: JsonPropertyName("message")]
    string Message);
