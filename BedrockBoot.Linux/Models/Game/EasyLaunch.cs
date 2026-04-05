using System.Diagnostics;
using BedrockBoot.Linux.Entry;

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
        if (!Directory.Exists(_launchInfo.PrefixPath)) Directory.CreateDirectory(_launchInfo.PrefixPath);

        string protonScript = Path.Combine(_launchInfo.ProtonPath, "proton");
        
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = protonScript,
            Arguments = $"run \"{_launchInfo.GamePath}\"",
            UseShellExecute = false,
            CreateNoWindow = false
        };

        startInfo.EnvironmentVariables["STEAM_COMPAT_DATA_PATH"] = _launchInfo.PrefixPath;
        startInfo.EnvironmentVariables["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam");
        
        string libPath = $"{Path.Combine(_launchInfo.ProtonPath, "files/lib64")}:{Path.Combine(_launchInfo.ProtonPath, "files/lib")}";
        string currentLdPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
        startInfo.EnvironmentVariables["LD_LIBRARY_PATH"] = $"{libPath}:{currentLdPath}";
        startInfo.EnvironmentVariables["WINEDLLOVERRIDES"] = "dxgi,d3d11,d3d10core,d3d9=b";

        Console.WriteLine("Launching Game...");
        try
        {
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Launching Game failed: {ex.Message}");
        }
    }
}