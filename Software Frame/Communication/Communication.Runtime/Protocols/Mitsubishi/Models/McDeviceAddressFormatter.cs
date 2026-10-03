// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：McDeviceAddressFormatter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、System、System.Globalization。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using System;
using System.Globalization;

namespace Communication.Runtime.Protocols.Mitsubishi;

/// <summary>集中维护 MC/MX 设备编号的进制和字符串格式。</summary>
internal static class McDeviceAddressFormatter
{
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetRadix。
    /// </summary>
    public static int GetRadix(string prefix) => prefix switch
    {
        "X" or "Y" or "B" or "W" or "SB" or "SW" => 16,
        _ => 10,
    };

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Format。
    /// 正文中的关键协作调用包括 nameof、GetRadix、ToString；具体条件和先后次序见方法体。
    /// </summary>
    public static string Format(string prefix, int number)
    {
        if (number is < 0 or > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }
        return prefix + (GetRadix(prefix) == 16
            ? number.ToString("X", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture));
    }
}
