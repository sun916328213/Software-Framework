// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemAlarm。成员说明可结合下方具体实现阅读。
namespace SecsGem.Core;

/// <summary>S5F1 报警数据；Code 为 1 表示报警、0 表示复位。</summary>
public sealed record SecsGemAlarm(string Id, string Description, byte Code, bool Enabled = true);
