using System.Collections.Generic;

using MauiDesigner.Core.Protocol;

using Xunit;

namespace MauiDesigner.Core.Tests
{
    public sealed class DesignerGridDefinitionsTests
    {
        [Theory]
        [InlineData("Auto,*,2*,120", "Auto,*,2*,120")]
        [InlineData("auto, 1*, 80.5", "Auto,*,80.5")]
        [InlineData("", "")]
        public void Parses_and_serializes_canonical_grid_tracks(
            string input,
            string expected)
        {
            Assert.True(DesignerGridDefinitions.TryParse(
                input,
                out IReadOnlyList<DesignerGridTrack> tracks));

            Assert.Equal(expected, DesignerGridDefinitions.Serialize(tracks));
        }

        [Theory]
        [InlineData("Auto,,*")]
        [InlineData("-1")]
        [InlineData("NaN*")]
        [InlineData("not-a-size")]
        public void Rejects_invalid_grid_tracks(string input)
        {
            Assert.False(DesignerGridDefinitions.TryParse(input, out _));
        }
    }
}
