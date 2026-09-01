using Extension.Core.ControlEx;
using WPFControl.Resources;

public class Script
{
    ResourceMonitorService _ResourceMonitorService;
    BulletinBoardService _BulletinBoardService;
    CommunicationManager _CommunicationManager;
    StatisticService _StatisticService;
    AlarmInfoManagerService _AlarmInfoService;
    MCHelper _MCHelper;
    SecsControl _SecsControl;

    public void Initialize()
    {
        // 初始化逻辑
        _ResourceMonitorService = ContainerManager.Resolve<ResourceMonitorService>();
        _CommunicationManager = ContainerManager.Resolve<CommunicationManager>();
        _BulletinBoardService = ContainerManager.Resolve<BulletinBoardService>();
        _StatisticService = ContainerManager.Resolve<StatisticService>();
        _AlarmInfoService = ContainerManager.Resolve<AlarmInfoManagerService>();
        _MCHelper = ContainerManager.Resolve<MCHelper>();
        _SecsControl = ContainerManager.Resolve<SecsControl>();
    }

    public void Run()
    {
        Console.WriteLine("开始执行脚本");
        _ResourceMonitorService.StartAutoRefresh();
        _CommunicationManager.SetReadTask("Data", "D8000", 1000, Extension.Core.CompareEx.EComparePolarity.None);
        _CommunicationManager.StartLoop();


        DispatcherTimerEx.DispatcherTimer(new Action(Loop), 1000, "Loop");
    }

    private void Loop()
    {
        var now = DateTime.Now;

        if (now.Minute % 2 == 0 && now.Second == 0)//5分钟读一次
        {
            //读取OK NG
            //ReadCount();
            //更新每天统计信息
            _BulletinBoardService.ShowData_Day(new List<WPFControl.CustomControl.Models.Charts.LineSeries>
            {
                new WPFControl.CustomControl.Models.Charts.LineSeries {
                    SeriesName="OK",
                    Value=new System.Collections.ObjectModel.ObservableCollection<double>{ 10},
                    Color= ColorHelper.Color_Green,
                },
                new WPFControl.CustomControl.Models.Charts.LineSeries {
                    SeriesName="NG",
                    Value=new System.Collections.ObjectModel.ObservableCollection<double>{ 20,20,30,40,130,20,0},
                    Color= ColorHelper.Color_Red,
                },
                new WPFControl.CustomControl.Models.Charts.LineSeries {
                    SeriesName="Total",
                    Value=new System.Collections.ObjectModel.ObservableCollection<double>{ 120,120,130,140,230,120,0},
                    Color= ColorHelper.Color_Blue,
                }
            });
        }
        else if (now.Hour == 0)
        {
            /// 更新每月统计信息
            _BulletinBoardService.ShowData_Month(new List<WPFControl.CustomControl.Models.Charts.LineSeries>
            {
                new WPFControl.CustomControl.Models.Charts.LineSeries
                {
                    SeriesName = "OK",
                    Value = new System.Collections.ObjectModel.ObservableCollection<double> { 10, 120 },
                    Color = ColorHelper.Color_Blue,
                },
                new WPFControl.CustomControl.Models.Charts.LineSeries
                {
                    SeriesName = "NG",
                    Value = new System.Collections.ObjectModel.ObservableCollection<double> { 20, 20 },
                    Color = ColorHelper.Color_Red,
                }
            });
        }


        var plcstatus = _MCHelper.IsConnected ? ELinkStatus.Online : ELinkStatus.Offline;
        if (plcstatus == ELinkStatus.Online)
        {
            //await RefreshSystem();
            RefreshAlarm();
        }

        // 更新统计信息（在UI线程上执行）
        _StatisticService.UpdateID("12345678");

        _StatisticService.UpdateCount(10, 200);
        _StatisticService.UpdateRunTime(_AlarmInfoService.TotalAlarmTime == 0 ? 100 : 500, 800);
        _StatisticService.UpdateSecsMode(_SecsControl._StatusManager.CurrentMode);
        _StatisticService.UpdatePLC(plcstatus);
        _StatisticService.UpdateSecsStatus(_SecsControl.IsConnected ? ELinkStatus.Online : ELinkStatus.Offline);
        _StatisticService.UpdateTime();


        // 在后台线程执行耗时操作
        Task.Run(() =>
        {
            // 每小时检查一次（当分钟为0且秒为0时）
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
        var data = _CommunicationManager.GetData("Alarm");
        if (data != null)
            _AlarmInfoService.Organize(data);
    }
}