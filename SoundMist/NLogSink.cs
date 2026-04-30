using Avalonia;
using Avalonia.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SoundMist;

public class NLogSink : ILogSink
{
    private readonly LogEventLevel _level;
    private readonly IList<string>? _areas;

    public NLogSink(
        LogEventLevel minimumLevel,
        IList<string>? areas = null)
    {
        _level = minimumLevel;
        _areas = areas?.Count > 0 ? areas : null;
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return level >= _level && (_areas?.Contains(area) ?? true);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if (IsEnabled(level, area))
        {
            ILogger? logger = null;
            if (source is not null)
                logger = Resolve(source.GetType());
            else
                logger = Resolve(typeof(NLogSink));

            logger.Log(LogLevelToNLogLevel(level), $"{area}: {messageTemplate}");
        }
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (IsEnabled(level, area))
        {
            ILogger? logger = null;
            if (source is not null)
                logger = Resolve(source.GetType());
            else
                logger = Resolve(typeof(NLogSink));

            logger.Log(LogLevelToNLogLevel(level), $"{area}: {messageTemplate}", propertyValues);
        }
    }

    private const int MaxCacheSize = 16;

    private static readonly Dictionary<Type, (ILogger logger, DateTime creationTime)> _loggerCache = [];

    public static ILogger Resolve(Type type)
    {
        if (_loggerCache.TryGetValue(type, out var value))
            return value.logger;

        var log = LogManager.GetLogger(type.ToString());
        _loggerCache.Add(type, (log, DateTime.Now));

        if (_loggerCache.Count > MaxCacheSize)
            _loggerCache.Remove(_loggerCache.MinBy(x => x.Value.creationTime).Key);

        return log;
    }

    private static LogLevel LogLevelToNLogLevel(LogEventLevel level)
    {
        switch (level)
        {
            case LogEventLevel.Verbose:
                return LogLevel.Trace;

            case LogEventLevel.Debug:
                return LogLevel.Debug;

            case LogEventLevel.Information:
                return LogLevel.Info;

            case LogEventLevel.Warning:
                return LogLevel.Warn;

            case LogEventLevel.Error:
                return LogLevel.Error;

            case LogEventLevel.Fatal:
                return LogLevel.Fatal;

            default:
                return LogLevel.Trace;
        }
    }
}

public static class NLogSinkExtensions
{
    public static AppBuilder LogToNLog(
        this AppBuilder builder,
        LogEventLevel level = LogEventLevel.Warning,
        params string[] areas)
    {
        LogManager.Setup().LoadConfiguration(conf =>
        {
            Directory.CreateDirectory(Globals.AppDirectory);
            string logFilePath = Globals.AppDirectory + "log.txt";
            if (File.Exists(logFilePath))
            {
                var fi = new FileInfo(logFilePath);
                if (fi.Length > 1_000_000) //if more than ~1MB - scoot over
                    File.Move(logFilePath, logFilePath + ".old", true);
            }

            conf.ForLogger().FilterMinLevel(LogLevel.Trace).WriteToDebug();
#if DEBUG
            conf.ForLogger().FilterMinLevel(LogLevel.Trace).WriteToFile(logFilePath);
#else
            conf.ForLogger().FilterMinLevel(LogLevel.Info).WriteToFile(logFilePath);
#endif
        });

        Avalonia.Logging.Logger.Sink = new NLogSink(level, areas);
        return builder;
    }
}