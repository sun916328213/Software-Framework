// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfoQueryViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、System.Collections.ObjectModel、WPFControls.Queries.Alarms。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WPFControls.Queries.Alarms;
using WPFControls.Charts.Models;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.ViewModels.Alarm;

/// <summary>
/// AlarmInfoQueryViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
/// </summary>
internal class AlarmInfoQueryViewModel : ObservableObject
{
    // Preserve existing XAML binding paths; state belongs to this page instance.
    public AlarmInfoQueryModel Datas { get; } = new();
    private ObservableCollection<ChartPieData> _pieData = new();
    public ObservableCollection<ChartPieData> PieData
    {
        get => _pieData;
        set => SetProperty(ref _pieData, value);
    }
    public IAsyncRelayCommand QueryCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>
    /// 初始化 AlarmInfoQueryViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 QueryAsync、Select；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public AlarmInfoQueryViewModel(IAlarmHistoryQueryService service)
    {
        QueryCommand = new AsyncRelayCommand(async cancellationToken =>
        {
            var result = await service.QueryAsync(Datas.DateTime_Start, Datas.DateTime_End, cancellationToken);
            // Resume on the WPF context and publish only after the entire query succeeds.
            Datas.Datas = new ObservableCollection<AlarmInfoDTO>(result.Rows.Select(row => new AlarmInfoDTO
            {
                ID = row.ID, Info = row.Info, StartTime = row.StartTime, EndTime = row.EndTime, Duration = row.Duration
            }));
            PieData = new ObservableCollection<ChartPieData>(result.Summaries.Select(summary => new ChartPieData
            {
                Value = summary.Count, SeriesName = summary.Info
            }));
        });
        // Export was previously unimplemented; preserve the command and binding contract.
        SaveCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }
}
