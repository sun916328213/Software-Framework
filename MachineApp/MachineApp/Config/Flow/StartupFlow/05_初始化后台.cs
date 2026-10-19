// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Script。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Controls、WPFControls.Controls.Resources、SecsGem.Core、Extension.Automation.Secs。导入不代表所有类型都会在运行时加载。
using Extension.UI.Controls;
using WPFControls.Controls.Resources;
using SecsGem.Core;
using Extension.Automation.Secs;
using MachineApp.Infra.Core;
using MachineApp.Infra.Tool;
using WPFControls.ProgramControl.Services;
using MachineApp.Data;
using WPFControls.ProgramControl.Models;
using Extension.IO.Retention;

public class Script
{
    ResourceMonitorService _ResourceMonitorService;
    BulletinBoardService _BulletinBoardService;
    StatisticService _StatisticService;
    AlarmInfoManagerService _AlarmInfoService;
    ISecsGemRuntime _secsRuntime;
    PLCTool _PLCMethod;

    public void Initialize()
    {
        // 初始化逻辑
        _ResourceMonitorService = ContainerManager.Resolve<ResourceMonitorService>();
        _BulletinBoardService = ContainerManager.Resolve<BulletinBoardService>();
        _StatisticService = ContainerManager.Resolve<StatisticService>();
        _AlarmInfoService = ContainerManager.Resolve<AlarmInfoManagerService>();
        _secsRuntime = ContainerManager.Resolve<ISecsGemRuntime>();
        _PLCMethod = ContainerManager.Resolve<PLCTool>();
    }

    public void Run()
    {
        Console.WriteLine("开始执行脚本");
        _ResourceMonitorService.StartAutoRefresh();


        DispatcherTimerEx.DispatcherTimer(new Action(Loop), 1000, "Loop");
    }

