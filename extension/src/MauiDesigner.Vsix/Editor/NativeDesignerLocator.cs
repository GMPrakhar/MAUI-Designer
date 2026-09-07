using System;
using System.IO;
using System.Reflection;

namespace MauiDesigner.Vsix
{
    public static class NativeDesignerLocator
    {
        public static string ExecutablePath
        {
            get
            {
                string? overridePath = Environment.GetEnvironmentVariable(
                    "MAUI_DESIGNER_NATIVE_PATH");
                if (!string.IsNullOrWhiteSpace(overridePath))
                {
                    return Path.GetFullPath(overridePath!);
                }

                string assembly = Assembly.GetExecutingAssembly().Location;
                string directory = Path.GetDirectoryName(assembly) ??
                    AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(directory, "native", "MAUIDesigner.exe");
            }
        }
    }
}
