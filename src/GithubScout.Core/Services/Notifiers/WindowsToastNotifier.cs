using System.Diagnostics;
using GithubScout.Core.Abstractions;
using GithubScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.Notifiers;

/// <summary>
/// Shows a native Windows toast notification using the built-in WinRT toast API via a short
/// PowerShell script, which avoids taking on extra native/UI package dependencies. No-ops
/// (and never runs) on any non-Windows OS.
/// </summary>
public sealed class WindowsToastNotifier : INotifier
{
    private readonly ILogger<WindowsToastNotifier> _logger;

    public WindowsToastNotifier(ILogger<WindowsToastNotifier> logger)
    {
        _logger = logger;
    }

    public async Task NotifyAsync(WatchItem item, WatchStatus currentStatus, IReadOnlyList<string> changes, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || changes.Count == 0)
        {
            return;
        }

        var title = Escape(item.DisplayName);
        var message = Escape(string.Join(" | ", changes));

        var script = $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
            [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
            $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
            $textNodes = $template.GetElementsByTagName('text')
            $textNodes.Item(0).AppendChild($template.CreateTextNode('{{title}}')) | Out-Null
            $textNodes.Item(1).AppendChild($template.CreateTextNode('{{message}}')) | Out-Null
            $toast = [Windows.UI.Notifications.ToastNotification]::new($template)
            [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('GithubScout').Show($toast)
            """;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process is not null)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show toast notification for {Item}", item.DisplayName);
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
