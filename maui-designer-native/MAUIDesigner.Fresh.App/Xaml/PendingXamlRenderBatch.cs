namespace MAUIDesigner.Fresh.App.Xaml;

public sealed class PendingXamlRenderBatch
{
    public bool DocumentChanged { get; private set; }

    public bool FullRebuild { get; private set; }

    public bool SelectionChanged { get; private set; }

    public bool SuppressXamlWriteback { get; private set; }

    public void RecordDocumentChange(bool incremental, bool fromXaml)
    {
        DocumentChanged = true;
        FullRebuild |= !incremental;
        SuppressXamlWriteback = fromXaml;
    }

    public void RecordSelectionChange() => SelectionChanged = true;

    public PendingXamlRenderState Consume()
    {
        var state = new PendingXamlRenderState(
            DocumentChanged,
            FullRebuild,
            SelectionChanged,
            SuppressXamlWriteback);
        DocumentChanged = false;
        FullRebuild = false;
        SelectionChanged = false;
        SuppressXamlWriteback = false;
        return state;
    }
}

public readonly record struct PendingXamlRenderState(
    bool DocumentChanged,
    bool FullRebuild,
    bool SelectionChanged,
    bool SuppressXamlWriteback);
