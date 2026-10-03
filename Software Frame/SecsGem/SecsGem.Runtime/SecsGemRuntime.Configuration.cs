// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;

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
}
