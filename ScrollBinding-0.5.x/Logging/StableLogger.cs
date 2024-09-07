using System;
using OpenTabletDriver.Plugin;
using ScrollBinding.Lib.Interfaces;
using LogLevel = ScrollBinding.Lib.Enums.LogLevel;
using OTDLogLevel = OpenTabletDriver.Plugin.LogLevel;

namespace ScrollBinding.Logging;

public class StableLogger : ILogger
{
    public void Debug(string group, string message)
    {
        Log.Write(group, message);
    }

    public void Exception(Exception ex, LogLevel level = LogLevel.Error)
    {
        throw new NotSupportedException();
    }

    public void Write(string group, string message, LogLevel level = LogLevel.Info, bool createStackTrace = false, bool notify = false)
    {
        Log.Write(group, message, (OTDLogLevel)level);
    }

    public void WriteNotify(string group, string text, LogLevel level = LogLevel.Info)
    {
        throw new NotSupportedException();
    }
}