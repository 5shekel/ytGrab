using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace YtGrab;

internal static class ToolManager
{
    private const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string YtDlpLatestReleaseUrl = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private const string FfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private static readonly HttpClient HttpClient = new();

    public static string ToolFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YtGrab",
        "bin");

    public static async Task<ToolPaths> EnsureToolsAsync(Action<string>? status = null)
    {
        Directory.CreateDirectory(ToolFolder);

        var ytDlpPath = FindOnPath("yt-dlp.exe") ?? Path.Combine(ToolFolder, "yt-dlp.exe");
        if (!File.Exists(ytDlpPath))
        {
            status?.Invoke("Installing yt-dlp...");
            await DownloadFileAsync(YtDlpDownloadUrl, ytDlpPath);
        }
        else if (IsBundledTool(ytDlpPath))
        {
            await UpdateBundledYtDlpIfNeededAsync(ytDlpPath, status);
        }

        var ffmpegPath = FindExecutable("ffmpeg.exe") ?? Path.Combine(ToolFolder, "ffmpeg.exe");
        var usingBundledFfmpeg = !IsOnPath(ffmpegPath);
        if (!File.Exists(ffmpegPath))
        {
            status?.Invoke("Installing ffmpeg...");
            await DownloadAndExtractFfmpegAsync(ffmpegPath);
            usingBundledFfmpeg = true;
        }

        status?.Invoke("Idle");
        return new ToolPaths(ytDlpPath, usingBundledFfmpeg ? ToolFolder : null);
    }

    private static async Task UpdateBundledYtDlpIfNeededAsync(string ytDlpPath, Action<string>? status)
    {
        try
        {
            status?.Invoke("Checking yt-dlp...");

            var localVersion = await GetYtDlpVersionAsync(ytDlpPath);
            var latestVersion = await GetLatestYtDlpVersionAsync();

            if (IsVersionNewer(latestVersion, localVersion))
            {
                status?.Invoke("Updating yt-dlp...");
                await DownloadFileAsync(YtDlpDownloadUrl, ytDlpPath);
            }
        }
        catch
        {
            // If an existing bundled yt-dlp works, don't block downloads because GitHub or version checks failed.
        }
    }

    private static async Task<string?> GetYtDlpVersionAsync(string ytDlpPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("--version");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return process.ExitCode == 0 ? NormalizeVersion(await outputTask) : null;
    }

    private static async Task<string?> GetLatestYtDlpVersionAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, YtDlpLatestReleaseUrl);
        request.Headers.UserAgent.ParseAdd("YtGrab");

        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);

        return json.RootElement.TryGetProperty("tag_name", out var tagName)
            ? NormalizeVersion(tagName.GetString())
            : null;
    }

    private static bool IsVersionNewer(string? latestVersion, string? localVersion)
    {
        if (!Version.TryParse(latestVersion, out var latest) || !Version.TryParse(localVersion, out var local))
        {
            return false;
        }

        return latest > local;
    }

    private static string? NormalizeVersion(string? version)
    {
        return version?.Trim().TrimStart('v', 'V');
    }

    private static async Task DownloadFileAsync(string url, string destinationPath)
    {
        var tempPath = destinationPath + ".download";
        await using (var input = await HttpClient.GetStreamAsync(url))
        await using (var output = File.Create(tempPath))
        {
            await input.CopyToAsync(output);
        }

        File.Move(tempPath, destinationPath, true);
    }

    private static async Task DownloadAndExtractFfmpegAsync(string destinationPath)
    {
        var zipPath = Path.Combine(ToolFolder, "ffmpeg.zip.download");
        var extractFolder = Path.Combine(ToolFolder, "ffmpeg-extract");

        if (Directory.Exists(extractFolder))
        {
            Directory.Delete(extractFolder, true);
        }

        await DownloadFileAsync(FfmpegDownloadUrl, zipPath);
        ZipFile.ExtractToDirectory(zipPath, extractFolder);

        var extractedFfmpeg = Directory.EnumerateFiles(extractFolder, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (extractedFfmpeg is null)
        {
            throw new FileNotFoundException("Downloaded ffmpeg archive did not contain ffmpeg.exe.");
        }

        File.Copy(extractedFfmpeg, destinationPath, true);
        File.Delete(zipPath);
        Directory.Delete(extractFolder, true);
    }

    private static string? FindExecutable(string fileName)
    {
        return FindOnPath(fileName) ?? FindBundledTool(fileName);
    }

    private static string? FindOnPath(string fileName)
    {
        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => Path.Combine(path, fileName))
            .FirstOrDefault(File.Exists);
    }

    private static string? FindBundledTool(string fileName)
    {
        var localPath = Path.Combine(ToolFolder, fileName);
        return File.Exists(localPath) ? localPath : null;
    }

    private static bool IsBundledTool(string executablePath)
    {
        return string.Equals(
            Path.GetDirectoryName(Path.GetFullPath(executablePath)),
            Path.GetFullPath(ToolFolder),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOnPath(string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath);

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => Path.Combine(path, Path.GetFileName(executablePath)))
            .Any(path => string.Equals(Path.GetFullPath(path), fullPath, StringComparison.OrdinalIgnoreCase)) == true;
    }
}

internal sealed record ToolPaths(string YtDlpPath, string? FfmpegLocation);
