using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BedrockBoot.Linux.Models.Global;

public class JsonSerializerOption
{
    public static JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip, // 忽略注释
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        AllowTrailingCommas = true, // 可选：也允许JSON末尾的逗号[citation:1]
        WriteIndented = true
    };
}