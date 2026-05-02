using System.Diagnostics;
using System.IO.Compression;

namespace YtGrab;

internal static class ToolManager
{
    private const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private static readonly HttpClient HttpClient = new();

    public static string ToolFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YtGrab",
        "bin");

    public static async Task<ToolPaths> EnsureToolsAsync(Action<string>? status = null)
    {
        Directory.CreateDirectory(ToolFolder);

        var ytDlpPath = FindExecutable("yt-dlp.exe") ?? Path.Combine(ToolFolder, "yt-dlp.exe");
        if (!File.Exists(ytDlpPath))
        {
            status?.Invoke("Installing yt-dlp...");
            await DownloadFileAsync(YtDlpDownloadUrl, ytDlpPath);
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
        var localPath = Path.Combine(ToolFolder, fileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => Path.Combine(path, fileName))
            .FirstOrDefault(File.Exists);
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
