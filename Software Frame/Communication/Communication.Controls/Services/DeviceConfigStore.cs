// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DeviceConfigStore、StoredDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls、System.IO、System.Text.Json、Communication.Core、Communication.Runtime.Core。导入不代表所有类型都会在运行时加载。
using Communication.Controls;
using System.IO;
using System.Text.Json;
using Communication.Core;
using Communication.Runtime.Core;

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

    /// <summary>
    /// StoredDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
    /// </summary>
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
                var type = global::Extension.Core.Reflection.FrameworkTypeResolver.ResolvePersistedType(item.ParamType);
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Serialize。
    /// 正文中的关键协作调用包括 Serialize、GetType；具体条件和先后次序见方法体。
    /// </summary>
    private static string Serialize(CommunicationParam param) =>
        JsonSerializer.Serialize(param, param.GetType(), Options);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Deserialize。
    /// 正文中的关键协作调用包括 Deserialize、Parse、TryGetProperty、GetInt32；具体条件和先后次序见方法体。
    /// </summary>
    private static CommunicationParam Deserialize(string json, Type type)
    {
        var parameter = (CommunicationParam)JsonSerializer.Deserialize(json, type, Options)!;
        if (parameter is Communication.Core.Protocols.Http.HttpParam http)
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("ServerRequestCapacity", out _) &&
                document.RootElement.TryGetProperty("MaxConnections", out var oldCapacity))
                http.ServerRequestCapacity = oldCapacity.GetInt32();
        }
        return parameter;
    }

    /// <summary>按协议给出参数 CLR 类型（供 UI 做类型提示）。</summary>
    public static Type ParamTypeFor(ProtocolType protocol, DeviceRole role) =>
        ProtocolCatalog.CreateParam(protocol, role).GetType();
}
