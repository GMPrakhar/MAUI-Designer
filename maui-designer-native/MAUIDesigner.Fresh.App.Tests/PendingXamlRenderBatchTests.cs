using MAUIDesigner.Fresh.App.Xaml;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class PendingXamlRenderBatchTests
{
    [Fact]
    public void Later_canvas_change_restores_writeback_after_live_xaml()
    {
        var batch = new PendingXamlRenderBatch();

        batch.RecordDocumentChange(incremental: false, fromXaml: true);
        batch.RecordDocumentChange(incremental: true, fromXaml: false);

        PendingXamlRenderState state = batch.Consume();
        Assert.True(state.DocumentChanged);
        Assert.True(state.FullRebuild);
        Assert.False(state.SuppressXamlWriteback);
    }

    [Fact]
    public void Later_live_xaml_suppresses_an_older_canvas_writeback()
    {
        var batch = new PendingXamlRenderBatch();

        batch.RecordDocumentChange(incremental: true, fromXaml: false);
        batch.RecordDocumentChange(incremental: false, fromXaml: true);

        Assert.True(batch.Consume().SuppressXamlWriteback);
    }

    [Fact]
    public void Consume_resets_every_pending_flag()
    {
        var batch = new PendingXamlRenderBatch();
        batch.RecordDocumentChange(incremental: false, fromXaml: true);
        batch.RecordSelectionChange();

        _ = batch.Consume();

        Assert.Equal(default, batch.Consume());
    }
}
