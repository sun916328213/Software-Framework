// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：CapacityStatisticsPresentation。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、Extension.UI.Controls、Extension.Core.Conversion、Extension.Integration.MiniExcel、WPFControls.Queries.Data、WPFControls.Controls.Models.Charts。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using Extension.UI.Controls;
using Extension.Core.Conversion;
using Extension.Integration.MiniExcel;
using WPFControls.Queries.Data;
using WPFControls.Controls.Models.Charts;
using WPFControls.Controls.Resources;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Presentation.Data;

/// <summary>
/// CapacityStatisticsPresentation 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
internal static class CapacityStatisticsPresentation
{
    /// <summary>
    /// 执行保存操作；对应入口为 Save。
    /// 正文中的关键协作调用包括 SaveFile、ToDate、IsNullOrEmpty、ConfigureAwait、SaveToExcelAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    internal static async Task<bool> Save(CapacityStatisticsModel model, List<CapacityStatisticsRow> data)
    {
        if (data == null || data.Count == 0) return false;
        var path = FileDialogEx.SaveFile($"产能统计_{model.StartTime.ToDate()}-{model.EndTime.ToDate()}.xlsx");
        if (string.IsNullOrEmpty(path)) return false;
        await ExcelEx.SaveToExcelAsync(path, data).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Apply。
    /// 正文中的关键协作调用包括 Clear、Add、Select；具体条件和先后次序见方法体。
    /// </summary>
    internal static void Apply(CapacityStatisticsModel model, CapacityStatisticsResult<CapacityStatisticsRow> result)
    {
        model.CapacityStatisticsDatas.Clear();
        foreach (var row in result.Rows) model.CapacityStatisticsDatas.Add(row);
        model.TotalOutput = result.TotalOutput;
        model.GoodCount = result.GoodCount;
        model.DefectCount = result.DefectCount;
        model.YieldRate = result.YieldRate;
        model.RunTime = result.RunTime;
        model.UtilizationRate = result.UtilizationRate;
        var good = new LineSeries { SeriesName = "OK", Color = ColorHelper.Color_Green };
        var fail = new LineSeries { SeriesName = "NG", Color = ColorHelper.Color_Red };
        var total = new LineSeries { SeriesName = "Total", Color = ColorHelper.Color_Blue };
        foreach (var point in result.Trend)
        {
            good.Value.Add(point.Good);
            fail.Value.Add(point.Defect);
            total.Value.Add(point.Total);
        }
        model.AxisX = new ObservableCollection<string>(result.Trend.Select(point => point.Label));
        model.OutputTrend = new ObservableCollection<LineSeries> { good, fail, total };
    }
}
