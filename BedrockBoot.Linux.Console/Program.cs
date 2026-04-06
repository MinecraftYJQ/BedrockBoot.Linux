using BedrockBoot.Linux.Console.Entry;
using BedrockBoot.Linux.Console.Global;
using BedrockBoot.Linux.Entity;
using BedrockBoot.Linux.Entry.Progress;
using BedrockBoot.Linux.Models.Game;
using BedrockBoot.Linux.Models.Global;
using BedrockBoot.Linux.Models.Helper;
using BedrockBoot.Linux.Models.Pack.Game.Instance;
using BedrockBoot.Linux.Models.Proton;
using BedrockLauncher.Core.Linux;
using BedrockLauncher.Core.VersionJsons;
using Spectre.Console;

class Program
{
    public static ConfigEntity<ConfigEntry> Config { get; set; }
    static async Task Main(string[] args)
    {
        Config = new ConfigEntity<ConfigEntry>(PathsList.ConfigPath);
        GlobalModel.BedrockCore = new BedrockCore();
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
                    if (i + 1 < args.Length)
                    {
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
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]错误:[/] enable-x11-detector 需要指定 true/false");
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
                        await InstallGame(version);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]错误:[/] install 需要指定版本号。");
                    }
                    break;

                case "install-list":
                    // 处理 install-list，如果有参数则作为搜索关键词
                    string searchKey = "";
                    if (i + 1 < args.Length)
                    {
                        searchKey = args[i + 1];
                        i++; // 消耗掉关键词参数
                    }
                    await ViewAllVersions(searchKey);
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
        table.AddRow("install-list", "", "显示所有可用版本");
        table.AddRow("install-list", "<关键词>", "搜索包含关键词的版本");
        table.AddRow("enable-x11-detector", "<bool>", "设置是否启用 X11 检测");
        AnsiConsole.Write(table);
    }

    private static async Task InstallGame(string version)
    {
        // 实现安装逻辑
        AnsiConsole.MarkupLine($"正在安装 [green]{version}[/]...");
        // TODO: 实现具体的安装逻辑
        await Task.CompletedTask;
    }

    private static async Task ViewAllVersions(string searchKey = "")
    {
        // 显示加载状态
        await AnsiConsole.Status()
            .StartAsync("正在获取版本列表...", async ctx =>
            {
                // 获取版本数据
                var database = await VersionsHelper.GetBuildDatabaseAsync("https://data.mcappx.com/v2/bedrock.json");
                if (database == null)
                {
                    AnsiConsole.MarkupLine("[red]错误:[/] 无法获取版本数据库。");
                    return;
                }
                
                var versions = database.Builds;
                
                // 处理版本列表
                var gdkVersions = versions
                    .Where(v => v.Value.BuildType == MinecraftBuildTypeVersion.GDK)
                    .Select(v => v.Value)
                    .ToListAsync().Result;
                
                // 按搜索关键词过滤
                if (!string.IsNullOrEmpty(searchKey))
                {
                    gdkVersions = gdkVersions
                        .Where(v => v.ID.Contains(searchKey, StringComparison.OrdinalIgnoreCase) ||
                                    v.Type.ToString().Contains(searchKey, StringComparison.OrdinalIgnoreCase) ||
                                    v.Date.Contains(searchKey, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                
                // 显示结果
                if (!gdkVersions.Any())
                {
                    if (!string.IsNullOrEmpty(searchKey))
                    {
                        AnsiConsole.MarkupLine($"[yellow]未找到包含关键词 '{searchKey}' 的版本。[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]未找到任何版本。[/]");
                    }
                    return;
                }
                
                // 创建表格
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn("ID");
                table.AddColumn("类型");
                table.AddColumn("发布日期");
                
                // 设置表格标题
                if (!string.IsNullOrEmpty(searchKey))
                {
                    table.Caption = new TableTitle($"搜索结果: '{searchKey}' ({gdkVersions.Count} 个匹配)");
                }
                else
                {
                    table.Caption = new TableTitle($"所有版本 ({gdkVersions.Count} 个)");
                }
                
                // 添加行（倒序显示，最新的在前面）
                foreach (var v in gdkVersions.OrderByDescending(v => v.Date))
                {
                    // 根据类型设置颜色
                    string typeColor = v.Type == MinecraftGameTypeVersion.Release ? "green" : "yellow";
                    table.AddRow(
                        v.ID, 
                        $"[{typeColor}]{v.Type}[/]", 
                        v.Date
                    );
                }
                
                AnsiConsole.Write(table);
            });
    }

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