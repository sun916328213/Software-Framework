using System.IO;
using System.Text.Json;
using CommunicationNew.Core;

namespace Communication.Controls.Services;

/// <summary>
/// 设备配置持久化 —— 把 <see cref="CommunicationNew"/> 具体 Param 按协议类型 JSON 化，
/// 存到本地文件，下次打开恢复。全部协议走 CommunicationNew 的 Param/Device 工厂，不存在旁路。
/// </summary>
public static class DeviceConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private sealed record StoredDevice(string ParamType, string Json);

    /// <summary>按设备当前状态深拷贝其 Param（JSON 往返，保证持久化与恢复一致）。</summary>
    public static CommunicationParam CloneParamFor(ICommunicationDevice device)
    {
        var param = ExtractLiveParam(device)
            ?? ProtocolCatalog.CreateParam(device.ProtocolType, device.Role);
        var json = Serialize(param);
        return Deserialize(json, param.GetType());
    }

    /// <summary>保存全部设备参数到文件。</summary>
    public static void Save(string filePath, IEnumerable<CommunicationParam> parameters)
    {
        var stored = parameters
            .Select(p => new StoredDevice(p.GetType().AssemblyQualifiedName!, Serialize(p)))
            .ToList();
        File.WriteAllText(filePath, JsonSerializer.Serialize(stored, Options));
    }

    /// <summary>从文件加载设备参数（逐条解析，坏条跳过并返回错误数）。</summary>
    public static (List<CommunicationParam> Params, int Skipped) Load(string filePath)
    {
        var result = new List<CommunicationParam>();
        var skipped = 0;
        var stored = JsonSerializer.Deserialize<List<StoredDevice>>(File.ReadAllText(filePath), Options);
        if (stored is null)
        {
            return (result, 0);
        }

        foreach (var item in stored)
        {
            try
            {
                var type = Type.GetType(item.ParamType);
                if (type is null || !typeof(CommunicationParam).IsAssignableFrom(type))
                {
                    skipped++;
                    continue;
                }
                result.Add(Deserialize(item.Json, type));
            }
            catch
            {
                skipped++;
            }
        }
        return (result, skipped);
    }

    /// <summary>从设备反查其创建参数：优先取设备暴露的参数快照，取不到则按协议/角色重建默认参数。</summary>
    private static CommunicationParam? ExtractLiveParam(ICommunicationDevice device)
    {
        // P11: 跨程序集反射 protected Param 字段已消除；Runtime 基类实现 ICommunicationParameterSource。
        if (device is ICommunicationParameterSource source)
        {
            return source.GetParameterSnapshot();
        }

        return null;
    }

    private static string Serialize(CommunicationParam param) =>
        JsonSerializer.Serialize(param, param.GetType(), Options);

    private static CommunicationParam Deserialize(string json, Type type) =>
        (CommunicationParam)JsonSerializer.Deserialize(json, type, Options)!;

    /// <summary>按协议给出参数 CLR 类型（供 UI 做类型提示）。</summary>
    public static Type ParamTypeFor(ProtocolType protocol, DeviceRole role) =>
        ProtocolCatalog.CreateParam(protocol, role).GetType();
}
