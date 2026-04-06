using System.Runtime.InteropServices;

namespace BedrockBoot.Linux.Models.Helper;

public static class LinuxDisplayServerDetector
{
    /// <summary>
    /// 检测当前 Linux 系统是否使用 X11
    /// </summary>
    /// <returns>
    /// true: 正在使用 X11
    /// false: 未使用 X11（可能是 Wayland、其他显示服务器，或非 Linux 系统）
    /// </returns>
    public static bool IsX11()
    {
        // 首先确保在 Linux 系统上
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;

        // 方法1：检查 XDG_SESSION_TYPE 环境变量（最可靠）
        string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (!string.IsNullOrEmpty(sessionType))
        {
            return sessionType.Equals("x11", StringComparison.OrdinalIgnoreCase);
        }

        // 方法2：检查 Wayland 特有的环境变量（反推）
        string waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (!string.IsNullOrEmpty(waylandDisplay))
        {
            // 如果 WAYLAND_DISPLAY 存在，说明正在使用 Wayland
            return false;
        }

        // 方法3：检查 X11 特有的环境变量
        string xdgVtnr = Environment.GetEnvironmentVariable("XDG_VTNR");
        string display = Environment.GetEnvironmentVariable("DISPLAY");
        
        // 典型的 X11 环境会有 DISPLAY 变量
        if (!string.IsNullOrEmpty(display) && display.StartsWith(":"))
        {
            return true;
        }

        // 兜底逻辑：无法明确判断，默认返回 false
        return false;
    }

    /// <summary>
    /// 获取当前显示服务器的详细类型信息
    /// </summary>
    /// <returns>返回字符串："x11", "wayland", "unknown" 或 "not-linux"</returns>
    public static string GetDisplayServerType()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "not-linux";

        // 优先检查 XDG_SESSION_TYPE
        string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (!string.IsNullOrEmpty(sessionType))
        {
            if (sessionType.Equals("x11", StringComparison.OrdinalIgnoreCase))
                return "x11";
            if (sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase))
                return "wayland";
            return sessionType.ToLowerInvariant(); // 其他类型
        }

        // 备选：检查 WAYLAND_DISPLAY
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return "wayland";

        // 备选：检查 DISPLAY
        string display = Environment.GetEnvironmentVariable("DISPLAY");
        if (!string.IsNullOrEmpty(display) && display.StartsWith(":"))
            return "x11";

        return "unknown";
    }

    /// <summary>
    /// 判断当前是否在 Wayland 环境下
    /// </summary>
    public static bool IsWayland()
    {
        string type = GetDisplayServerType();
        return type == "wayland";
    }
}