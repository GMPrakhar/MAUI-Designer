using System;
using System.IO;
using System.Reflection;

namespace MauiDesigner.Vsix
{
    /// <summary>Finds the compiled Angular application shipped inside the VSIX.</summary>
    public static class WebAssetLocator
    {
        /// <summary>The folder mapped into WebView2 as a virtual host.</summary>
        public static string WebRootDirectory
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly().Location;
                var directory = Path.GetDirectoryName(assembly) ?? AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(directory, "webview");
            }
        }
    }
}
