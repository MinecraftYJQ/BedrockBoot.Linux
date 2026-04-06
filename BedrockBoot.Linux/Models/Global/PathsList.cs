namespace BedrockBoot.Linux.Models.Global;

public class PathsList
{
    public static readonly string RootConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoundStudio",
            "BedrockBoot2.Linux");
    
    public static readonly string ProtonPath = Path.Combine(RootConfigPath, "BedrockBoot.Proton");
    public static readonly string LinuxGamePath = Path.Combine(RootConfigPath, "BedrockBoot.LinuxGame");
    public static readonly string ConfigPath = Path.Combine(RootConfigPath, "BedrockBoot.Config", "Config.json");
}