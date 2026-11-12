namespace GithubScout.Core.Services.KeepAwake;

/// <summary>
/// Appends timestamped diagnostic lines to a plain text file. Used for keep-awake and network
/// investigation, because the console dashboard redraws the screen and would hide console log lines.
/// Writing never throws, so diagnostics can't take the app down.
/// </summary>
public sealed class KeepAwakeDiagnostics
{
    private readonly string _path;
    private readonly object _sync = new();

    public KeepAwakeDiagnostics(string path)
    {
        _path = path;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (Exception)
        {
            // Best effort: Write will simply fail silently if the directory can't be created.
        }
    }

    /// <summary>Default location: %LOCALAPPDATA%\GithubScout\keepawake.log</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GithubScout", "keepawake.log");

    public string LogFilePath => _path;

    public void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [pid {Environment.ProcessId}] {message}{Environment.NewLine}";
        try
        {
            lock (_sync)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch (Exception)
        {
            // Never let diagnostics crash the service.
        }
    }
}
