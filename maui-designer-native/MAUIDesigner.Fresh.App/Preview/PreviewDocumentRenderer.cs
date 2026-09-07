using System.Reflection;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Rendering;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Preview;

public sealed class PreviewDocumentRenderer
{
    private readonly IControlCatalog _catalog;
    private readonly LayoutAdapterRegistry _layoutAdapters = new();

    public PreviewDocumentRenderer(IControlCatalog catalog)
    {
        _catalog = catalog;
    }

    public View Materialize(DesignerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        return Build(document.Root);
    }

    private View Build(DesignerNode node)
    {
        if (!_catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor) ||
            descriptor is null)
        {
            return CreatePlaceholder(
                node,
                $"Control type '{node.ControlType.XamlName}' is not registered.");
        }

        View view;
        try
        {
            view = _catalog.Create(node.ControlType);
            ApplyProperties(view, descriptor, node);
            view.AutomationId = $"preview-{node.Id.Value}";
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return CreatePlaceholder(node, GetReason(exception));
        }

        ILayoutAdapter? adapter;
        try
        {
            adapter = descriptor.AcceptsChildren
                ? _layoutAdapters.Resolve(descriptor)
                : null;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return CreatePlaceholder(node, GetReason(exception));
        }

        foreach (DesignerNode childNode in node.Children)
        {
            View child = Build(childNode);
            try
            {
                if (childNode.ParentPropertyName is string propertyName)
                {
                    SetVisualProperty(view, propertyName, child);
                }
                else if (adapter is not null)
                {
                    adapter.AddChild(view, child, childNode);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"'{node.ControlType.XamlName}' does not accept visual children.");
                }
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                TryAddAttachmentPlaceholder(
                    view,
                    adapter,
                    childNode,
                    CreatePlaceholder(childNode, GetReason(exception)));
            }
        }

        return view;
    }

    private static void ApplyProperties(
        View view,
        ControlDescriptor descriptor,
        DesignerNode node)
    {
        foreach ((string name, DesignerValue designerValue) in node.Properties)
        {
            if (!TryGetPreviewText(designerValue, out string text))
            {
                continue;
            }

            PropertyDescriptor? propertyDescriptor = descriptor.Properties.FirstOrDefault(
                property =>
                    string.Equals(property.Name, name, StringComparison.Ordinal) &&
                    !property.IsReadOnly);
            if (propertyDescriptor is null ||
                !DesignerValueConverter.TryConvert(
                    text,
                    propertyDescriptor.ValueType,
                    out object? value))
            {
                continue;
            }

            if (GetWritableProperties(descriptor.RuntimeType)
                .TryGetValue(name, out PropertyInfo? property))
            {
                property.SetValue(view, value);
            }
        }
    }

    private static IReadOnlyDictionary<string, PropertyInfo> GetWritableProperties(Type type) =>
        RuntimePropertyCache.GetWritableProperties(type);

    private static bool TryGetPreviewText(DesignerValue value, out string text)
    {
        if (value.Kind == DesignerValueKind.Literal)
        {
            text = value.Text;
            return true;
        }

        if (value.Kind == DesignerValueKind.MarkupExtension &&
            DesignerMarkupPreview.TryGetLiteral(value.Text, out string preview))
        {
            text = preview;
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static void SetVisualProperty(View parent, string propertyName, View child)
    {
        PropertyInfo property = VisualContentProperty.FindAll(parent.GetType())
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, propertyName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Visual property '{propertyName}' is unavailable on '{parent.GetType().FullName}'.");
        property.SetValue(parent, child);
    }

    private static void TryAddAttachmentPlaceholder(
        View parent,
        ILayoutAdapter? adapter,
        DesignerNode childNode,
        View placeholder)
    {
        try
        {
            if (childNode.ParentPropertyName is string propertyName)
            {
                SetVisualProperty(parent, propertyName, placeholder);
            }
            else
            {
                adapter?.AddChild(parent, placeholder, childNode);
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }
    }

    private static Border CreatePlaceholder(DesignerNode node, string reason) =>
        new()
        {
            AutomationId = $"preview-error-{node.Id.Value}",
            Padding = 12,
            Margin = 4,
            BackgroundColor = Color.FromArgb("#FFF1F2"),
            Stroke = Color.FromArgb("#E11D48"),
            StrokeThickness = 1,
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = $"Could not render {node.ControlType.XamlName}",
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#9F1239")
                    },
                    new Label
                    {
                        Text = reason,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#881337"),
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                }
            }
        };

    private static string GetReason(Exception exception) =>
        exception.InnerException?.Message ?? exception.Message;

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException and
        not AppDomainUnloadedException and
        not BadImageFormatException;
}