    private void Loop()
    {
        // 与 ReadCount 共用同一个单例；Resolve 失败不该让整个 Loop 崩掉。
        CapacityStatisticsService statistics = null;
        try { statistics = ContainerManager.Resolve<CapacityStatisticsService>(); }
        catch (Exception ex) { Console.WriteLine("产能统计服务不可用：" + ex.Message); return; }
        var now = DateTime.Now;

        if (now.Minute % 2 == 0 && now.Second == 0) // 5分钟读一次
        {
            // 更新每天统计信息：读真实产能数据。
            // 原先这里推的是硬编码占位值（OK={10} / NG={20,20,30,40,130,20,0} /
            // Total={120,120,130,140,230,120,0}），看板上显示的一直是假数字。
            // 必须自己 try/catch：异常冒到定时器回调里会打断整个 Loop，
            // 而数据库不可用是现场常见状态，不该因此崩掉。
            try
            {
                ReadCount(DateTime.Today, DateTime.Now, _BulletinBoardService.ShowData_Day);
            }
            catch (Exception ex)
            {
                // 保留看板上一次的数据，不用占位值盖回去。
                Console.WriteLine("读取日产能统计失败：" + ex.Message);
            }
        }
        else if (now.Hour == 0)
        {
            // 更新每月统计信息
            try
            {
                ReadCount(new DateTime(now.Year, now.Month, 1), now, _BulletinBoardService.ShowData_Month);
            }
            catch (Exception ex)
            {
                Console.WriteLine("读取月产能统计失败：" + ex.Message);
            }
        }


        // PLC 状态读真实连接状态；断线后自动反映 Offline。
        // 原先这里写死 ELinkStatus.Offline，下面的 if (Online) 分支因此永不执行。
        // 与 AutoProcess.cs:242 同一写法。
        ELinkStatus plcstatus = _PLCMethod.IsOnline ? ELinkStatus.Online : ELinkStatus.Offline;
        if (plcstatus == ELinkStatus.Online)
        {
            //await RefreshSystem();
            RefreshAlarm();
        }

        // 更新统计信息（在UI线程上执行）
        // 原先写死 "12345678"。StatisicsModel.ID 在全部 XAML 里没有任何显示位置（
        // 界面只消费 DateTime/OK/Total/PLCLinkStatus/SecsStatus/SecsMode），
        // 且没有配置项或设备序列号来源 —— 与其继续显示一个编造的值，不如置空。
        _StatisticService.UpdateID(string.Empty);

        // OK/总数改读真实产能数据（CapacityStatisticsService 的同一份查询结果）。
        // 原先写死 (10, 200)，BulletinBoardView 的 OKCount/TotalCount 绑的就是 StatisicsModel.OK/Total。
        // Model 是单例上的可变状态：Resolve 只拿到实例，不会自动查库。
        // 这里必须自己刷一次，否则上面 UpdateCount/UpdateRunTime 拿的是上一次（甚至从未查询过）的值。
        try
        {
            statistics.Model.StartTime = DateTime.Today;
            statistics.Model.EndTime = DateTime.Now;
            statistics.GetStatistics();
        }
        catch (Exception ex)
        {
            Console.WriteLine("刷新头部统计数据失败：" + ex.Message);
        }

        _StatisticService.UpdateCount(statistics.Model.GoodCount, statistics.Model.TotalOutput);
        // 运行时长原写死 800。CapacityStatisticsModel 有真实的 RunTime（小时）与 UtilizationRate（%），
        // 取自数据库聚合结果；报警时长仍是 AlarmInfoManagerService.TotalAlarmTime。
        _StatisticService.UpdateRunTime(_AlarmInfoService.TotalAlarmTime, statistics.Model.RunTime);
        var secsState = _secsRuntime.GetState();
        _StatisticService.UpdateSecsMode(secsState.Mode switch
        {
            SecsGemMode.OnlineLocal => ESecsMode.Online_Local,
            SecsGemMode.OnlineRemote => ESecsMode.Online_Remote,
            _ => ESecsMode.Offline
        });
        _StatisticService.UpdatePLC(plcstatus);
        _StatisticService.UpdateSecsStatus(secsState.IsConnected ? ELinkStatus.Online : ELinkStatus.Offline);
        _StatisticService.UpdateTime();


        // 在后台线程执行耗时操作
        Task.Run(() =>
        {
            // 每小时检查一次（当分钟为0且秒丼时）
            if (now.Minute / 10 == 0 && now.Second == 0)
            {
                ScheduledDeletion.DeleteExpiredFiles(GlobalData.Path_Image, GlobalData.SaveImageDay);
            }
            if (now.Second == 0)
                GC.Collect();
        });
    }
    /// <summary>
    /// 读真实产能数据并推给看板。
    /// <para>
    /// 用 WPFControls 已有的 <c>CapacityStatisticsService</c>：它内部的
    /// <c>CapacityStatisticsPresentation.Apply</c> 产出的正是 <c>OK</c> / <c>NG</c> / <c>Total</c>
    /// 三条 <c>ChartLineData</c>，且名称与配色（绿/红/蓝）和本看板一致 ——
    /// 这正是这些硬编码数值的来处。所以不必自己去拼 <c>OKCOunt1..24</c> 那 24 个小时桶。
    /// </para>
    /// </summary>
    private void ReadCount(DateTime from, DateTime to,
        Action<List<WPFControls.Charts.Models.ChartLineData>> publish)
    {
        var statistics = ContainerManager.Resolve<CapacityStatisticsService>();
        statistics.Model.StartTime = from;
        statistics.Model.EndTime = to;
        statistics.GetStatistics();          // 同步查库
        publish(statistics.Model.OutputTrend.ToList());
    }


    private void RefreshAlarm()
    {
        // 旧实现走 CommunicationManager.GetData("Alarm")，该能力已随旧通信栈删除。
        // 报警读取暂时停用：不伪造数据，待迁移到 Communication.Core 后恢复。
    }
}
