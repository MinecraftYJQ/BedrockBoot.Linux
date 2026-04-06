using BedrockBoot.Linux.Console.Entry;
using BedrockBoot.Linux.Console.Global;
using BedrockBoot.Linux.Entity;
using BedrockBoot.Linux.Entry.Progress;
using BedrockBoot.Linux.Models.Downloader;
using BedrockBoot.Linux.Models.Game;
using BedrockBoot.Linux.Models.Global;
using BedrockBoot.Linux.Models.Helper;
using BedrockBoot.Linux.Models.Pack.Game.Instance;
using BedrockBoot.Linux.Models.Proton;
using BedrockLauncher.Core.CoreOption;
using BedrockLauncher.Core.Linux;
using BedrockLauncher.Core.Utils;
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
                var progress = new Progress<DownloadProgress>(s => { task.Value = s.ProgressPercentage; });

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
    // 首先获取版本数据库
    var database = await VersionsHelper.GetBuildDatabaseAsync("https://data.mcappx.com/v2/bedrock.json");
    if (database == null)
    {
        AnsiConsole.MarkupLine("[red]错误:[/] 无法获取版本数据库。");
        return;
    }

    var versions = database.Builds;

    // 处理版本列表，获取所有 GDK 版本
    var gdkVersions = versions
        .Where(v => v.Value.BuildType == MinecraftBuildTypeVersion.GDK)
        .Select(v => v.Value)
        .ToListAsync().Result;

    // 搜索匹配的版本（不区分大小写）
    var matchedVersions = gdkVersions
        .Where(v => v.ID.Contains(version, StringComparison.OrdinalIgnoreCase))
        .ToList();

    // 检查匹配结果
    if (matchedVersions.Count == 0)
    {
        AnsiConsole.MarkupLine($"[red]错误:[/] 未找到版本 '{version}'。");
        AnsiConsole.MarkupLine(
            $"[yellow]提示:[/] 使用 [cyan]bbl install-list[/] 查看所有可用版本，或使用 [cyan]bbl install-list <关键词>[/] 搜索版本。");
        return;
    }

    if (matchedVersions.Count > 1)
    {
        // 多个匹配，显示列表让用户选择
        AnsiConsole.MarkupLine($"[red]错误:[/] 版本 '{version}' 匹配到多个版本，请使用更精确的版本号。");
        AnsiConsole.MarkupLine($"[yellow]匹配到的版本:[/]");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("序号");
        table.AddColumn("ID");
        table.AddColumn("类型");
        table.AddColumn("发布日期");

        for (int i = 0; i < matchedVersions.Count; i++)
        {
            var v = matchedVersions[i];
            string typeColor = v.Type == MinecraftGameTypeVersion.Release ? "green" : "yellow";
            table.AddRow(
                (i + 1).ToString(),
                v.ID,
                $"[{typeColor}]{v.Type}[/]",
                v.Date
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[yellow]提示:[/] 请使用完整的版本号重新安装，例如: [cyan]bbl install " + matchedVersions.First().ID +
                               "[/]");
        return;
    }

    // 唯一匹配，开始安装
    var targetVersion = matchedVersions.First();
    AnsiConsole.MarkupLine($"[green]找到版本:[/] {targetVersion.ID} ({targetVersion.Type})");
    AnsiConsole.MarkupLine($"[green]发布日期:[/] {targetVersion.Date}");

    // 检查是否已存在缓存文件
    string tempDownloadPath = Path.Combine(PathsList.LinuxGamePath, "version_save");
    string cachedFilePath = Path.Combine(tempDownloadPath, $"{targetVersion.ID}.insPack");
    bool hasCacheFile = File.Exists(cachedFilePath);
    bool useCache = false;
    
    // 如果存在缓存文件，询问用户是否使用
    if (hasCacheFile)
    {
        var fileInfo = new FileInfo(cachedFilePath);
        var fileSize = FormatFileSize(fileInfo.Length);
        
        AnsiConsole.MarkupLine($"[yellow]检测到已存在的安装包缓存:[/]");
        AnsiConsole.MarkupLine($"  [blue]路径:[/] {cachedFilePath}");
        AnsiConsole.MarkupLine($"  [blue]大小:[/] {fileSize}");
        AnsiConsole.MarkupLine($"  [blue]修改时间:[/] {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        
        useCache = AnsiConsole.Confirm("是否使用此缓存文件进行安装？", defaultValue: true);
        
        if (useCache)
        {
            AnsiConsole.MarkupLine("[green]将使用缓存文件进行安装，跳过下载步骤。[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]将重新下载安装包。[/]");
            // 备份旧文件（可选）
            string backupPath = cachedFilePath + ".old";
            if (File.Exists(backupPath))
                File.Delete(backupPath);
            File.Move(cachedFilePath, backupPath);
            AnsiConsole.MarkupLine($"[grey]旧缓存已备份至: {backupPath}[/]");
        }
    }

    // 确认安装
    if (!AnsiConsole.Confirm($"是否安装版本 [cyan]{targetVersion.ID}[/]?"))
    {
        AnsiConsole.MarkupLine("[yellow]安装已取消。[/]");
        return;
    }

    // 开始下载和安装
    try
    {
        // 获取版本信息
        var buildInfo = targetVersion;

        // 创建游戏安装目录
        string gameInstallPath = Path.Combine(PathsList.LinuxGamePath, "bedrock_versions", targetVersion.ID);
        string fileSave = cachedFilePath; // 使用相同的缓存路径

        if (Directory.Exists(gameInstallPath))
        {
            AnsiConsole.MarkupLine($"[yellow]警告:[/] 目录 {gameInstallPath} 已存在。");
            if (!AnsiConsole.Confirm("是否覆盖安装?"))
            {
                AnsiConsole.MarkupLine("[yellow]安装已取消。[/]");
                return;
            }

            // 删除现有目录
            try
            {
                Directory.Delete(gameInstallPath, true);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]错误:[/] 无法删除现有目录: {ex.Message}");
                return;
            }
        }

        // 创建必要的目录
        Directory.CreateDirectory(gameInstallPath);
        Directory.CreateDirectory(tempDownloadPath);

        // 创建进度显示
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var downloadTask = ctx.AddTask("[green]下载进度[/]", new ProgressTaskSettings
                {
                    MaxValue = 100,
                    AutoStart = false
                });

                var extractTask = ctx.AddTask("[yellow]解压进度[/]", new ProgressTaskSettings
                {
                    MaxValue = 100,
                    AutoStart = false
                });

                // 如果使用缓存，跳过下载步骤
                if (!useCache || !hasCacheFile)
                {
                    // 下载文件
                    var url = buildInfo.Variations[0].MetaData[0];

                    AnsiConsole.MarkupLine($"[green]开始下载游戏文件...[/]");
                    AnsiConsole.MarkupLine($"[blue]下载地址:[/] {url}");
                    AnsiConsole.MarkupLine($"[blue]保存路径:[/] {fileSave}");

                    var downloader = new MultiThreadDownloader();
                    
                    // 开始下载
                    downloadTask.StartTask();

                    // 下载进度回调
                    var downloadProgress = new Progress<DownloadProgress>(p =>
                    {
                        downloadTask.Value = p.ProgressPercentage;
                        downloadTask.Description =
                            $"[green]下载进度[/] {p.ProgressPercentage:F1}% ({FormatFileSize(p.DownloadedBytes)}/{FormatFileSize(p.TotalBytes)})";

                        if (p.BytesPerSecond > 0)
                        {
                            downloadTask.Description += $" @ {FormatFileSize(p.BytesPerSecond)}/s";
                        }
                    });

                    // 执行下载
                    await downloader.DownloadAsync(url, fileSave, downloadProgress);
                    downloadTask.StopTask();
                }
                else
                {
                    // 使用缓存文件，验证文件完整性
                    AnsiConsole.MarkupLine($"[green]使用缓存文件:[/] {cachedFilePath}");
                    downloadTask.Value = 100;
                    downloadTask.Description = "[green]使用缓存文件，跳过下载[/]";
                    downloadTask.StopTask();
                    
                    // 可选：验证文件大小是否合理
                    var fileInfo = new FileInfo(cachedFilePath);
                    if (fileInfo.Length == 0)
                    {
                        throw new Exception("缓存文件大小为 0，文件可能已损坏");
                    }
                    AnsiConsole.MarkupLine($"[green]缓存文件大小: {FormatFileSize(fileInfo.Length)}[/]");
                }

                // 开始安装
                var installCompletionSource = new TaskCompletionSource<bool>();
                var cts = new CancellationTokenSource();

                // 安装进度回调
                var installProgress = new Progress<InstallStates>(state =>
                {
                    switch (state)
                    {
                        case InstallStates.Extracting:
                            extractTask.StartTask();
                            extractTask.Description = "[yellow]正在解压游戏文件...[/]";
                            break;
                        case InstallStates.Extracted:
                            extractTask.Value = 100;
                            extractTask.StopTask();
                            installCompletionSource.TrySetResult(true);
                            break;
                    }
                });

                // 解压进度回调
                var extractionProgress = new Progress<DecompressProgress>(p =>
                {
                    if (!extractTask.IsStarted)
                    {
                        extractTask.StartTask();
                    }

                    extractTask.Value = p.Percentage;
                    extractTask.Description =
                        $"[yellow]解压进度[/] {p.Percentage:F1}% ({p.CurrentCount}/{p.TotalCount} 文件)";
                });

                // 创建安装选项
                var installOptions = new LocalGamePackageOptions
                {
                    FileFullPath = fileSave,
                    Type = MinecraftBuildTypeVersion.GDK,
                    InstallDstFolder = gameInstallPath,
                    ExtractionProgress = extractionProgress,
                    InstallStates = installProgress,
                    CancellationToken = cts.Token,
                    GameTypeVersion = targetVersion.Type,
                    GameName = null
                };

                // 执行安装
                await GlobalModel.BedrockCore.InstallPackageAsync(installOptions);

                // 等待安装完成
                await installCompletionSource.Task;

                AnsiConsole.MarkupLine($"[green]✓ 版本 {targetVersion.ID} 安装完成！[/]");
                AnsiConsole.MarkupLine($"[blue]安装路径:[/] {gameInstallPath}");
                
                // 安装成功后，可选：清理旧的备份文件
                string backupPath = cachedFilePath + ".old";
                if (File.Exists(backupPath))
                {
                    if (AnsiConsole.Confirm("是否删除旧的缓存备份文件？", defaultValue: false))
                    {
                        File.Delete(backupPath);
                        AnsiConsole.MarkupLine("[green]已删除旧备份。[/]");
                    }
                }
            });
    }
    catch (OperationCanceledException)
    {
        AnsiConsole.MarkupLine("[yellow]安装已取消。[/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]安装失败:[/] {ex.Message}");
        AnsiConsole.MarkupLine($"[red]详细信息:[/] {ex.StackTrace}");
    }
}

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
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