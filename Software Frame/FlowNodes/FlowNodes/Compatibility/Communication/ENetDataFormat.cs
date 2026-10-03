// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。
// 本文件定义：ENetDataFormat。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowNodes、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using FlowNodes;
using System.ComponentModel;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 新通讯节点的数据收发格式。
/// </summary>
public enum ENetDataFormat
{
    /// <summary>UTF8 文本：直接按 UTF8 编解码字符串。</summary>
    [Description("UTF8")]
    UTF8 = 0,

    /// <summary>HEX：如 "01 03 00 00 00 02"，解析时容忍空格/横杠/0x 前缀。</summary>
    [Description("HEX")]
    Hex = 1,
}
