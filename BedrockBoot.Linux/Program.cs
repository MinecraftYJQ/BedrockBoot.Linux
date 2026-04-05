using System;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("❌ 错误: 请指定游戏 .exe 路径");
            Console.WriteLine("使用方法: dotnet run -- /path/to/Minecraft.Windows.exe");
            return;
        }

        string gameExePath = Path.GetFullPath(args[0]);
        string workDir = AppDomain.CurrentDomain.BaseDirectory;
        string protonRoot = Path.Combine(workDir, "GDK-Proton10-32");
        string prefixPath = Path.Combine(protonRoot, "game_prefix");

        Console.WriteLine("=== Minecraft Bedrock For Linux C# Cli ===");

        if (!Directory.Exists(protonRoot))
        {
            await DownloadAndExtractProton(workDir, protonRoot);
        }

        LaunchGame(protonRoot, prefixPath, gameExePath);
    }

    static async Task DownloadAndExtractProton(string workDir, string protonRoot)
    {
        string apiUrl = "https://api.github.com/repos/Weather-OS/GDK-Proton/releases/latest";
        string tarPath = Path.Combine(workDir, "gdk_proton.tar.gz");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "C# Launcher");

        try
        {
            Console.WriteLine("🚚 正在获取最新下载链接...");
            var apiResponse = await client.GetStringAsync(apiUrl);
            using var json = JsonDocument.Parse(apiResponse);
            
            var downloadUrl = json.RootElement
                .GetProperty("assets")
                .EnumerateArray()
                .FirstOrDefault(a => a.GetProperty("name").GetString().Contains("GDK-Proton10-32.tar.gz"))
                .GetProperty("browser_download_url")
                .GetString();

            if (string.IsNullOrEmpty(downloadUrl)) throw new Exception("未能找到下载地址");

            // --- 开始带进度的下载 ---
            Console.WriteLine($"🌐 开始下载: {Path.GetFileName(downloadUrl)}");
            
            using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tarPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalReadBytes = 0;
                    int readBytes;

                    while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, readBytes);
                        totalReadBytes += readBytes;

                        if (canReportProgress)
                        {
                            double progress = (double)totalReadBytes / totalBytes * 100;
                            // 使用 \r 实现单行刷新进度条
                            Console.Write($"\r⏳ 进度: {progress:F2}% [{GetProgressBar(progress)}] ({totalReadBytes / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB)");
                        }
                        else
                        {
                            Console.Write($"\r⏳ 已下载: {totalReadBytes / 1024 / 1024}MB");
                        }
                    }
                }
            }
            Console.WriteLine("\n✅ 下载完成！");

            // --- 解压逻辑 ---
            Console.WriteLine("📦 正在解压 GDK-Proton (请稍候)...");
            var startInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{tarPath}\" -C \"{workDir}\"",
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            
            if (process?.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"解压失败: {error}");
            }

            File.Delete(tarPath);
            Console.WriteLine("✅ GDK-Proton 已就绪");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 发生错误: {ex.Message}");
            if (File.Exists(tarPath)) File.Delete(tarPath);
            Environment.Exit(1);
        }
    }

    // 辅助方法：生成简单的字符进度条
    static string GetProgressBar(double percent)
    {
        int blockCount = (int)(percent / 5);
        return new string('█', blockCount) + new string('░', 20 - blockCount);
    }

    static void LaunchGame(string protonRoot, string prefixPath, string gameExePath)
    {
        if (!Directory.Exists(prefixPath)) Directory.CreateDirectory(prefixPath);

        string protonScript = Path.Combine(protonRoot, "proton");
        
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = protonScript,
            Arguments = $"run \"{gameExePath}\"",
            UseShellExecute = false,
            CreateNoWindow = false
        };

        startInfo.EnvironmentVariables["STEAM_COMPAT_DATA_PATH"] = prefixPath;
        startInfo.EnvironmentVariables["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam");
        
        string libPath = $"{Path.Combine(protonRoot, "files/lib64")}:{Path.Combine(protonRoot, "files/lib")}";
        string currentLdPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
        startInfo.EnvironmentVariables["LD_LIBRARY_PATH"] = $"{libPath}:{currentLdPath}";
        startInfo.EnvironmentVariables["WINEDLLOVERRIDES"] = "dxgi,d3d11,d3d10core,d3d9=b";

        Console.WriteLine("🎮 启动游戏...");
        try
        {
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 启动失败: {ex.Message}");
        }
    }
}