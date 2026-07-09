namespace Ypopup.Core.Logging;

public static class LogService
{
    private static string? _logDirectory;
    private static readonly object _lock = new();

    public static void Initialize(string logDirectory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(logDirectory);
            CleanupOldLogs(logDirectory);
            _logDirectory = logDirectory;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogService] Failed to initialize log directory: {ex.Message}");
        }
    }

    public static void Debug(string module, string message) => Write("DEBUG", module, message);
    public static void Info(string module, string message) => Write("INFO", module, message);
    public static void Warning(string module, string message) => Write("WARN", module, message);
    public static void Error(string module, string message) => Write("ERROR", module, message);

    private static void Write(string level, string module, string message)
    {
        var now = DateTime.Now;
        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {module}: {message}";
        System.Diagnostics.Debug.WriteLine(line);

        if (_logDirectory is null)
        {
            return;
        }

        lock (_lock)
        {
            try
            {
                var logPath = System.IO.Path.Combine(_logDirectory, $"{now:yyyy-MM-dd}.log");
                System.IO.File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogService] Failed to write log file: {ex.Message}");
            }
        }
    }

    private static void CleanupOldLogs(string logDirectory)
    {
        if (!System.IO.Directory.Exists(logDirectory))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-7);
        foreach (var file in System.IO.Directory.GetFiles(logDirectory, "*.log"))
        {
            try
            {
                if (System.IO.File.GetCreationTimeUtc(file) < cutoff)
                {
                    System.IO.File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogService] Failed to clean old log: {ex.Message}");
            }
        }
    }
}
