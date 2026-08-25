using System.Reflection;
using System.Runtime.InteropServices;

namespace LibSixel.PowerShell.Internal;

internal static class SkiaNativeLibraryLoader
{
    private static readonly Lazy<IntPtr> NativeLibraryHandle = new(
        LoadNativeLibrary,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static void EnsureLoaded()
    {
        _ = NativeLibraryHandle.Value;
    }

    private static IntPtr LoadNativeLibrary()
    {
        string assemblyDirectory = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Unable to locate the PowerShell module directory.");

        string fileName;
        string runtimeDirectory;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            fileName = "libSkiaSharp.dylib";
            runtimeDirectory = "osx";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileName = "libSkiaSharp.dll";
            runtimeDirectory = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => throw UnsupportedPlatform(),
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            fileName = "libSkiaSharp.so";
            bool isMusl = RuntimeInformation.RuntimeIdentifier.Contains(
                "musl",
                StringComparison.OrdinalIgnoreCase);
            runtimeDirectory = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 when isMusl => "linux-musl-x64",
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                Architecture.Arm => "linux-arm",
                _ => throw UnsupportedPlatform(),
            };
        }
        else
        {
            throw UnsupportedPlatform();
        }

        string rootPath = Path.Combine(assemblyDirectory, fileName);
        if (File.Exists(rootPath))
        {
            return NativeLibrary.Load(rootPath);
        }

        string runtimePath = Path.Combine(
            assemblyDirectory,
            "runtimes",
            runtimeDirectory,
            "native",
            fileName);
        if (File.Exists(runtimePath))
        {
            return NativeLibrary.Load(runtimePath);
        }

        throw new DllNotFoundException(
            $"SkiaSharp native library was not found at '{rootPath}' or '{runtimePath}'.");
    }

    private static PlatformNotSupportedException UnsupportedPlatform()
    {
        return new PlatformNotSupportedException(
            $"SkiaSharp is not packaged for {RuntimeInformation.OSDescription} " +
            $"on {RuntimeInformation.ProcessArchitecture}.");
    }
}
