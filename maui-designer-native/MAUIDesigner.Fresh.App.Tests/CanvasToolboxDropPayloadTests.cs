using MAUIDesigner.Fresh.App.Controls;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class CanvasToolboxDropPayloadTests
{
    [Fact]
    public void Parses_the_cross_process_toolbox_stream_payload()
    {
        Assert.Equal(
            "Microsoft.Maui.Controls.Button",
            CanvasToolboxDropPayload.ParseUtf8(
                System.Text.Encoding.UTF8.GetBytes(
                    "Microsoft.Maui.Controls.Button")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejects_incomplete_payload(string value) =>
        Assert.Null(CanvasToolboxDropPayload.ParseUtf8(
            System.Text.Encoding.UTF8.GetBytes(value)));
}
