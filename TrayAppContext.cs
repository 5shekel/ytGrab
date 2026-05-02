using System.Collections.Concurrent;
using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YtGrab;

internal sealed partial class TrayAppContext : ApplicationContext
{
    private readonly AppSettings settings;
    private readonly ClipboardWatcher clipboardWatcher;
    private readonly NotifyIcon notifyIcon;
    private readonly Icon trayIcon;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem outputFolderItem;
    private readonly ToolStripMenuItem beepItem;
    private readonly ToolStripMenuItem openWhenDoneItem;
    private readonly ConcurrentQueue<string> queue = new();
    private readonly HashSet<string> seenUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim downloaderGate = new(1, 1);
    private readonly Task<ToolPaths> toolsTask;
    private static readonly HttpClient HttpClient = new();
    private bool isDownloading;

    public TrayAppContext()
    {
        settings = AppSettings.Load();
        Directory.CreateDirectory(settings.OutputFolder);

        statusItem = new ToolStripMenuItem("Idle") { Enabled = false };
        outputFolderItem = new ToolStripMenuItem($"Output: {settings.OutputFolder}", null, SelectOutputFolder);
        beepItem = new ToolStripMenuItem("Beep when done", null, ToggleBeep) { Checked = settings.BeepWhenDone };
        openWhenDoneItem = new ToolStripMenuItem("Open folder when done", null, ToggleOpenWhenDone) { Checked = settings.OpenFolderWhenDone };

        trayIcon = CreateTrayIcon();
        notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "YtGrab - watching clipboard",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        notifyIcon.ContextMenuStrip.Items.AddRange([
            statusItem,
            new ToolStripSeparator(),
            outputFolderItem,
            new ToolStripMenuItem("Open output folder", null, (_, _) => OpenOutputFolder()),
            beepItem,
            openWhenDoneItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem($"YtGrab v{Application.ProductVersion}") { Enabled = false },
            new ToolStripMenuItem("Exit", null, Exit)
        ]);

        clipboardWatcher = new ClipboardWatcher();
        clipboardWatcher.ClipboardChanged += ClipboardChanged;
        toolsTask = ToolManager.EnsureToolsAsync(SetStatus);
    }

    private void ClipboardChanged(object? sender, EventArgs e)
    {
        string? text;

        try
        {
            text = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : null;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var url in ExtractUrls(text))
        {
            if (seenUrls.Add(url))
            {
                queue.Enqueue(url);
                SetStatus($"Queued {queue.Count}");
            }
        }

        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (!await downloaderGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            while (queue.TryDequeue(out var url))
            {
                isDownloading = true;
                SetStatus("Downloading...");

                var result = await DownloadAsync(url);
                if (result.ExitCode == 0)
                {
                    Notify("Download complete", Shorten(url));

                    if (settings.BeepWhenDone)
                    {
                        SystemSounds.Asterisk.Play();
                    }

                    if (settings.OpenFolderWhenDone)
                    {
                        OpenOutputFolder();
                    }
                }
                else
                {
                    Notify("Download failed", result.ErrorMessage);
                }
            }
        }
        finally
        {
            isDownloading = false;
            SetStatus("Idle");
            downloaderGate.Release();
        }
    }

