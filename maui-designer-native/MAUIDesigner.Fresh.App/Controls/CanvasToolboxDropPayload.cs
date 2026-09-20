using System.Text;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

using WindowsDataPackageView =
    Windows.ApplicationModel.DataTransfer.DataPackageView;

namespace MAUIDesigner.Fresh.App.Controls;

public static class CanvasToolboxDropPayload
{
    public const string DataFormat = "MauiDesigner.ControlType";

    public static bool CanRead(WindowsDataPackageView data) =>
        data.Contains(DataFormat);

    public static async Task<string?> ReadControlTypeAsync(
        WindowsDataPackageView data)
    {
        if (!data.Contains(DataFormat))
        {
            return null;
        }

        object value = await data.GetDataAsync(DataFormat);
        if (value is string text)
        {
            return Normalize(text);
        }

        if (value is not IRandomAccessStream stream)
        {
            return null;
        }

        if (stream.Size > int.MaxValue)
        {
            throw new InvalidDataException(
                "The Toolbox payload is too large.");
        }

        using IInputStream input = stream.GetInputStreamAt(0);
        using var reader = new DataReader(input);
        uint length = checked((uint)stream.Size);
        await reader.LoadAsync(length);
        var bytes = new byte[length];
        reader.ReadBytes(bytes);
        return ParseUtf8(bytes);
    }

    public static string? ParseUtf8(byte[] value) =>
        Normalize(Encoding.UTF8.GetString(value));

    private static string? Normalize(string? value)
    {
        string controlType = value?.Trim() ?? string.Empty;
        return controlType.Length == 0 ? null : controlType;
    }
}
