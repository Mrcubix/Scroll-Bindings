using System;
using ScrollBinding.Lib.Enums;

namespace ScrollBinding.Lib.Interfaces;

public interface ILogger
{
    void Write(string group, string message, LogLevel level = LogLevel.Info, bool createStackTrace = false, bool notify = false);

    void WriteNotify(string group, string text, LogLevel level = LogLevel.Info);

    void Debug(string group, string message);

    void Exception(Exception ex, LogLevel level = LogLevel.Error);
}