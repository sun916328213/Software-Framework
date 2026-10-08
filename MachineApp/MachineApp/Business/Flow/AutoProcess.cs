// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：AutoProcess。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、MachineApp.Infra.Device、Extension.UI.Controls、Extension.Core.Conversion、Extension.Core.Enums、Extension.IO.Files。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using MachineApp.Infra.Device;
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
using DataManager.Core.Log.Definition;
using MachineApp.Data;
using MachineApp.Business.Order;
using MachineApp.Business.Station;
using MachineApp.Infra.Tool;

using SecsGem.Core;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using WPFControls.Controls.Resources;

namespace MachineApp.Business.Flow
{
    /// <summary>
    /// AutoProcess 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    class AutoProcess
    {
        private readonly TryCatchHelper _errors;

        readonly UserInfoModel _Model_UserInfo;
        readonly StatusBarModel _Model_StatusBar;
        readonly StatisticService _StatisticService;
          IProductionLog _LogInfoService => _logInfoService();

        readonly IProductionPlc _PLCMethod;
        readonly IProductionSecs _SecsMethod;
        readonly ISecsGemRuntime _secsRuntime;

        private IProductionDevices _devices;

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


        private readonly Func<IProductionLog> _logInfoService;
     

        /// <summary>
        /// 初始化 AutoProcess 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 getUserInfoModel、getStatusBarModel、getStatisticService、getResourceMonitorService、getPLCTool；具体条件和先后次序见方法体。
        /// </summary>
        public AutoProcess(Func<IProductionLog> getLogInfoService,
            Func<UserInfoModel> getUserInfoModel,
            Func<StatusBarModel> getStatusBarModel,
            Func<StatisticService> getStatisticService,
            Func<ResourceMonitorService> getResourceMonitorService,
            Func<IProductionPlc> getPLCTool,
            Func<IProductionSecs> getSecsTool,
            Func<IProductionDevices> getProductionDeviceSession,
            Func<Load> getLoad,
            Func<ReadID> getReadID,
            Func<Accessory> getAccessory,
            Func<Label> getLabel,
            Func<HeatSeal> getHeatSeal,
            Func<UnLoad> getUnLoad,
            Func<ALDetect> getALDetect,
            Func<HumidityCard> getHumidityCard,
            Func<Recipe> getRecipe,
            Func<Process> getProcess,
            Func<AlarmInfoManagerService> getAlarmInfoManagerService,
            Func<BulletinBoardService> getBulletinBoardService,
            Func<UserInfoService> getUserInfoService,
            ISecsGemRuntime secsRuntime,
            TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            _logInfoService = getLogInfoService;
            _secsRuntime = secsRuntime;
            _Model_UserInfo = getUserInfoModel();
            _Model_StatusBar = getStatusBarModel();
            _StatisticService = getStatisticService();
            _ResourceMonitorService = getResourceMonitorService();

            _PLCMethod = getPLCTool();
            _SecsMethod = getSecsTool();
            _devices = getProductionDeviceSession();

            _Load = getLoad();
            _ReadID = getReadID();
            _Accessory = getAccessory();
            _Label = getLabel();
            _HeatSeal = getHeatSeal();

            _UnLoad = getUnLoad();
            _ALDetect = getALDetect();
            _HumidityCard = getHumidityCard();
            _Recipe = getRecipe();
            _Process = getProcess();
            _AlarmInfoService = getAlarmInfoManagerService();
            _BulletinBoardService = getBulletinBoardService();

            _UserInfoService = getUserInfoService();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Init。
        /// 正文中的关键协作调用包括 TryExecute、Update、UIInvokeAsync、ConfigScan、ConfigUserChanged；具体条件和先后次序见方法体。
        /// </summary>
        public void Init()
        {
            _errors.TryExecute(() =>
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
            if (_stopped || _timer != null) return;
            _PLCMethod.Refresh();//设置刷新任务
            _ResourceMonitorService.StartAutoRefresh();///刷新系统资源


            Action action = async () => await TickAsync(DateTime.Now);

            // 创建定时器，间隔1000毫秒，启用防重入机制
            _timer = action.DispatcherTimer(1000);
        }


        private bool _isUpdating;
        private bool _stopped;
        private System.Windows.Threading.DispatcherTimer? _timer;
        private Task _updateTask = Task.CompletedTask;

        private Task TickAsync(DateTime now)
        {
            if (_stopped || _isUpdating) return Task.CompletedTask;
            return _updateTask = UpdateAsync(now);
        }

        /// <summary>在窗口的 UI 线程停止新刷新，等待已开始的刷新结束后再释放设备。</summary>
        public async Task StopAsync()
        {
            _stopped = true;
            _timer?.Stop();
            _ResourceMonitorService.StopAutoRefresh();
            // This task belongs to this timer and starts on the same UI dispatcher.
            // Shutdown drains it asynchronously; no thread synchronously waits here.
#pragma warning disable VSTHRD003
            await _updateTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }

        private async Task UpdateAsync(DateTime now)
        {
            if (_stopped || _isUpdating) return;
            _isUpdating = true;
            try
            {

                if (now.Minute % 2 == 0 && now.Second == 0)//5分钟读一次
                {
                    //读取OK NG
                    ReadCount();
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
                    _BulletinBoardService.ShowData_Month(new List<WPFControls.Controls.Models.Charts.LineSeries> {
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
                _StatisticService.UpdateSecsMode(_SecsMethod.CurrentMode);
                _StatisticService.UpdatePLC(plcstatus);
                _StatisticService.UpdateSecsStatus(_secsRuntime.GetState().IsConnected ? ELinkStatus.Online : ELinkStatus.Offline);
                _StatisticService.UpdateTime();


                // 清理方法内部已使用后台任务；等待它完成，异常进入下面的日志分支。
                if (now.Minute == 0 && now.Second == 0)
                {
                    await ScheduledDeletion.DeleteExpiredFiles(GlobalData.Path_Image, GlobalData.SaveImageDay);
                }
                if (now.Second == 0)
                    await Task.Run(GC.Collect);
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(message);
            }
            finally { _isUpdating = false; }
        }

        /// <summary>
        /// 读取当前实现提供的数据；对应入口为 ReadCount。
        /// 正文中的关键协作调用包括 ReadData、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ConfigScan。
        /// 正文中的关键协作调用包括 Trim；具体条件和先后次序见方法体。
        /// </summary>
        private void ConfigScan()
        {
            _devices.Scanners[0].ScanReceived += (s, e) =>
            {
                _ReadID.curInfo.FOSBID = e.Value.Trim();
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
                    if (!await _secsRuntime.GoOfflineAsync().ConfigureAwait(false))
                        throw new InvalidOperationException("SECS/GEM offline request failed");
                    _StatisticService.UpdateSecsMode(EnumEx.GetEnum<ESecsMode>(CurMode));
                }
                else if (Mode != CurMode && Mode != ESecsMode.Offline.ToString())
                {
                    CurMode = Mode;
                    if (!await _secsRuntime.GoOnlineRemoteAsync().ConfigureAwait(false))
                        throw new InvalidOperationException("SECS/GEM online request failed");
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
