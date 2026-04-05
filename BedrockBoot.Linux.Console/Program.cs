using BedrockBoot.Linux.Entry.Progress;
using BedrockBoot.Linux.Models.Game;
using BedrockBoot.Linux.Models.Global;
using BedrockBoot.Linux.Models.Proton;

class Program
{
    static async Task Main(string[] args)
    {
        await ArgsAnalysis(args);
    }

    public static async Task ArgsAnalysis(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLower();

            switch (arg)
            {
                case "-launch":
                case "--l":
                    if (i + 1 < args.Length)
                    {
                        string gameFolder = args[++i];
                        await Task.Run(() => LaunchGame(gameFolder));
                    }
                    else
                    {
                        Console.WriteLine("错误: -launch 需要指定游戏目录。");
                    }
                    break;

                case "-install":
                case "--i":
                    if (i + 1 < args.Length)
                    {
                        string version = args[++i];
                        InstallGame(version);
                    }
                    break;

                case "-install-list":
                    ViewAllVersions();
                    break;

                case "-game-list":
                    ViewInstalledVersions();
                    break;

                default:
                    Console.WriteLine($"未知指令: {arg}");
                    PrintHelp();
                    break;
            }
        }
    }

    static void LaunchGame(string gameFolder)
    {
        string protonWorkPath = Path.Combine(PathsList.ProtonPath, "work");
        string protonBinPath = Path.Combine(protonWorkPath, "GDK-Proton10-32");

        if (!Directory.Exists(protonBinPath))
        {
            Console.WriteLine("Proton 环境缺失，正在开始下载...");
            DownloadAndExtractProton().Wait(); 
        }

        var launch = new EasyLaunch(new()
        {
            GamePath = Path.Combine(gameFolder, "Minecraft.Windows.exe"),
            PrefixPath = Path.Combine(protonWorkPath, "game_prefix"),
            ProtonPath = Path.Combine(protonWorkPath, ProtonDownloader.ProtonVersion)
        });

        launch.Launch(); 
    }

    static async Task DownloadAndExtractProton()
    {
        var downloader = new ProtonDownloader();
        await downloader.Download(new Progress<DownloadProgress>(s =>
        {
            Console.Write($"\r下载进度: {s.ProgressPercentage:F2} %");
        }));
        Console.WriteLine("\n环境准备就绪。");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("\n--- 可用参数 ---");
        Console.WriteLine("-launch --l <folder>     启动游戏");
        Console.WriteLine("-install --i <version>   安装游戏");
        Console.WriteLine("-install-list            查看所有版本");
        Console.WriteLine("-game-list               查看已安装的版本");
        Console.WriteLine("----------------\n");
    }

    private static void InstallGame(string v) => Console.WriteLine($"正在安装 {v}...");
    private static void ViewAllVersions() => Console.WriteLine("查看版本列表...");
    private static void ViewInstalledVersions() => Console.WriteLine("查看已安装版本...");
}