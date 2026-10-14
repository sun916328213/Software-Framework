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
    /// 存的是**运行时自己那张表**的当前状态 —— 调用方在界面上改了什么，这里一概看不到。
    /// </summary>
    void SaveReports();
    /// <summary>
    /// 执行保存操作；对应入口为 SaveEvents。
    /// 同 <see cref="SaveReports()"/>：存的是运行时自己的表，不含调用方的编辑。
    /// </summary>
    void SaveEvents();

    /// <summary>
    /// 把 <paramref name="reports"/> 给出的报表定义写入配置并落盘。
    /// <para>
    /// 这是页面上编辑内容唯一能落盘的路径：<see cref="SaveReports()"/> 只序列化运行时内存里那份，
    /// 而 <see cref="LoadReports"/> 返回的是映射出来的快照，两者之间没有任何连接。
    /// </para>
    /// </summary>
    void SaveReports(IEnumerable<SecsGemReportDefinition> reports);

    /// <summary>
    /// 把 <paramref name="events"/> 给出的事件定义写入配置并落盘。语义同 <see cref="SaveReports(IEnumerable{SecsGemReportDefinition})"/>。
    /// </summary>
    void SaveEvents(IEnumerable<SecsGemEventDefinition> events);
}

/// <summary>
/// SecsGemReportDefinition 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public sealed record SecsGemReportDefinition(string Id, string Name, string VariableIds);

/// <summary>
/// SecsGemEventDefinition 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public sealed record SecsGemEventDefinition(string Id, string Name, string ReportIds, bool Enabled);
