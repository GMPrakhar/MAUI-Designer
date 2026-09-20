using System.IO;
using System.Text;

namespace MauiDesigner.Vsix
{
    internal static class DesignerToolboxPayload
    {
        public const string DataFormat = "MauiDesigner.ControlType";

        public static string Decode(Stream stream)
        {
            long originalPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);
                return reader.ReadToEnd();
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }
    }
}
