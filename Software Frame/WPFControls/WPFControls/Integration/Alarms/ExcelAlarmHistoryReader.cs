// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ExcelAlarmHistoryReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Excel.Definition、DataManager.Runtime.Excel.EPPlus、System.Globalization、WPFControls.Queries.Alarms、WPFControls.ProgramControl.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using DataManager.Core.Excel.Definition;
using DataManager.Runtime.Excel.EPPlus;
using System.Globalization;
using WPFControls.Queries.Alarms;
using WPFControls.ProgramControl.Models;

namespace WPFControls.Integration.Alarms;

/// <summary>Each read owns its workbook parameters, separate from the live alarm writer.</summary>
/// <remarks>
/// ExcelAlarmHistoryReader 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：IAlarmHistoryReader。具体约束按这些契约及各成员实现解释。
/// </remarks>
internal sealed class ExcelAlarmHistoryReader(Func<string> getDirectory) : IAlarmHistoryReader
{
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadDayAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、getDirectory、ToString、QueryAsync、ToArray；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task<IReadOnlyList<AlarmHistoryRow>> ReadDayAsync(DateTime day, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var excel = new EPPlusHelper
        {
            ExcelParam = new ExcelParam
            {
                Name = "AlarmInfo", FilePath = getDirectory(),
                FileName = day.ToString("yyyyMMdd", CultureInfo.CurrentCulture) + ".xlsx", ToolType = ExcelType.EPPlus
            }
        };
        var rows = await excel.QueryAsync<AlarmInfoDTO>();
        cancellationToken.ThrowIfCancellationRequested();
        return rows.Select(row => new AlarmHistoryRow(row.ID, row.Info, row.StartTime, row.EndTime, row.Duration)).ToArray();
    }
}
