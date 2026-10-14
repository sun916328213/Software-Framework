// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime.Variable;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecsGem.Runtime;

/// <summary>
/// SecsGemRuntime 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// 继承或实现的契约：ISecsGemConfiguration。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class SecsGemRuntime : ISecsGemConfiguration
{
    /// <summary>
    /// 执行加载操作；对应入口为 LoadReports。
    /// 正文中的关键协作调用包括 Load、ToArray、Select、GetDatas；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<SecsGemReportDefinition> LoadReports()
    {
        _vidManager.ReportID.Load();
        return _vidManager.ReportID.GetDatas()
            .Select(x => new SecsGemReportDefinition(x.ID, x.Name, x.VID_Collection))
            .ToArray();
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadEvents。
    /// 正文中的关键协作调用包括 Load、ToArray、Select、GetDatas；具体条件和先后次序见方法体。
    /// </summary>
    public IReadOnlyList<SecsGemEventDefinition> LoadEvents()
    {
        _vidManager.CEID.Load();
        return _vidManager.CEID.GetDatas()
            .Select(x => new SecsGemEventDefinition(x.ID, x.Name, x.ReportID_Collection, x.Enable))
            .ToArray();
    }

    /// <summary>
    /// 执行保存操作；对应入口为 SaveReports。
    /// 正文中的关键协作调用包括 Save；具体条件和先后次序见方法体。
    /// </summary>
    public void SaveReports() => _vidManager.ReportID.Save();

    /// <summary>
    /// 执行保存操作；对应入口为 SaveEvents。
    /// 正文中的关键协作调用包括 Save；具体条件和先后次序见方法体。
    /// </summary>
    public void SaveEvents() => _vidManager.CEID.Save();

    /// <summary>
    /// 把 <paramref name="reports"/> 写入 <c>ReportID</c> 表并落盘。
    /// </summary>
    public void SaveReports(IEnumerable<SecsGemReportDefinition> reports)
    {
        var table = _vidManager.ReportID;
        EnsureLoaded(table);
        Merge(table, reports as IReadOnlyList<SecsGemReportDefinition> ?? reports.ToArray(),
            idOfItem: x => x.ID,
            idOfDef: x => x.Id,
            create: x => new ReportInfo { ID = x.Id, Name = x.Name, VIDID = SplitList(x.VariableIds) },
            apply: (item, def) => { item.Name = def.Name; item.VIDID = SplitList(def.VariableIds); });
        table.Save();
    }

    /// <summary>
    /// 把 <paramref name="events"/> 写入 <c>CEID</c> 表并落盘。
    /// </summary>
    public void SaveEvents(IEnumerable<SecsGemEventDefinition> events)
    {
        var table = _vidManager.CEID;
        EnsureLoaded(table);
        Merge(table, events as IReadOnlyList<SecsGemEventDefinition> ?? events.ToArray(),
            idOfItem: x => x.ID,
            idOfDef: x => x.Id,
            create: x => new CEIDInfo { ID = x.Id, Name = x.Name, ReportID = SplitList(x.ReportIds), Enable = x.Enabled },
            apply: (item, def) =>
            {
                item.Name = def.Name;
                item.ReportID = SplitList(def.ReportIds);
                item.Enable = def.Enabled;
            });
        table.Save();
    }

    /// <summary>
    /// 表还空着才读盘。没读就 <c>Save()</c> 会把 <c>ReportID.csv</c> / <c>CEID.csv</c> 写成空表 ——
    /// 调用方完全可能没走过 <c>Load</c> 就点了保存，那属于静默截断配置文件。
    /// </summary>
    private static void EnsureLoaded<T>(DataBase<T> table) where T : new()
    {
        if (table.Datas.Count == 0) table.Load();
    }

    /// <summary>
    /// 把 <paramref name="incoming"/> 按 ID 合进 <paramref name="table"/>：已存在的改字段、
    /// 缺的新建、多余的删掉，落盘由调用方做。
    /// <para>
    /// 刻意**不就地替换整个 <c>Datas</c>**：<c>CEIDInfo.ReportInfo</c>（CEID.cs:36）持有
    /// <c>ReportInfo</c> 实例引用，换新实例会让 CEID 的缓存指向孤儿；<c>ReportInfo.VIDInfo</c>
    /// 字段也会一并丢掉（得靠 <c>ReportID.Update</c> 重建）。
    /// </para>
    /// </summary>
    private static void Merge<TItem, TDef>(
        DataBase<TItem> table,
        IReadOnlyList<TDef> incoming,
        Func<TItem, string> idOfItem,
        Func<TDef, string> idOfDef,
        Func<TDef, TItem> create,
        Action<TItem, TDef> apply)
        where TItem : new()
    {
        var existing = table.Datas.ToDictionary(idOfItem, StringComparer.Ordinal);
        foreach (var def in incoming)
        {
            if (!existing.Remove(idOfDef(def), out var item))
            {
                item = create(def);
                table.Datas.Add(item);
            }
            apply(item, def);
        }
        foreach (var orphan in existing.Values)
            table.Datas.Remove(orphan);
    }

    /// <summary>
    /// 逗号分隔串拆成列表。空串得到**空列表**而不是 <c>{""}</c> ——
    /// <c>ReportInfo.VID_Collection</c> 的 setter 直接 <c>Split(',')</c>，空串会留下一个空 ID，
    /// 之后 <c>ReportID.Update()</c> 会抛 "VID not exist in ReportID"。
    /// </summary>
    private static List<string> SplitList(string csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
}
