using System.Text.Json;
using System.Text.Json.Serialization;

namespace HMISim.ProjectModel;

/// <summary>
/// 工程文件的读写：JSON、缩进、枚举以字符串存。
/// 版本策略（规范 v0.2 第 6 节）：新版读旧版（缺字段取默认）；文件版本高于程序支持时拒绝并明确报错，不带病打开。
/// </summary>
public static class ProjectFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>保存工程到文件（UTF-8）。</summary>
    public static void Save(string path, ProjectDocument document)
    {
        var json = JsonSerializer.Serialize(document, Options);
        File.WriteAllText(path, json);
    }

    /// <summary>序列化为字符串（测试/预览用）。</summary>
    public static string Serialize(ProjectDocument document) => JsonSerializer.Serialize(document, Options);

    /// <summary>从文件加载工程。</summary>
    /// <exception cref="InvalidOperationException">文件版本高于支持版本时抛出，消息含双方版本号。</exception>
    public static ProjectDocument Load(string path) => Deserialize(File.ReadAllText(path));

    /// <summary>从字符串解析工程。</summary>
    public static ProjectDocument Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<ProjectDocument>(json, Options)
            ?? throw new InvalidOperationException("工程文件内容为空或格式不正确");
        if (document.SchemaVersion > ProjectDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"工程格式版本 {document.SchemaVersion} 高于本程序支持的 {ProjectDocument.CurrentSchemaVersion}，请升级组态端/运行端后再打开");
        }

        return document;
    }
}
