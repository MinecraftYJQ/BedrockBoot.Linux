using BedrockBoot.Linux.Entity;
using BedrockBoot.Linux.Enum;

namespace BedrockBoot.Linux.Entry;

public class ConfigSaveEventArgs<T> : EventArgs where T : new()
{
    public ConfigSaveEventArgs(ConfigEntity<T> config, SavePhase phase)
    {
        Config = config;
        Phase = phase;
        Timestamp = DateTime.Now;
    }

    public ConfigEntity<T> Config { get; }
    public SavePhase Phase { get; }
    public DateTime Timestamp { get; }
}