using MAUIDesigner.Fresh.App.Viewport;
using Microsoft.Maui.Graphics;

namespace MAUIDesigner.Fresh.App.Rendering;

public sealed class CanvasGridDrawable : IDrawable
{
    private readonly DesignerViewportState _viewport;

    public CanvasGridDrawable(DesignerViewportState viewport)
    {
        _viewport = viewport;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!_viewport.ShowGrid)
        {
            return;
        }

        canvas.StrokeColor = _viewport.IsDarkPreview
            ? Color.FromArgb("#263247")
            : Color.FromArgb("#E5E7EB");
        canvas.StrokeSize = (float)(1 / _viewport.Zoom);
        for (double x = 0; x <= _viewport.DesignWidth; x += _viewport.GridSize)
        {
            canvas.DrawLine((float)x, 0, (float)x, (float)_viewport.DesignHeight);
        }

        for (double y = 0; y <= _viewport.DesignHeight; y += _viewport.GridSize)
        {
            canvas.DrawLine(0, (float)y, (float)_viewport.DesignWidth, (float)y);
        }
    }
}

public sealed class CanvasRulerDrawable : IDrawable
{
    private const float RulerSize = 22;
    private readonly DesignerViewportState _viewport;

    public CanvasRulerDrawable(DesignerViewportState viewport)
    {
        _viewport = viewport;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!_viewport.ShowRulers)
        {
            return;
        }

        canvas.FillColor = _viewport.IsDarkPreview
            ? Color.FromArgb("#1E293B")
            : Color.FromArgb("#F8FAFC");
        canvas.FillRectangle(0, 0, dirtyRect.Width, RulerSize);
        canvas.FillRectangle(0, 0, RulerSize, dirtyRect.Height);
        canvas.StrokeColor = _viewport.IsDarkPreview
            ? Color.FromArgb("#475569")
            : Color.FromArgb("#CBD5E1");
        canvas.FontColor = _viewport.IsDarkPreview
            ? Color.FromArgb("#CBD5E1")
            : Color.FromArgb("#475569");
        canvas.FontSize = 8;

        double step = Math.Max(_viewport.GridSize * 5, 40);
        for (double value = 0; value <= _viewport.DesignWidth; value += step)
        {
            float x = (float)(_viewport.PanX + value * _viewport.Zoom);
            if (x < RulerSize || x > dirtyRect.Width)
            {
                continue;
            }

            canvas.DrawLine(x, 14, x, RulerSize);
            canvas.DrawString(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x + 2,
                0,
                42,
                14,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);
        }

        for (double value = 0; value <= _viewport.DesignHeight; value += step)
        {
            float y = (float)(_viewport.PanY + value * _viewport.Zoom);
            if (y < RulerSize || y > dirtyRect.Height)
            {
                continue;
            }

            canvas.DrawLine(14, y, RulerSize, y);
            canvas.DrawString(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                1,
                y + 1,
                20,
                12,
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
        }

        canvas.FillColor = _viewport.IsDarkPreview
            ? Color.FromArgb("#172033")
            : Color.FromArgb("#EEF2F7");
        canvas.FillRectangle(0, 0, RulerSize, RulerSize);
    }
}
