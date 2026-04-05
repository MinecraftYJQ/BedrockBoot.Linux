using System.Diagnostics;
using BedrockBoot.Linux.Entry.Progress;
using BedrockBoot.Linux.Models.Downloader;
using BedrockBoot.Linux.Models.Global;
using Octokit;

namespace BedrockBoot.Linux.Models.Proton;

public class ProtonDownloader
{
    public ProtonDownloader() { }
    public static string ProtonVersion = "GDK-Proton10-32";
    public async Task Download(IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        var client = new GitHubClient(new ProductHeaderValue("BedrockBoot.Linux"));
        var latest = client.Repository.Release.GetAll("Weather-OS", "GDK-Proton").Result.ToList()
            .Find(x => x.Name == ProtonVersion);

        var fileName = Path.Combine(PathsList.ProtonPath, "download",$"{ProtonVersion}.tar.gz");
        var workDir = Path.Combine(PathsList.ProtonPath, "work");
        var downloader = new GithubFilesDownloader();
        await downloader.DownloadAsync(latest!.Assets[0].BrowserDownloadUrl, fileName, progress, ct);
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xzf \"{fileName}\" -C \"{workDir}\"",
            RedirectStandardError = true,
            UseShellExecute = false
        };
            
        using var process = Process.Start(startInfo);
        await process?.WaitForExitAsync(ct);
    }
}