using MAUIDesigner.Fresh.App.Xaml;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class XamlTextIdentityTests
{
    [Theory]
    [InlineData("<Grid>\r\n  <Label />\r\n</Grid>", "<Grid>\n  <Label />\n</Grid>")]
    [InlineData("<Grid>\r  <Label />\r</Grid>", "<Grid>\n  <Label />\n</Grid>")]
    public void Normalization_treats_editor_and_writer_line_endings_as_identical(
        string editorText,
        string writerText)
    {
        Assert.Equal(
            XamlTextIdentity.Normalize(writerText),
            XamlTextIdentity.Normalize(editorText));
    }
}
