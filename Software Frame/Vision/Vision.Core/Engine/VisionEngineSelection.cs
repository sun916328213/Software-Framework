#nullable enable

namespace Vision.Core.Engine;

/// <summary>
/// 中立的唯一后端选择值；配置绑定、已部署实现验证与DI注册不属于Core。
/// </summary>
public sealed class VisionEngineSelection
{
    /// <summary>
    /// 传统视觉实现的稳定标识。
    /// </summary>
    public string VisionBackendId
    {
        get;
    }

    /// <summary>
    /// 人工智能实现的稳定标识；未启用时为空。
    /// </summary>
    public string? AiBackendId
    {
        get;
    }

    /// <summary>
    /// 人工智能推理设备的明确标识；未启用时为空。
    /// </summary>
    public string? AiDeviceId
    {
        get;
    }

    /// <summary>
    /// 创建明确的实现选择，不加载运行库或读取配置文件。
    /// </summary>
    public VisionEngineSelection(
        string visionBackendId,
        string? aiBackendId = null,
        string? aiDeviceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visionBackendId);
        if (visionBackendId != visionBackendId.Trim())
        {
            throw new ArgumentException("后端身份不能包含首尾空白。", nameof(visionBackendId));
        }

        if (aiBackendId != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(aiBackendId);
            ArgumentException.ThrowIfNullOrWhiteSpace(aiDeviceId);
            if (aiBackendId != aiBackendId.Trim() || aiDeviceId != aiDeviceId.Trim())
            {
                throw new ArgumentException("AI后端和设备必须为明确的无首尾空白身份。");
            }
        }
        else if (aiDeviceId != null)
        {
            throw new ArgumentException("未启用AI时不能指定AI设备。", nameof(aiDeviceId));
        }

        VisionBackendId = visionBackendId;
        AiBackendId = aiBackendId;
        AiDeviceId = aiDeviceId;
    }
}