    private async Task<DownloadResult> DownloadAsync(string url)
    {
        Directory.CreateDirectory(settings.OutputFolder);
        ToolPaths tools;

        try
        {
            tools = await toolsTask;
        }
        catch (Exception ex)
        {
            return new DownloadResult(1, $"Could not install yt-dlp/ffmpeg: {ex.Message}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = tools.YtDlpPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        startInfo.ArgumentList.Add("--no-playlist");
        startInfo.ArgumentList.Add("--windows-filenames");
        startInfo.ArgumentList.Add("--restrict-filenames");
        startInfo.ArgumentList.Add("--merge-output-format");
        startInfo.ArgumentList.Add("mp4");
        startInfo.ArgumentList.Add("--recode-video");
        startInfo.ArgumentList.Add("mp4");
        if (tools.FfmpegLocation is not null)
        {
            startInfo.ArgumentList.Add("--ffmpeg-location");
            startInfo.ArgumentList.Add(tools.FfmpegLocation);
        }

        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("bv*[ext=mp4][vcodec^=avc1][height<=720]+ba[ext=m4a]/b[ext=mp4][height<=720]/b[height<=720]/b");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(await GetOutputTemplateAsync(url));
        startInfo.ArgumentList.Add(url);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new DownloadResult(1, "Could not start yt-dlp.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;
            var message = LastMeaningfulLine(error) ?? LastMeaningfulLine(output) ?? "yt-dlp exited with an error.";

            return new DownloadResult(process.ExitCode, message);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new DownloadResult(1, "yt-dlp could not be started.");
        }
        catch (Exception ex)
        {
            return new DownloadResult(1, ex.Message);
        }
    }

    private void SelectOutputFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where downloads are saved",
            SelectedPath = settings.OutputFolder,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        settings.OutputFolder = dialog.SelectedPath;
        Directory.CreateDirectory(settings.OutputFolder);
        settings.Save();
        outputFolderItem.Text = $"Output: {settings.OutputFolder}";
    }

    private void ToggleBeep(object? sender, EventArgs e)
    {
        settings.BeepWhenDone = !settings.BeepWhenDone;
        beepItem.Checked = settings.BeepWhenDone;
        settings.Save();
    }

    private void ToggleOpenWhenDone(object? sender, EventArgs e)
    {
        settings.OpenFolderWhenDone = !settings.OpenFolderWhenDone;
        openWhenDoneItem.Checked = settings.OpenFolderWhenDone;
        settings.Save();
    }

    private void OpenOutputFolder()
    {
        Directory.CreateDirectory(settings.OutputFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = settings.OutputFolder,
            UseShellExecute = true
        });
    }

    private void Notify(string title, string body)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = body;
        notifyIcon.ShowBalloonTip(5000);
    }

    private async Task<string> GetOutputTemplateAsync(string url)
    {
        var title = await GetYouTubeTitleAsync(url);
        var fileName = title is null ? "%(title,id)s" : SanitizeFileName(title);

        return Path.Combine(settings.OutputFolder, fileName + ".%(ext)s");
    }

    private static async Task<string?> GetYouTubeTitleAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsYouTubeHost(uri.Host))
        {
            return null;
        }

        try
        {
            var oembedUrl = "https://www.youtube.com/oembed?format=json&url=" + Uri.EscapeDataString(url);
            using var response = await HttpClient.GetAsync(oembedUrl);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var title = document.RootElement.TryGetProperty("title", out var property) ? property.GetString() : null;

            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsYouTubeHost(string host)
    {
        return host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars().Append('%').ToHashSet();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? ' ' : ch).ToArray());
        sanitized = WhitespaceRegex().Replace(sanitized, " ").Trim(' ', '.');

        if (sanitized.Length > 180)
        {
            sanitized = sanitized[..180].Trim(' ', '.');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "download" : sanitized;
    }

    private void SetStatus(string status)
    {
        var text = $"YtGrab - {status}";
        statusItem.Text = status;
        notifyIcon.Text = text[..Math.Min(63, text.Length)];
    }

    private void Exit(object? sender, EventArgs e)
    {
        if (isDownloading)
        {
            var result = MessageBox.Show(
                "A download is still running. Exit anyway?",
                "YtGrab",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        clipboardWatcher.Dispose();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        trayIcon.Dispose();
        ExitThread();
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.DeepPink);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        graphics.FillEllipse(brush, 4, 4, 56, 56);

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static IEnumerable<string> ExtractUrls(string text)
    {
        foreach (Match match in UrlRegex().Matches(text))
        {
            var url = match.Value.TrimEnd('.', ',', ')', ']', '}', '>', '"', '\'');
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                yield return url;
            }
        }
    }

    private static string Shorten(string text)
    {
        return text.Length <= 180 ? text : text[..177] + "...";
    }

    private static string? LastMeaningfulLine(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private sealed record DownloadResult(int ExitCode, string ErrorMessage);
}
