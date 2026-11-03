using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vision.Controls.Models;
using Vision.Core.Tools;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>工程v1/v2清单编解码；无会话、文件或SDK操作。</summary>
internal static class VisionProjectCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
    // 只映射持久化字段；运行结果、流程身份和定位位姿不进入工具工程清单。
    public static VisionWorkbenchProject CreateProject(VisionToolConfiguration configuration,
        ImageViewKind imageView, bool showResultBounds, bool showResultCenters, string imageSource = "") => new()
    {
        Tool = configuration.ToolIdentity, Parameters = configuration.Parameters, Provider = configuration.Provider,
        Rois = configuration.Rois, SelectedRoiIndex = configuration.SelectedRoiIndex,
        ImageView = imageView, ShowResultBounds = showResultBounds, ShowResultCenters = showResultCenters,
        ImageSource = imageSource, TemplateImage = configuration.TemplateImage,
        TrainingParameters = configuration.TrainingParameters, ModelReference = configuration.ModelReference
    };
    public static string Encode(VisionWorkbenchProject project) => JsonSerializer.Serialize(project, Options);
    public static JsonElement EncodeParameters(object parameters) => JsonSerializer.SerializeToElement(parameters, parameters.GetType(), Options);
    public static VisionWorkbenchProject Decode(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var project = JsonSerializer.Deserialize<VisionWorkbenchProject>(json, Options)
            ?? throw new InvalidDataException("工程文件为空或格式无效。");
        if (project.Version is not (1 or 2)) throw new InvalidDataException($"不支持的工程版本：{project.Version}。");
        if (project.Version == 1 && project.InputImage != null) throw new InvalidDataException("v1工程不能声明包内图像。");
        if (!Enum.IsDefined(project.ImageView)) throw new InvalidDataException($"工程包含未知图像视图：{project.ImageView}。");
        if (project.Rois == null || project.Rois.Any(roi => roi == null || roi.IsEmpty || !Enum.IsDefined(roi.Kind)))
            throw new InvalidDataException("工程包含无效 ROI。");
        if (project.SelectedRoiIndex < -1 || project.SelectedRoiIndex >= project.Rois.Count)
            throw new InvalidDataException("工程包含无效的选中 ROI 索引。");
        return project;
    }
    public static void ApplyParameters(JsonElement? json, object target) => PrepareParameters(json, target)();
    // 先反序列化和读取全部值，非法JSON不得写入原DTO的前半部分。
    public static Action PrepareParameters(JsonElement? json, object target)
    {
        if (json is not { } value) return () => { };
        var incoming = JsonSerializer.Deserialize(value.GetRawText(), target.GetType(), Options)
            ?? throw new InvalidDataException("工程参数为空。");
        List<(PropertyInfo Property, object? Value)> values = [];
        foreach (var property in target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0 ||
                property.IsDefined(typeof(JsonIgnoreAttribute), true)) continue;
            values.Add((property, property.GetValue(incoming)));
        }
        return () => { foreach (var (property, propertyValue) in values) property.SetValue(target, propertyValue); };
    }
}
