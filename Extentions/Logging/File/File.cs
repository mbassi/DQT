using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Extentions.Logging.File
{
    public class CustomFileLoggerProvider : ILoggerProvider
    {
        private readonly CustomFileLoggerOptions _options;

        public CustomFileLoggerProvider(CustomFileLoggerOptions options)
        {
            _options = options;
            // Ensure log directory exists
            Directory.CreateDirectory(options.LogDirectory);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomFileLogger(categoryName, _options);
        }

        public void Dispose() { }
    }

    public class CustomFileLoggerOptions
    {
        public string LogDirectory { get; set; } = "Logs";
        public string FileNamePrefix { get; set; } = "app-";
        public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff zzz";
        public LogLevel MinLogLevel { get; set; } = LogLevel.Information;
    }

    public class CustomFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly CustomFileLoggerOptions _options;

        public CustomFileLogger(string categoryName, CustomFileLoggerOptions options)
        {
            _categoryName = categoryName;
            _options = options;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _options.MinLogLevel;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Generate log file path with rotation
            string logFilePath = Path.Combine(
                _options.LogDirectory,
                $"{_options.FileNamePrefix}{DateTime.UtcNow:yyyyMMdd-HHmm}.log"
            );

            // Format the log message with timestamp
            string timestamp = DateTime.UtcNow.ToString(_options.TimestampFormat);
            string logLevel_str = logLevel.ToString().ToUpper();
            string message = formatter(state, exception);

            string logEntry = $"[{timestamp}] [{logLevel_str}] [{_categoryName}] {message}";

            // Add exception details if present
            if (exception != null)
            {
                logEntry += $"\n    Exception: {exception.GetType().Name}: {exception.Message}";
                logEntry += $"\n    Stacktrace: {exception.StackTrace}";
            }

            // Thread-safe file writing
            try
            {
                System.IO.File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Fallback error handling
                Console.Error.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }
    }

    // Custom Console Logger (Optional, for consistent formatting)
    public class CustomConsoleLoggerProvider : ILoggerProvider
    {
        private readonly CustomConsoleLoggerOptions _options;

        public CustomConsoleLoggerProvider(CustomConsoleLoggerOptions options)
        {
            _options = options;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomConsoleLogger(categoryName, _options);
        }

        public void Dispose() { }
    }

    public class CustomConsoleLoggerOptions
    {
        public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff zzz";
        public LogLevel MinLogLevel { get; set; } = LogLevel.Information;
    }

    public class CustomConsoleLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly CustomConsoleLoggerOptions _options;

        public CustomConsoleLogger(string categoryName, CustomConsoleLoggerOptions options)
        {
            _categoryName = categoryName;
            _options = options;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _options.MinLogLevel;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Format the log message with timestamp
            string timestamp = DateTime.UtcNow.ToString(_options.TimestampFormat);
            string logLevel_str = logLevel.ToString().ToUpper();
            string message = formatter(state, exception);

            string logEntry = $"[{timestamp}] [{logLevel_str}] [{_categoryName}] {message}";

            // Add exception details if present
            if (exception != null)
            {
                logEntry += $"\n    Exception: {exception.GetType().Name}: {exception.Message}";
                logEntry += $"\n    Stacktrace: {exception.StackTrace}";
            }

            // Write to console with color-coding
            Console.ForegroundColor = logLevel switch
            {
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => Console.ForegroundColor
            };

            Console.WriteLine(logEntry);
            Console.ResetColor();
        }
    }
}

