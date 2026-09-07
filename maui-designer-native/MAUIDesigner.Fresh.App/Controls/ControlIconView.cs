namespace MAUIDesigner.Fresh.App.Controls;

public sealed class ControlIconView : GraphicsView
{
    public ControlIconView(string controlName)
    {
        WidthRequest = 20;
        HeightRequest = 20;
        InputTransparent = true;
        Drawable = new ControlIconDrawable(ControlIconClassifier.ForControlName(controlName));
    }
}

public enum ControlIconKind
{
    Generic,
    Text,
    Button,
    TextInput,
    Search,
    Check,
    Toggle,
    Slider,
    Calendar,
    Progress,
    Image,
    VerticalLayout,
    HorizontalLayout,
    Grid,
    Border,
    Scroll,
    List
}

public static class ControlIconClassifier
{
    public static ControlIconKind ForControlName(string controlName)
    {
        string name = controlName.ToUpperInvariant();
        if (name.Contains("SEARCH")) return ControlIconKind.Search;
        if (name.Contains("CHECK")) return ControlIconKind.Check;
        if (name.Contains("SWITCH") || name.Contains("TOGGLE")) return ControlIconKind.Toggle;
        if (name.Contains("SLIDER") || name.Contains("STEPPER")) return ControlIconKind.Slider;
        if (name.Contains("DATE") || name.Contains("TIME")) return ControlIconKind.Calendar;
        if (name.Contains("PROGRESS") || name.Contains("ACTIVITY")) return ControlIconKind.Progress;
        if (name.Contains("IMAGE") || name.Contains("MEDIA")) return ControlIconKind.Image;
        if (name.Contains("VERTICAL") || name == "STACKLAYOUT") return ControlIconKind.VerticalLayout;
        if (name.Contains("HORIZONTAL")) return ControlIconKind.HorizontalLayout;
        if (name.Contains("GRID")) return ControlIconKind.Grid;
        if (name.Contains("BORDER") || name.Contains("FRAME")) return ControlIconKind.Border;
        if (name.Contains("SCROLL")) return ControlIconKind.Scroll;
        if (name.Contains("COLLECTION") || name.Contains("LIST")) return ControlIconKind.List;
        if (name.Contains("BUTTON")) return ControlIconKind.Button;
        if (name.Contains("ENTRY") || name.Contains("EDITOR") || name.Contains("PICKER"))
        {
            return ControlIconKind.TextInput;
        }

        if (name.Contains("LABEL") || name.Contains("TEXT")) return ControlIconKind.Text;
        return ControlIconKind.Generic;
    }
}

internal sealed class ControlIconDrawable(ControlIconKind kind) : IDrawable
{
    private static readonly Color Ink = Color.FromArgb("#6554C0");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = Ink;
        canvas.FillColor = Ink;
        canvas.StrokeSize = 1.6f;

        switch (kind)
        {
            case ControlIconKind.Text:
                Line(canvas, 4, 5, 16, 5);
                Line(canvas, 10, 5, 10, 16);
                Line(canvas, 7, 16, 13, 16);
                break;
            case ControlIconKind.Button:
                canvas.DrawRoundedRectangle(2, 4, 16, 12, 3);
                Line(canvas, 6, 10, 14, 10);
                break;
            case ControlIconKind.TextInput:
                canvas.DrawRoundedRectangle(2, 4, 16, 12, 2);
                Line(canvas, 5, 10, 12, 10);
                Line(canvas, 14, 7, 14, 13);
                break;
            case ControlIconKind.Search:
                canvas.DrawCircle(8, 8, 5);
                Line(canvas, 12, 12, 17, 17);
                break;
            case ControlIconKind.Check:
                canvas.DrawRectangle(3, 3, 14, 14);
                Line(canvas, 6, 10, 9, 13);
                Line(canvas, 9, 13, 15, 6);
                break;
            case ControlIconKind.Toggle:
                canvas.DrawRoundedRectangle(2, 6, 16, 8, 4);
                canvas.FillCircle(13, 10, 3);
                break;
            case ControlIconKind.Slider:
                Line(canvas, 2, 10, 18, 10);
                canvas.FillCircle(11, 10, 3);
                break;
            case ControlIconKind.Calendar:
                canvas.DrawRoundedRectangle(3, 4, 14, 13, 2);
                Line(canvas, 3, 8, 17, 8);
                Line(canvas, 7, 2, 7, 6);
                Line(canvas, 13, 2, 13, 6);
                break;
            case ControlIconKind.Progress:
                canvas.DrawCircle(10, 10, 7);
                canvas.FillCircle(10, 3, 2);
                break;
            case ControlIconKind.Image:
                canvas.DrawRoundedRectangle(2, 3, 16, 14, 2);
                canvas.DrawCircle(13, 7, 2);
                Line(canvas, 4, 15, 8, 10);
                Line(canvas, 8, 10, 12, 14);
                break;
            case ControlIconKind.VerticalLayout:
                Box(canvas, 3, 2, 14, 4);
                Box(canvas, 3, 8, 14, 4);
                Box(canvas, 3, 14, 14, 4);
                break;
            case ControlIconKind.HorizontalLayout:
                Box(canvas, 2, 3, 4, 14);
                Box(canvas, 8, 3, 4, 14);
                Box(canvas, 14, 3, 4, 14);
                break;
            case ControlIconKind.Grid:
                Box(canvas, 2, 2, 7, 7);
                Box(canvas, 11, 2, 7, 7);
                Box(canvas, 2, 11, 7, 7);
                Box(canvas, 11, 11, 7, 7);
                break;
            case ControlIconKind.Border:
                canvas.DrawRoundedRectangle(2, 2, 16, 16, 4);
                break;
            case ControlIconKind.Scroll:
                canvas.DrawRoundedRectangle(3, 2, 12, 16, 2);
                Line(canvas, 17, 4, 17, 16);
                Line(canvas, 15, 6, 17, 4);
                Line(canvas, 17, 4, 19, 6);
                break;
            case ControlIconKind.List:
                for (int y = 5; y <= 15; y += 5)
                {
                    canvas.FillCircle(4, y, 1.2f);
                    Line(canvas, 8, y, 17, y);
                }
                break;
            default:
                Box(canvas, 3, 3, 6, 6);
                Box(canvas, 11, 3, 6, 6);
                Box(canvas, 7, 11, 6, 6);
                break;
        }
    }

    private static void Box(ICanvas canvas, float x, float y, float width, float height) =>
        canvas.DrawRoundedRectangle(x, y, width, height, 1.5f);

    private static void Line(ICanvas canvas, float x1, float y1, float x2, float y2) =>
        canvas.DrawLine(x1, y1, x2, y2);
}
