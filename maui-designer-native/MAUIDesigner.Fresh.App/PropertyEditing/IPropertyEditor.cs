using MAUIDesigner.Fresh.App.Catalog;

namespace MAUIDesigner.Fresh.App.PropertyEditing;

public interface IPropertyEditor
{
    bool CanEdit(PropertyDescriptor property);

    View Create(PropertyEditorContext context);
}

public sealed record PropertyEditorContext(
    PropertyDescriptor Property,
    string? Value,
    Action<string?> Commit,
    Action<string> ShowError);
