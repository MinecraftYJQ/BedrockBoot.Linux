namespace BedrockBoot.Linux.Models.Global;

public class PathsList
{
    public static readonly string RootConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoundStudio",
            "BedrockBoot2");
    
    public static readonly string ProtonPath = Path.Combine(RootConfigPath, "BedrockBoot.Proton");
}