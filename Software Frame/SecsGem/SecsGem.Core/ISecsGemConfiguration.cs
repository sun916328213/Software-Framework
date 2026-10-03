// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ISecsGemConfiguration、SecsGemReportDefinition、SecsGemEventDefinition。成员说明可结合下方具体实现阅读。
namespace SecsGem.Core;

/// <summary>供配置界面读取和保存 GEM 报表、事件定义的契约。</summary>
public interface ISecsGemConfiguration
{
    /// <summary>
    /// 执行加载操作；对应入口为 LoadReports。
    /// </summary>
    IReadOnlyList<SecsGemReportDefinition> LoadReports();
    /// <summary>
    /// 执行加载操作；对应入口为 LoadEvents。
    /// </summary>
    IReadOnlyList<SecsGemEventDefinition> LoadEvents();
    /// <summary>
    /// 执行保存操作；对应入口为 SaveReports。
    /// </summary>
    void SaveReports();
    /// <summary>
    /// 执行保存操作；对应入口为 SaveEvents。
    /// </summary>
    void SaveEvents();
}

/// <summary>
/// SecsGemReportDefinition 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public sealed record SecsGemReportDefinition(string Id, string Name, string VariableIds);

/// <summary>
/// SecsGemEventDefinition 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public sealed record SecsGemEventDefinition(string Id, string Name, string ReportIds, bool Enabled);
