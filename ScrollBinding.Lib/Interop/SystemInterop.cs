using System.Runtime.InteropServices;
using ScrollBinding.Lib.Enums;

namespace ScrollBinding.Lib.Interop;

public class SystemInterop
{
    protected SystemInterop()
    {
    }

    public static PluginPlatform CurrentPlatform
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return PluginPlatform.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return PluginPlatform.Linux;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return PluginPlatform.MacOS;
            else
                return PluginPlatform.Unknown;
        }
    }
}