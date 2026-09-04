namespace MAUIDesigner.Fresh.Core.Geometry;

public readonly record struct PointD(double X, double Y);

public readonly record struct SizeD(double Width, double Height)
{
    public bool IsValid => Width >= 0 && Height >= 0;
}

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double Area => Math.Max(0, Width) * Math.Max(0, Height);

    public bool Contains(PointD point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
}
