using System.Diagnostics;
using BedrockBoot.Linux.Entry;
using Spectre.Console;

namespace BedrockBoot.Linux.Models.Game;

public class EasyLaunch
{
    private readonly LaunchInfo _launchInfo;

    public EasyLaunch(LaunchInfo launchInfo)
    {
        _launchInfo = launchInfo;
    }

    public void Launch()
    {
        if (!Directory.Exists(_launchInfo.PrefixPath)) 
            Directory.CreateDirectory(_launchInfo.PrefixPath);

        string protonScript = Path.Combine(_launchInfo.ProtonPath, "proton");
        
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = protonScript,
            Arguments = $"run \"{_launchInfo.GamePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,           // 接管输出通常不需要弹出原生窗口
            RedirectStandardOutput = true,   // 重定向标准输出
            RedirectStandardError = true,    // 重定向错误输出
        };

        // 注入 Proton 所需的环境变量
        startInfo.EnvironmentVariables["STEAM_COMPAT_DATA_PATH"] = _launchInfo.PrefixPath;
        startInfo.EnvironmentVariables["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam");
        
        string libPath = $"{Path.Combine(_launchInfo.ProtonPath, "files/lib64")}:{Path.Combine(_launchInfo.ProtonPath, "files/lib")}";
        string currentLdPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
        startInfo.EnvironmentVariables["LD_LIBRARY_PATH"] = $"{libPath}:{currentLdPath}";
        startInfo.EnvironmentVariables["WINEDLLOVERRIDES"] = "dxgi,d3d11,d3d10core,d3d9=b";

        try
        {
            using var process = new Process();
            process.StartInfo = startInfo;

            // 标准输出回调：使用 Text 对象避开 Markup 解析陷阱
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    AnsiConsole.Write(new Text($"[Out] ", new Style(Color.Grey)));
                    AnsiConsole.WriteLine(e.Data);
                }
            };

            // 错误输出回调
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    AnsiConsole.Write(new Text($"[Err] ", new Style(Color.Red)));
                    AnsiConsole.WriteLine(e.Data);
                }
            };

            process.Start();

            // 开启异步读取流
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]FATAL:[/] Launching Game failed: {Markup.Escape(ex.Message)}");
        }
    }
}