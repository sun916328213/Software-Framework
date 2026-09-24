using Device.Scan;
using Extension.UI.Controls;
using Extension.Core.Conversion;
using Extension.Core.Enums;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using DataManager.Log.Definition;
using Packing_Machine.Data;
using Packing_Machine.Business.Order;
using Packing_Machine.Business.Station;
using Packing_Machine.Infra.Tool;
using Prism.Ioc;
using Secs;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;
using WPFControl.Resources;

namespace Packing_Machine.Business.Flow
{
    class AutoProcess
    {
        readonly UserInfoModel _Model_UserInfo;
        readonly StatusBarModel _Model_StatusBar;
        readonly StatisticService _StatisticService;
          LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();

        readonly PLCTool _PLCMethod;
        readonly SecsTool _SecsMethod;
        readonly SecsControl _SecsControl;

        private ScanManager _ScanManager;

        internal readonly Load _Load;
        internal readonly ReadID _ReadID;
        internal readonly Accessory _Accessory;
        internal readonly Label _Label;
        internal readonly HeatSeal _HeatSeal;

        internal readonly UnLoad _UnLoad;
        internal readonly ALDetect _ALDetect;
        internal readonly HumidityCard _HumidityCard;
        internal readonly Recipe _Recipe;
        internal readonly Process _Process;

        readonly ResourceMonitorService _ResourceMonitorService;
        readonly AlarmInfoManagerService _AlarmInfoService;
        readonly BulletinBoardService _BulletinBoardService;

        readonly UserInfoService _UserInfoService;


        private readonly IContainerProvider _container;
     

        public AutoProcess(IContainerProvider container)
        {
            _container = container;
            _Model_UserInfo = container.Resolve<UserInfoModel>();
            _Model_StatusBar = container.Resolve<StatusBarModel>();
            _StatisticService = container.Resolve<StatisticService>();
            _ResourceMonitorService = container.Resolve<ResourceMonitorService>();

            _PLCMethod = container.Resolve<PLCTool>();
            _SecsMethod = container.Resolve<SecsTool>();
            _SecsControl = container.Resolve<SecsControl>();
            _ScanManager = container.Resolve<ScanManager>();

            _Load = container.Resolve<Load>();
            _ReadID = container.Resolve<ReadID>();
            _Accessory = container.Resolve<Accessory>();
            _Label = container.Resolve<Label>();
            _HeatSeal = container.Resolve<HeatSeal>();

            _UnLoad = container.Resolve<UnLoad>();
            _ALDetect = container.Resolve<ALDetect>();
            _HumidityCard = container.Resolve<HumidityCard>();
            _Recipe = container.Resolve<Recipe>();
            _Process = container.Resolve<Process>();
            _AlarmInfoService = container.Resolve<AlarmInfoManagerService>();
            _BulletinBoardService = container.Resolve<BulletinBoardService>();

            _UserInfoService = container.Resolve<UserInfoService>();
        }

        public void Init()
        {
            TryCatchHelper.TryExecute(() =>
            {

                Action action = () => Update();
                action.UIInvokeAsync();

                ConfigScan();
                
 
                ConfigUserChanged();
            });
        }


        /// <summary>
        /// 刷新数据
        /// </summary>
        private void Update()
        {
            _PLCMethod.Refresh();//设置刷新任务
            _ResourceMonitorService.StartAutoRefresh();///刷新系统资源


            Action action = new Action(async () =>
            {
                try
                {
                    var now = DateTime.Now;

                    if (now.Minute % 2 == 0 && now.Second == 0)//5分钟读一次
                    {
                        //读取OK NG
                        ReadCount();
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
                        _BulletinBoardService.ShowData_Month(new List<WPFControl.CustomControl.Models.Charts.LineSeries> {
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


                    // P5：PLC 状态改为真实连接状态；断线后自动反映 Offline。
                    var plcstatus = _PLCMethod.IsOnline ? ELinkStatus.Online : ELinkStatus.Offline;
                    if (plcstatus == ELinkStatus.Online)
                    {
                        await RefreshSystem();
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
                catch (Exception ex)
                {
                    string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                    _LogInfoService.AddRunInfo(message);
                }
            });

            // 创建定时器，间隔1000毫秒，启用防重入机制
            action.DispatcherTimer(1000);
        }


        private List<string> ReadCount()
        {
            List<string> strings = new List<string>();
            //读取计数 PLC 地址从20:00开始
            for (int i = 0; i < 10; i++)
            {
                string Time20 = _PLCMethod.ReadData("读取计数", PLCSystem.Count, 0, false);
                strings.Add(Time20);
            }
            return strings;
        }

        private void ConfigScan()
        {
            _ScanManager[0].DataReceived += (s, e) =>
            {
                _ReadID.curInfo.FOSBID = e.ToASCII(EEndianType.Little_Endian).Trim();
            };
        }

        /// <summary>
        /// 用户信息上报
        /// </summary>
        public void ConfigUserChanged()
        {
            _UserInfoService.UserInfoChanged += async (e, s) =>
            {
                _PLCMethod.TriggerUserInfo(s.CurUserInfo);
                _LogInfoService.AddRunInfo(Message.用户登录.GetMessage(), ELogLevel.Information);
            };
        }


        string CurMode = ESecsMode.Offline.ToString();
        string CurStatus = "";

        /// <summary>
        /// 刷新系统信息
        /// </summary>
        /// <returns></returns>
        private async Task RefreshSystem()
        {
            try
            {
                string StationName = "AuotoProcess";

                //获取PLC状态
                var Status = _PLCMethod.ReadData(StationName, PLCSystem.PLC_DeviceStatus, 0, false);
                //获取PLC模式
                var Mode = _PLCMethod.ReadData(StationName, PLCSystem.PLC_Mode, 0, false);

                //模式改变
                if (Mode != CurMode && Mode == ESecsMode.Offline.ToString())
                {
                    CurMode = Mode;
                    await _SecsControl._ConnectManager.Offline_EtoH().ConfigureAwait(false);
                    _StatisticService.UpdateSecsMode(EnumEx.GetEnum<ESecsMode>(CurMode));
                }
                else if (Mode != CurMode && Mode != ESecsMode.Offline.ToString())
                {
                    CurMode = Mode;
                    await _SecsControl._ConnectManager.Online_EtoH().ConfigureAwait(false);
                    _StatisticService.UpdateSecsMode(EnumEx.GetEnum<ESecsMode>(CurMode));
                }

                //状态改变时
                if (Status != CurStatus)
                {
                    CurStatus = Status;
                    _SecsMethod.Update("DeviceStatus", CurStatus);
                    _StatisticService.UpdateDeviceStatus(EnumEx.GetEnum<EDeviceStatus>(CurStatus));
                }

            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(message, ELogLevel.SysError);
            }
        }

        /// <summary>
        /// 刷新报警信息
        /// </summary>
        public void RefreshAlarm()
        {
            // 旧实现走 CommunicationManager.GetData("Alarm")，该能力已随旧通信栈删除。
            // 报警读取暂时停用：不伪造数据，待迁移到 Communication.Core 的 DataPoint 后恢复。
        }
 

    }
}