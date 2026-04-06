using BedrockBoot.Linux.Console.Entry;
using BedrockBoot.Linux.Entity;
using BedrockBoot.Linux.Entry.Progress;
using BedrockBoot.Linux.Models.Game;
using BedrockBoot.Linux.Models.Global;
using BedrockBoot.Linux.Models.Helper;
using BedrockBoot.Linux.Models.Pack.Game.Instance;
using BedrockBoot.Linux.Models.Proton;
using Spectre.Console;

class Program
{
    public static ConfigEntity<ConfigEntry> Config { get; set; }
    static async Task Main(string[] args)
    {
        Config = new ConfigEntity<ConfigEntry>(PathsList.ConfigPath);
        if (!LinuxDisplayServerDetector.IsX11() && 
            Config.Data.EnableX11Detector)
        {
            Console.WriteLine("当前系统可能正在使用非 X11 的图形窗口渲染系统，可能会导致游戏卡顿甚至系统卡顿。");
            Console.WriteLine("请考虑切换至 X11 图形窗口渲染系统。如不想再次看到此消息，请运行 bbl enable-x11-detector false");
        }
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
                case "enable-x11-detector":
                    string enableStr = args[++i];
                    if (bool.TryParse(enableStr, out bool enable))
                    {
                        Config.Data.EnableX11Detector = enable;
                        Console.WriteLine($"X11 检测启用状态：{enable}");
                    }
                    else
                    {
                        Console.WriteLine($"无效字符串：{enableStr}");
                    }
                    break;
                case "help":
                case "h":
                    PrintHelp();
                    break;
                case "launch":
                case "l":
                    if (i + 1 < args.Length)
                    {
                        string gameFolder = args[++i];
                        await LaunchGameFlow(gameFolder);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]错误:[/] launch 需要指定游戏目录。");
                    }
                    break;

                case "install":
                case "i":
                    if (i + 1 < args.Length)
                    {
                        string version = args[++i];
                        InstallGame(version);
                    }
                    break;

                case "install-list":
                    ViewAllVersions();
                    break;

                case "game-list":
                    ViewInstalledVersions();
                    break;

                default:
                    AnsiConsole.MarkupLine($"[yellow]未知指令:[/] {arg}");
                    PrintHelp();
                    break;
            }
        }
    }

    // 封装启动流，确保逻辑顺序不变
    static async Task LaunchGameFlow(string gameFolder)
    {
        string protonWorkPath = Path.Combine(PathsList.ProtonPath, "work");
        string protonBinPath = Path.Combine(protonWorkPath, "GDK-Proton10-32");

        // 原本的检测逻辑：如果不存在则下载
        if (!Directory.Exists(protonBinPath))
        {
            AnsiConsole.MarkupLine("[yellow]Proton 环境缺失，开始准备环境...[/]");
            await DownloadProtonWithBottomBar();
        }

        // 启动逻辑
        await LaunchWithBottomBar(gameFolder, protonWorkPath);
    }

    // 1. 下载部分：原本逻辑不变，仅更换进度条表现
    static async Task DownloadProtonWithBottomBar()
    {
        var downloader = new ProtonDownloader();

        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[] 
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]正在下载 Proton 组件[/]");
                
                // 依然使用原本的 Progress<T> 逻辑
                var progress = new Progress<DownloadProgress>(s =>
                {
                    task.Value = s.ProgressPercentage;
                });

                await downloader.Download(progress);
            });
        
        AnsiConsole.MarkupLine("[green]环境下载并解压完成。[/]");
    }

    // 2. 启动部分：加上底部进度感官
    static async Task LaunchWithBottomBar(string gameFolder, string protonWorkPath)
    {
        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[] 
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]正在启动游戏进程...[/]");
                task.IsIndeterminate = true; // 类似 apt 加载时的往返滚动效果

                // 原本的启动逻辑
                var launch = new EasyLaunch(new()
                {
                    GamePath = Path.Combine(gameFolder, "Minecraft.Windows.exe"),
                    PrefixPath = Path.Combine(protonWorkPath, "game_prefix"),
                    ProtonPath = Path.Combine(protonWorkPath, ProtonDownloader.ProtonVersion)
                });

                // 在进度条上方输出原本的文本
                AnsiConsole.MarkupLine($"[grey]GamePath:[/] {gameFolder}");
                
                // 执行原本的 Launch
                await Task.Run(() => launch.Launch());
                
                task.Value = 100;
                task.StopTask();
            });
            
        AnsiConsole.MarkupLine("[bold blue]游戏已拉起。[/]");
    }

    private static void PrintHelp()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("命令");
        table.AddColumn("参数");
        table.AddColumn("说明");
        table.AddRow("help / h", "", "显示帮助");
        table.AddRow("launch / l", "<folder>", "启动游戏");
        table.AddRow("install / i", "<version>", "安装游戏");
        table.AddRow("game-list", "", "已安装列表");
        table.AddRow("install-list", "<option>", "下载列表 (默认全显示)");
        table.AddRow("", "release", "所有正式版");
        table.AddRow("", "preview", "所有预览版");
        table.AddRow("", "search <key>", "搜索指定关键词的版本");
        table.AddRow("enable-x11-detector", "<bool>", "设置是否启用 X11 检测");
        table.AddRow("", "false/true", "");
        AnsiConsole.Write(table);
    }

    private static void InstallGame(string v) => AnsiConsole.MarkupLine($"正在安装 [green]{v}[/]...");
    private static void ViewAllVersions() => AnsiConsole.MarkupLine("获取版本列表中...");

    private static void ViewInstalledVersions()
    {
        var games = InstanceHelper.GetVersionConfigs(PathsList.LinuxGamePath);
        if (games == null || !games.Any())
        {
            AnsiConsole.MarkupLine("[red]未找到已安装的版本。[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("名称");
        table.AddColumn("版本");
        table.AddColumn("路径");

        int id = 1;
        foreach (var game in games)
        {
            table.AddRow(id.ToString(), game.Info?.VersionName ?? "未知", game.Info?.Version ?? "未知", game.VersionPath);
            id++;
        }
        AnsiConsole.Write(table);
    }
}