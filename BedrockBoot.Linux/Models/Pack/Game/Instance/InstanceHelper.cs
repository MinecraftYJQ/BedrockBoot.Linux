using BedrockBoot.Linux.Entity;
using BedrockBoot.Linux.Entry.Manifest.Game;
using BedrockBoot.Linux.Enum;

namespace BedrockBoot.Linux.Models.Pack.Game.Instance;

public class InstanceHelper
{
    private const string ConfigSubPath = "config/BedrockBoot2";
    private const string ConfigFileName = "config.json";
    
    public static List<GameConfig> GetVersionConfigs(string gameFolder)
    {
        var bedrockVersionsPath = Path.Combine(gameFolder, "bedrock_versions");

        if (!Directory.Exists(bedrockVersionsPath))
            return new List<GameConfig>();

        // 使用 EnumerateDirectories 提高大目录下的性能
        return Directory.EnumerateDirectories(bedrockVersionsPath)
            .Select(GetVersionConfig)
            .Where(config => config?.Info != null && 
                             !string.IsNullOrEmpty(config.Info.VersionName) && 
                             !string.IsNullOrEmpty(config.Info.Version))
            .ToList();
    }
    public static GameConfig GetVersionConfig(string gamePath)
    {
        var configDir = Path.Combine(gamePath, ConfigSubPath);
        var configJsonPath = Path.Combine(configDir, ConfigFileName);
        
        ConfigEntity<GameConfig> configEntity;

        // 检查配置文件是否存在
        if (!File.Exists(configJsonPath))
        {
            var manifestPath = Path.Combine(gamePath, "appxmanifest.xml");
            if (!File.Exists(manifestPath)) return null;

            // 初始化新配置
            Directory.CreateDirectory(configDir);
            configEntity = new ConfigEntity<GameConfig>(configJsonPath);
            configEntity.Load();

            var manifest = PackageIdentity.ParseFromXml(File.ReadAllText(manifestPath));
            
            configEntity.Data.Info = new GameConfig.VersionInfo
            {
                Version = manifest.Version,
                VersionName = Path.GetFileName(gamePath),
                BuildType = File.Exists(Path.Combine(gamePath, "MicrosoftGame.Config"))
                    ? MinecraftType.MinecraftBuildTypeVersion.GDK
                    : MinecraftType.MinecraftBuildTypeVersion.UWP,
                VersionType = GetVersionTypeWithPackName(manifest.Name)
            };
            configEntity.Save();
        }
        else
        {
            configEntity = new ConfigEntity<GameConfig>(configJsonPath, false);
            configEntity.Load();
        }

        // 绑定运行时路径
        var bodyFile = GetBodyFile(gamePath);
        if (string.IsNullOrEmpty(bodyFile)) return null;

        var data = configEntity.Data;
        data.VersionPath = gamePath;
        data.BodyFile = bodyFile;

        return data;
    }
    public static void SaveVersionConfig(GameConfig config)
    {
        if (config == null) return;

        var configDir = Path.Combine(config.VersionPath, ConfigSubPath);
        var configJsonPath = Path.Combine(configDir, ConfigFileName);

        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);

        var cfg = new ConfigEntity<GameConfig>(configJsonPath) { Data = config };
        cfg.Save();
    }
    public static MinecraftType.MinecraftGameTypeVersion GetVersionTypeWithPackName(string packName)
    {
        if (string.IsNullOrEmpty(packName)) return MinecraftType.MinecraftGameTypeVersion.Release;

        // 使用 Contains 的 StringComparison 忽略大小写，效率更高
        if (packName.Contains("preview", StringComparison.OrdinalIgnoreCase) || 
            packName.Contains("beta", StringComparison.OrdinalIgnoreCase))
        {
            return MinecraftType.MinecraftGameTypeVersion.Preview;
        }

        return MinecraftType.MinecraftGameTypeVersion.Release;
    }
    public static string GetBodyFile(string gamePath)
    {
        // 仅搜索顶级目录，避免递归产生的性能消耗
        var exeFiles = Directory.EnumerateFiles(gamePath, "Minecraft*.exe")
            .ToList();

        if (exeFiles.Count == 0) return string.Empty;

        // 这里的逻辑保持严谨：多个 EXE 可能意味着环境异常
        if (exeFiles.Count > 1)
        {
            throw new InvalidOperationException(
                $"检测到异常：目录中存在多个 Minecraft EXE 文件 ({exeFiles.Count}个)。\n" +
                $"请清理目录以防潜在风险。\n路径：{gamePath}");
        }

        return Path.GetFileName(exeFiles[0]);
    }
}