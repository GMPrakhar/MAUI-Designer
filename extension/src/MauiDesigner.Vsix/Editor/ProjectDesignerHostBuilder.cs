using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MauiDesigner.Core.Manifests;

namespace MauiDesigner.Vsix
{
    internal static class ProjectDesignerHostBuilder
    {
        private const string TargetFramework = "net10.0-windows10.0.19041.0";
        private const string OverlayFormatVersion = "5";
        private static readonly SemaphoreSlim BuildGate = new SemaphoreSlim(1, 1);

        internal sealed class Result
        {
            public Result(string executablePath, bool usesProjectPackages)
            {
                ExecutablePath = executablePath;
                UsesProjectPackages = usesProjectPackages;
            }

            public string ExecutablePath { get; }

            public bool UsesProjectPackages { get; }
        }

        public static async Task<Result> ResolveAsync(
            string nativeDesignerPath,
            ProjectControlManifest projectControls)
        {
            if (projectControls.Packages.Count == 0)
            {
                return new Result(nativeDesignerPath, false);
            }

            string nativeDirectory = Path.GetDirectoryName(nativeDesignerPath)
                ?? throw new InvalidOperationException(
                    "The native designer path has no parent directory.");
            string designerAssembly = Path.Combine(nativeDirectory, "MAUIDesigner.dll");
            string coreAssembly = Path.Combine(
                nativeDirectory,
                "MAUIDesigner.Fresh.Core.dll");
            if (!File.Exists(designerAssembly) || !File.Exists(coreAssembly))
            {
                throw new InvalidOperationException(
                    "The native designer runtime assemblies are incomplete.");
            }

            string key = ComputeKey(designerAssembly, projectControls);
            string hostRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MauiDesigner",
                "ProjectHosts",
                key);
            string runtimeRoot = Path.Combine(hostRoot, "runtime");
            string executablePath = Path.Combine(
                runtimeRoot,
                "MAUIDesigner.exe");
            string completionMarker = Path.Combine(runtimeRoot, ".complete");
            string buildOutput = Path.Combine(
                hostRoot,
                "build",
                "bin",
                "Release",
                TargetFramework,
                "win-x64");
            if (File.Exists(executablePath) &&
                File.Exists(completionMarker))
            {
                return new Result(executablePath, true);
            }

