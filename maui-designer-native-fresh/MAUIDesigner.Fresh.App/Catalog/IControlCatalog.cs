using System.Collections.Immutable;
using System.Reflection;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Catalog;

public interface IControlCatalog
{
    event EventHandler? Changed;

    ImmutableArray<ControlDescriptor> Controls { get; }

    void RegisterAssembly(Assembly assembly);

    void RegisterFactory<TView>(Func<IServiceProvider, TView> factory)
        where TView : View;

    bool TryGet(ControlTypeId id, out ControlDescriptor? descriptor);
}
