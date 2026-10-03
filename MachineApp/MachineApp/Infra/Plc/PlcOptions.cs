// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：PlcOptions。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Text.Json、Communication.Core.Abstractions、Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Text.Json;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;

namespace MachineApp.Infra.Plc;

/// <summary>
/// Packing Machine PLC 连接/超时/心跳配置（对应 Config/PLC.json）。
/// 点位清单仍由 Config/PLCIO.csv 单独维护，本类只承载连接参数。
/// </summary>
public sealed class PlcOptions
{
    /// <summary>MC 后端：MxComponent（产线）或 Slmp（TCP 直连，后续切换保留）。</summary>
    public string Backend { get; set; } = "MxComponent";

    /// <summary>MX Component 逻辑站号（0-1023）。</summary>
    public int MxLogicalStationNumber { get; set; } = 0;

    /// <summary>
    /// MX 逻辑站密码。默认不写入仓库配置；有密码的产线由部署侧覆盖。
    /// </summary>
    public string MxPassword { get; set; } = string.Empty;

    /// <summary>SLMP 服务器 IP（TCP 模式）。</summary>
    public string ServerIP { get; set; } = "192.168.1.1";

    /// <summary>SLMP 端口（MC TCP 默认 5000/4999）。</summary>
    public int Port { get; set; } = 5000;

    /// <summary>通用通信超时（毫秒），同时作为 MX 后端的 CommunicationTimeout。</summary>
    public int TimeoutMs { get; set; } = 1000;

    /// <summary>Online 心跳间隔（毫秒）。</summary>
    public int HeartbeatIntervalMs { get; set; } = 500;

    /// <summary>握手等待超时（毫秒）。初始保持旧 TriggerAsync 的 200ms 语义。</summary>
    public int HandshakeTimeoutMs { get; set; } = 200;

    /// <summary>
    /// 32-bit 数值字顺序（ABCD/BADC/CDAB/DCBA），集中管理、禁止散落硬编码。
    /// 初始候选 CDAB；提交前必须用已知 PLC 样本做旧行为对照（见 PlcService 注释）。
    /// </summary>
    public string DataFormat { get; set; } = "CDAB";

    /// <summary>加载配置文件；文件缺失时返回默认配置（由调用方记录警告）。</summary>
    public static PlcOptions Load(string path)
    {
        if (!File.Exists(path))
        {
            return new PlcOptions();
        }

        var json = File.ReadAllText(path);
        var options = JsonSerializer.Deserialize<PlcOptions>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return options ?? new PlcOptions();
    }

    /// <summary>解析后端配置；非法值启动即失败。</summary>
    public McClientBackend ParsedBackend() =>
        Backend.Trim() switch
        {
            var b when b.Equals("MxComponent", StringComparison.OrdinalIgnoreCase) => McClientBackend.MxComponent,
            var b when b.Equals("Slmp", StringComparison.OrdinalIgnoreCase) => McClientBackend.Slmp,
            _ => throw new InvalidOperationException(
                $"PLC.json 的 Backend 非法：'{Backend}'，只支持 MxComponent / Slmp。"),
        };

    /// <summary>解析数据格式配置；非法值启动即失败。</summary>
    public Communication.Core.Abstractions.DataFormat ParsedDataFormat() =>
        DataFormat.Trim().ToUpperInvariant() switch
        {
            "ABCD" => Communication.Core.Abstractions.DataFormat.ABCD,
            "BADC" => Communication.Core.Abstractions.DataFormat.BADC,
            "CDAB" => Communication.Core.Abstractions.DataFormat.CDAB,
            "DCBA" => Communication.Core.Abstractions.DataFormat.DCBA,
            _ => throw new InvalidOperationException(
                $"PLC.json 的 DataFormat 非法：'{DataFormat}'，只支持 ABCD/BADC/CDAB/DCBA。"),
        };

    /// <summary>按当前选项构造 McClientDevice 参数。</summary>
    public McParam ToMcParam()
    {
        if (TimeoutMs <= 0)
        {
            throw new InvalidOperationException($"PLC.json 的 TimeoutMs 必须大于零，当前为 {TimeoutMs}。");
        }

        return new McParam
        {
            Backend = ParsedBackend(),
            MxLogicalStationNumber = MxLogicalStationNumber,
            MxPassword = MxPassword ?? string.Empty,
            ServerIP = ServerIP,
            Port = Port,
            TimeoutMs = TimeoutMs,
        };
    }
}