            await BuildGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!File.Exists(executablePath) ||
                    !File.Exists(completionMarker))
                {
                    PrepareProject(
                        hostRoot,
                        designerAssembly,
                        coreAssembly,
                        Path.Combine(nativeDirectory, "MAUIDesigner.pri"),
                        projectControls.Packages);
                    await BuildAsync(hostRoot).ConfigureAwait(false);
                    if (Directory.Exists(runtimeRoot))
                    {
                        Directory.Delete(runtimeRoot, true);
                    }

                    StageRuntime(
                        nativeDirectory,
                        buildOutput,
                        runtimeRoot);
                    File.WriteAllText(
                        completionMarker,
                        DateTimeOffset.UtcNow.ToString("O"),
                        new UTF8Encoding(false));
                }
            }
            finally
            {
                BuildGate.Release();
            }

            if (!File.Exists(executablePath))
            {
                throw new InvalidOperationException(
                    "The project-specific MAUI Designer host build completed " +
                    "without producing an executable.");
            }

            return new Result(executablePath, true);
        }

        private static string ComputeKey(
            string designerAssembly,
            ProjectControlManifest projectControls)
        {
            var identity = new StringBuilder(OverlayFormatVersion)
                .Append('\n')
                .Append(projectControls.Target);
            foreach (RuntimePackageDefinition package in projectControls.Packages
                         .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase))
            {
                identity.Append('\n')
                    .Append(package.Id)
                    .Append('/')
                    .Append(package.Version);
            }

            using (var hash = SHA256.Create())
            {
                byte[] assemblyHash;
                using (var stream = File.OpenRead(designerAssembly))
                {
                    assemblyHash = hash.ComputeHash(stream);
                }

                identity.Append('\n').Append(Convert.ToBase64String(assemblyHash));
                byte[] key = hash.ComputeHash(
                    Encoding.UTF8.GetBytes(identity.ToString()));
                return string.Concat(key.Take(16).Select(value => value.ToString("x2")));
            }
        }

        private static void PrepareProject(
            string hostRoot,
            string designerAssembly,
            string coreAssembly,
            string designerPri,
            IReadOnlyList<RuntimePackageDefinition> packages)
        {
            string buildRoot = Path.Combine(hostRoot, "build");
            string libraryDirectory = Path.Combine(buildRoot, "lib");
            string windowsDirectory = Path.Combine(
                buildRoot,
                "Platforms",
                "Windows");
            Directory.CreateDirectory(libraryDirectory);
            Directory.CreateDirectory(windowsDirectory);
            File.Copy(
                designerAssembly,
                Path.Combine(libraryDirectory, "MAUIDesigner.dll"),
                true);
            File.Copy(
                coreAssembly,
                Path.Combine(libraryDirectory, "MAUIDesigner.Fresh.Core.dll"),
                true);

            File.WriteAllText(
                Path.Combine(buildRoot, "MauiDesigner.ProjectHost.csproj"),
                CreateProject(packages, designerPri),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(buildRoot, "Program.cs"),
                CreateProgramCode(),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(windowsDirectory, "App.xaml.cs"),
                CreateApplicationCode(),
                new UTF8Encoding(false));
        }

        private static string CreateProject(
            IReadOnlyList<RuntimePackageDefinition> packages,
            string designerPri)
        {
            var references = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["CommunityToolkit.Maui"] = "15.0.1",
                ["Microsoft.Extensions.Logging.Debug"] = "10.0.0",
                ["Microsoft.Maui.Controls"] = "10.0.90"
            };
            foreach (RuntimePackageDefinition package in packages)
            {
                if (!string.IsNullOrWhiteSpace(package.Id) &&
                    !string.IsNullOrWhiteSpace(package.Version))
                {
                    references[package.Id] = package.Version;
                }
            }

            var packageReferences = new StringBuilder();
            foreach (KeyValuePair<string, string> reference in references
                         .OrderBy(reference => reference.Key, StringComparer.OrdinalIgnoreCase))
            {
                packageReferences.Append("    <PackageReference Include=\"")
                    .Append(SecurityElement.Escape(reference.Key))
                    .Append("\" Version=\"")
                    .Append(SecurityElement.Escape(reference.Value))
                    .AppendLine("\" />");
            }

            return
                "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n" +
                "  <PropertyGroup>\r\n" +
                $"    <TargetFramework>{TargetFramework}</TargetFramework>\r\n" +
                "    <OutputType>Exe</OutputType>\r\n" +
                "    <AssemblyName>MAUIDesigner</AssemblyName>\r\n" +
                "    <ProjectPriIndexName>MAUIDesigner</ProjectPriIndexName>\r\n" +
                "    <ProjectPriFileName>MAUIDesigner.merged.pri</ProjectPriFileName>\r\n" +
                "    <UseMaui>true</UseMaui>\r\n" +
                "    <SingleProject>true</SingleProject>\r\n" +
                "    <ImplicitUsings>enable</ImplicitUsings>\r\n" +
                "    <Nullable>enable</Nullable>\r\n" +
                "    <WindowsPackageType>None</WindowsPackageType>\r\n" +
                "    <RuntimeIdentifier>win-x64</RuntimeIdentifier>\r\n" +
                "    <SelfContained>true</SelfContained>\r\n" +
                "    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>\r\n" +
                "    <ApplicationTitle>MAUI Designer</ApplicationTitle>\r\n" +
                "    <ApplicationId>com.gmprakhar.mauidesigner.projecthost</ApplicationId>\r\n" +
                "    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>\r\n" +
                "    <ApplicationVersion>1</ApplicationVersion>\r\n" +
                "    <SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>\r\n" +
                "    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>\r\n" +
                "  </PropertyGroup>\r\n" +
                "  <ItemGroup>\r\n" +
                packageReferences +
                "  </ItemGroup>\r\n" +
                "  <ItemGroup>\r\n" +
                "    <_PriFile Include=\"" +
                SecurityElement.Escape(designerPri) +
                "\" />\r\n" +
                "    <Reference Include=\"MAUIDesigner\">\r\n" +
                "      <HintPath>lib\\MAUIDesigner.dll</HintPath>\r\n" +
                "      <Private>true</Private>\r\n" +
                "    </Reference>\r\n" +
                "    <Reference Include=\"MAUIDesigner.Fresh.Core\">\r\n" +
                "      <HintPath>lib\\MAUIDesigner.Fresh.Core.dll</HintPath>\r\n" +
                "      <Private>true</Private>\r\n" +
                "    </Reference>\r\n" +
                "  </ItemGroup>\r\n" +
                "</Project>\r\n";
        }

        private static string CreateProgramCode() =>
            "namespace MAUIDesigner.ProjectHost;\r\n" +
            "\r\n" +
            "internal static class Program\r\n" +
            "{\r\n" +
            "    private static void Main()\r\n" +
            "    {\r\n" +
            "    }\r\n" +
            "}\r\n";

        private static string CreateApplicationCode() =>
            "using Microsoft.UI.Xaml;\r\n" +
            "\r\n" +
            "namespace MAUIDesigner.ProjectHost.WinUI;\r\n" +
            "\r\n" +
            "public sealed class App : MauiWinUIApplication\r\n" +
            "{\r\n" +
            "    protected override MauiApp CreateMauiApp() =>\r\n" +
            "        MAUIDesigner.Fresh.App.MauiProgram.CreateMauiApp();\r\n" +
            "}\r\n";

        private static async Task BuildAsync(string hostRoot)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments =
                    "build \"MauiDesigner.ProjectHost.csproj\" " +
                    "-c Release -r win-x64 --nologo",
                WorkingDirectory = Path.Combine(hostRoot, "build"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
                string standardOutput = await output.ConfigureAwait(false);
                string standardError = await error.ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The project-specific MAUI Designer host could not be built. " +
                        LastBuildMessage(standardError, standardOutput));
                }
            }
        }

        private static void StageRuntime(
            string nativeDirectory,
            string buildOutput,
            string runtimeRoot)
        {
            Directory.CreateDirectory(runtimeRoot);
            LinkRuntimeFiles(nativeDirectory, nativeDirectory, runtimeRoot);

            File.Copy(
                Path.Combine(buildOutput, "MAUIDesigner.merged.pri"),
                Path.Combine(runtimeRoot, "MAUIDesigner.pri"),
                true);
        }

        private static void LinkRuntimeFiles(
            string root,
            string current,
            string destinationRoot)
        {
            foreach (string file in Directory.EnumerateFiles(current))
            {
                string relativePath = file.Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar);
                if (relativePath.Equals(
                        "MAUIDesigner.pri",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string destination = Path.Combine(destinationRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!CreateHardLink(destination, file, IntPtr.Zero))
                {
                    File.Copy(file, destination, true);
                }
            }

            foreach (string directory in Directory.EnumerateDirectories(current))
            {
                if (Path.GetFileName(directory).Equals(
                        "publish",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                LinkRuntimeFiles(root, directory, destinationRoot);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(
            string fileName,
            string existingFileName,
            IntPtr securityAttributes);

        private static string LastBuildMessage(params string[] output)
        {
            string[] lines = output
                .SelectMany(value => value.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries))
                .Where(line => line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            return lines.Length > 0
                ? lines[lines.Length - 1].Trim()
                : "Run dotnet workload restore for the project and try again.";
        }
    }
}
