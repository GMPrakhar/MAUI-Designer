using System.Collections.Immutable;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Catalog;

public sealed record ControlDescriptor(
    ControlTypeId Id,
    Type RuntimeType,
    string DisplayName,
    string Category,
    bool AcceptsChildren,
    Func<IServiceProvider, View> Factory,
    ImmutableArray<PropertyDescriptor> Properties);

public sealed record PropertyDescriptor(
    string Name,
    Type ValueType,
    bool IsBindable,
    bool IsContent,
    bool IsAttached,
    bool IsReadOnly);
