using System.IO;

namespace PharmaExt.App.Services;

public static class DiagnosticLogService
{
    public static string FirebirdLogPath => Path.Combine(AppContext.BaseDirectory, "Data", "firebird.log");

    public static void LogFirebird(string operation, string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FirebirdLogPath)!);
            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {operation}: {message}";
            if (exception is not null)
            {
                text += Environment.NewLine + exception;
            }

            File.AppendAllText(FirebirdLogPath, text + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
            // Logging must never interrupt pharmacy work.
        }
    }
}
