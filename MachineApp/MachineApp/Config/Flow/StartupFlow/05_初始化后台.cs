// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Script。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Controls、WPFControls.Controls.Resources、SecsGem.Core、Extension.Automation.Secs。导入不代表所有类型都会在运行时加载。
using Extension.UI.Controls;
using WPFControls.Controls.Resources;
using SecsGem.Core;
using Extension.Automation.Secs;

public class Script
{
    ResourceMonitorService _ResourceMonitorService;
    BulletinBoardService _BulletinBoardService;
    StatisticService _StatisticService;
    AlarmInfoManagerService _AlarmInfoService;
    ISecsGemRuntime _secsRuntime;

    public void Initialize()
    {
        // 初始化逻辑
        _ResourceMonitorService = ContainerManager.Resolve<ResourceMonitorService>();
        _BulletinBoardService = ContainerManager.Resolve<BulletinBoardService>();
        _StatisticService = ContainerManager.Resolve<StatisticService>();
        _AlarmInfoService = ContainerManager.Resolve<AlarmInfoManagerService>();
        _secsRuntime = ContainerManager.Resolve<ISecsGemRuntime>();
    }

    public void Run()
    {
        Console.WriteLine("开始执行脚本");
        _ResourceMonitorService.StartAutoRefresh();


        DispatcherTimerEx.DispatcherTimer(new Action(Loop), 1000, "Loop");
    }

    private void Loop()
    {
        var now = DateTime.Now;

        if (now.Minute % 2 == 0 && now.Second == 0)//5分钟读一次        {
            //读取OK NG
            //ReadCount();
            //更新每天统计信息
            _BulletinBoardService.ShowData_Day(new List<WPFControls.Controls.Models.Charts.LineSeries>
            {
                new WPFControls.Controls.Models.Charts.LineSeries {
                    SeriesName="OK",
                    Value=new System.Collections.ObjectModel.ObservableCollection<double>{ 10},
                    Color= ColorHelper.Color_Green,
                },
                new WPFControls.Controls.Models.Charts.LineSeries {
                    SeriesName="NG",
                    Value=new System.Collections.ObjectModel.ObservableCollection<double>{ 20,20,30,40,130,20,0},
                    Color= ColorHelper.Color_Red,
                },
                new WPFControls.Controls.Models.Charts.LineSeries {
                    SeriesName="Total",
                    Value=new System.Collections.ObjectModel.ObservableCollection<double>{ 120,120,130,140,230,120,0},
                    Color= ColorHelper.Color_Blue,
                }
            });
        }
        else if (now.Hour == 0)
        {
            /// 更新每月统计信息
            _BulletinBoardService.ShowData_Month(new List<WPFControls.Controls.Models.Charts.LineSeries>
            {
                new WPFControls.Controls.Models.Charts.LineSeries
                {
                    SeriesName = "OK",
                    Value = new System.Collections.ObjectModel.ObservableCollection<double> { 10, 120 },
                    Color = ColorHelper.Color_Blue,
                },
                new WPFControls.Controls.Models.Charts.LineSeries
                {
                    SeriesName = "NG",
                    Value = new System.Collections.ObjectModel.ObservableCollection<double> { 20, 20 },
                    Color = ColorHelper.Color_Red,
                }
            });
        }


        // 旧 MCHelper.IsConnected 已随旧通信栈删除；PLC 状态暂按 Offline 处理。
        var plcstatus = ELinkStatus.Offline;
        if (plcstatus == ELinkStatus.Online)
        {
            //await RefreshSystem();
            RefreshAlarm();
        }

        // 更新统计信息（在UI线程上执行）
        _StatisticService.UpdateID("12345678");

        _StatisticService.UpdateCount(10, 200);
        _StatisticService.UpdateRunTime(_AlarmInfoService.TotalAlarmTime == 0 ? 100 : 500, 800);
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

    private void RefreshAlarm()
    {
        // 旧实现走 CommunicationManager.GetData("Alarm")，该能力已随旧通信栈删除。
        // 报警读取暂时停用：不伪造数据，待迁移到 Communication.Core 后恢复。
    }
}
