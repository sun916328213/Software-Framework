using Communication;
using Communication.MC;
using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.SQL;
using DataManager.SQL.Definition;
using Device.Camera;
using Device.Light;
using Device.RFID;
using Device.Scan;
using Extension.Core.ControlEx;
using Extension.Core.ConvertEx;
using Extension.Core.FileEx;
using Extension.Core.SystemEx;
using Extension.Struct;
using MaterialDesignThemes.Wpf;
using Packing_Machine.Data;
using Packing_Machine.Data.Model;
using Packing_Machine.Business.Order;
using Packing_Machine.Business.Station;
using Packing_Machine.Infra.Tool;
using Packing_Machine.UI.ViewModel;
using Prism.Ioc;
using Prism.Regions;
using Prism.Services.Dialogs;
using Secs;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Views.Display;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.Business.Flow
{
    public class Init
    {
        private AlarmInfoManagerService _AlarmInfoService;

        private AutoProcess _AutoProcess;

        private CamManager _CamManager;

        private CamManagerService _CamManagerService;

        private CommunicationManager _CommunicationManager;

        // 通过构造函数注入依赖
        private IContainerProvider _container;

        private DialogService _dialogService;
        private DisplayService _DisplayService;
        private ImageQueryService _ImageQueryService;
        private LightControlManager _LightControlManager;
        private LogInfoService _LogInfoService=> _container.Resolve<LogInfoService>();
        private LogManager _LogManager;
        private MCHelper _MCHelper;
        private MenuService _MenuService;
        private ControlService _Model_Control;
        private PLCTool _PLCMethod;
        private IRegionManager _regionManager;
        private RFIDManager _RFIDManager;
        private RFIDManagerService _RFIDManagerService;
        private ScanManager _ScanManager;
        private SecsControl _SecsControl;
        private SEMILogService _SEMILogService;
        private SQLManager _SQLManager;
        private StatisticService _StatisticService;
        private VisionViewModel _VisionViewModel;
        private WindowMenuService _WindowMenuService;

        // 构造函数注入依赖
        public Init(IContainerProvider container, IRegionManager regionManager, DialogService dialogService)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            _Model_Control = _container.Resolve<ControlService>();
            _StatisticService = _container.Resolve<StatisticService>();
            _AlarmInfoService = _container.Resolve<AlarmInfoManagerService>();

            _MenuService = _container.Resolve<MenuService>();
            _DisplayService = _container.Resolve<DisplayService>();
            _ImageQueryService = _container.Resolve<ImageQueryService>();

            _CamManagerService = _container.Resolve<CamManagerService>();
            _LightControlManager = _container.Resolve<LightControlManager>();
            _RFIDManagerService = _container.Resolve<RFIDManagerService>();
            _ScanManager = _container.Resolve<ScanManager>();
            _LogManager = _container.Resolve<LogManager>();

            _MCHelper = _container.Resolve<MCHelper>();
            _CommunicationManager = _container.Resolve<CommunicationManager>();
            _PLCMethod = _container.Resolve<PLCTool>();
            _CamManager = _container.Resolve<CamManager>();
            _SecsControl = _container.Resolve<SecsControl>();
            _RFIDManager = _container.Resolve<RFIDManager>();
            _SQLManager = _container.Resolve<SQLManager>();

           
            _SEMILogService = _container.Resolve<SEMILogService>();
            _AutoProcess = _container.Resolve<AutoProcess>();

            _VisionViewModel = _container.Resolve<VisionViewModel>();
            _WindowMenuService = _container.Resolve<WindowMenuService>();
            GlobalData.Load();
        }
        #region 初始化工具

        public void InitTool()
        {
            Init_Tool();
        }
 
        /// <summary>
        /// 初始化工具
        /// </summary>
        private void Init_Tool()
        {
            TryCatchHelper.TryExecute(() =>
            {
                GlobalData.Load();

                //日志
                _LogManager.Create(new LogParam()
                {
                    FilePath = GlobalData.LogPath,
                    Name = "Log",
                    Separator = "||",
                    SaveDays = 180,
                    LogType = ELogType.SeriLog
                });
                //SEMI日志
                _LogManager.Create(new LogParam()
                {
                    FilePath = GlobalData.SecsLogPath,
                    Name = "SEMILog",
                    Separator = "||",
                    SaveDays = 180,
                    LogType = ELogType.SeriLog
                });

                _SQLManager.Create<CSQLData>(new SQLParam()
                {
                    ServerIP = "localhost",
                    Port = "5432",
                    UserName = "postgres",
                    Password = "123",
                    DBName = "test1",
                    DBType = ESQLType.PostgreSQL,
                    ToolType = ESQLToolType.EFCore, 
                });
                //连接数据库
                _SQLManager[0].Connect(); 
            },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.Tool_Error)
            );
        }

        #endregion 初始化工具

        #region 初始化通讯

        public void InitCommunity()
        {
            Init_PLC();
            InitSecs();
        }

        /// <summary>
        /// 初始化PLC
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void Init_PLC()
        {
            TryCatchHelper.TryExecute(() =>
            {
                var d = CSVEx.ReadCsv<CRegisterInfo>(GlobalData.PLCPath);

                List<CRegisterInfo> registerInfos = new List<CRegisterInfo>();
                foreach (var item in d)
                {
                    if (item.Name?.ToLower().Contains("_trigger") == true)
                    {
                        var add = Convert.ToInt32(item.Address.ReplaceLetter());
                        var let = item.Address.ReplaceNum();
                        registerInfos.Add(item);
                        registerInfos.Add(new CRegisterInfo() { Address = let + Convert.ToInt32(add + PLCAddressOffest.Reply), Type = EDataType.Short });
                        registerInfos.Add(new CRegisterInfo() { Address = let + Convert.ToInt32(add + PLCAddressOffest.Result), Type = EDataType.Short });
                        registerInfos.Add(new CRegisterInfo() { Address = let + Convert.ToInt32(add + PLCAddressOffest.Function), Type = EDataType.Short });
                        registerInfos.Add(new CRegisterInfo() { Address = let + Convert.ToInt32(add + PLCAddressOffest.Count), Type = EDataType.Short });
                        registerInfos.Add(new CRegisterInfo() { Address = let + Convert.ToInt32(add + PLCAddressOffest.CountReply), Type = EDataType.Short });
                        registerInfos.Add(new CRegisterInfo() { Address = let + Convert.ToInt32(add + PLCAddressOffest.ID), Type = EDataType.String, Length = 20 });
                    }
                    else
                    {
                        registerInfos.Add(item);
                    }
                }
                var d1 = _MCHelper.Connect(4);
                if (!d1) throw new Exception("PLC connect fail");
                _CommunicationManager.LoadConfig(registerInfos);
                _PLCMethod.CompleteAll(PLCSystem.PLC_SysTrigger, PLCSystem.PLC_SysTrigger, 1000);
            });
        }

        /// <summary>
        /// 初始化Secs
        /// </summary>
        private void InitSecs()
        {
            TryCatchHelper.TryExecute(() =>
            {
                _SecsControl.Config();
                var d = _SecsControl.Connect(false,"127.0.0.1",5000);

                var alarm = _AlarmInfoService.GetAlarms().Select(x => new Secs.Variable.AlarmInfo { ID = x.ID.ToString(), Description = x.Info_EN, Enable = true }).ToList();
                _SecsControl._VIDManager.AddAlarm(alarm);
                _SecsControl._StatusManager.Config(VIDOrder.DeviceStatus, VIDOrder.DeviceMode, EventOrder.Device_Status, EventOrder.Device_Mode);
                _SecsControl._StatusManager.StartMonitoringAsync().ConfigureAwait(false);
                _SecsControl._ConnectManager.WaitOnlineAndOfflineAsync().ConfigureAwait(false);
            },
            (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.Community_Error));
        }

        #endregion

        #region 初始化控件

        public   void InitControl()
        {
            Action action = () =>
            {
                Init_Control();
                Init_Display();
                Init_Menu();
            };
            action.UIInvoke();
        }

        private void Init_Menu()
        {
            TryCatchHelper.TryExecute(() =>
            {
                // 清空现有菜单并添加Packing Machine专用菜单
                _MenuService.Clear();
                _MenuService.AddRange(new List<CNavigationItem>
                    {
                        new CNavigationItem
                        {
                            Title = Message.主页.GetMessage(),
                            SelectedIcon = PackIconKind.Home,
                            UnselectedIcon = PackIconKind.HomeOutline,
                            Action = (() => {
                                _regionManager.RequestNavigate("MainContentRegion", "Home");
                            })
                        },

                        new CNavigationItem
                        {
                            Title = Message.配方.GetMessage(),
                            SelectedIcon = PackIconKind.ReceiptTextEdit,
                            UnselectedIcon = PackIconKind.ReceiptTextEditOutline,
                            //Userlevel= EUserLevel.Admin,
                            Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "Recipe"); })
                        },
                        new CNavigationItem
                        {
                            Title = Message.打印设置.GetMessage(),
                            SelectedIcon = PackIconKind.Printer,
                            UnselectedIcon = PackIconKind.PrinterOutline,
                            Action = (() =>
                            {
                                _regionManager.RequestNavigate("MainContentRegion", "PrintManagerView");
                            })
                        },
                        new CNavigationItem
                        {
                            Title = Message.视觉查看.GetMessage(),
                            SelectedIcon = PackIconKind.Video,
                            UnselectedIcon = PackIconKind.VideoOutline,
                            Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "DisplayView"); }),
                        },
                        new CNavigationItem
                        {
                            Title = Message.Cam设置.GetMessage(),
                            SelectedIcon = PackIconKind.Camera,
                            UnselectedIcon = PackIconKind.CameraOutline,
                            Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "CamManagerView"); })
                        },
                        new CNavigationItem
                        {
                            Title = Message.流程.GetMessage(),
                            SelectedIcon = PackIconKind.ListBox,
                            UnselectedIcon = PackIconKind.ListBoxOutline,
                            Children = new ObservableCollection<CNavigationItem>
                            {
                                new CNavigationItem
                                {
                                    Title = Message.启动流程.GetMessage(),
                                    SelectedIcon = PackIconKind.Play,
                                    UnselectedIcon = PackIconKind.PlayOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "StartupFlowView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.初始化流程.GetMessage(),
                                    SelectedIcon = PackIconKind.Refresh,
                                    UnselectedIcon = PackIconKind.Refresh,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "InitializeFlowView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.自动流程.GetMessage(),
                                    SelectedIcon = PackIconKind.Play,
                                    UnselectedIcon = PackIconKind.PlayOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "AutoFlowView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.关闭流程.GetMessage(),
                                    SelectedIcon = PackIconKind.PowerOff,
                                    UnselectedIcon = PackIconKind.PowerOff,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "ShutdownFlowView"); })
                                },
                            }
                        },
                        new CNavigationItem
                        {
                            Title =Message.检测项设置.GetMessage(),
                            SelectedIcon = PackIconKind.PencilRuler,
                            UnselectedIcon = PackIconKind.PencilRulerOutline,

                            Children=new ObservableCollection<CNavigationItem>
                            {
                                new CNavigationItem
                                {
                                    Title =Message.检测项设置.GetMessage(),
                                    SelectedIcon = PackIconKind.CameraWireless,
                                    UnselectedIcon = PackIconKind.CameraWirelessOutline,
                                    Action = (() =>
                                    {
                                        _regionManager.RequestNavigate("MainContentRegion", "VisionSettingView");
                                    }),
                                },
                                new CNavigationItem
                                {
                                    Title = Message.视觉设置.GetMessage(),
                                    SelectedIcon = PackIconKind.PencilBoxMultiple,
                                    UnselectedIcon = PackIconKind.PencilBoxMultipleOutline,
                                    Action = (() =>
                                    {
                                        _regionManager.RequestNavigate("MainContentRegion", "VisionView");
                                    })
                                }
                            }
                        },
                        new CNavigationItem
                        {
                            Title = Message.硬件设置.GetMessage(),
                            SelectedIcon = PackIconKind.Cog ,
                            UnselectedIcon = PackIconKind.CogOutline,
                            Children=new ObservableCollection<CNavigationItem>
                            {
                                new CNavigationItem
                                {
                                    Title = Message.Cam设置.GetMessage(),
                                    SelectedIcon = PackIconKind.Camera,
                                    UnselectedIcon = PackIconKind.CameraOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "CamManagerView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.RFID设置.GetMessage(),
                                    SelectedIcon = PackIconKind.CastConnected,
                                    UnselectedIcon = PackIconKind.Cast,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "RFIDManagerView"); })
                                },
                                 new CNavigationItem
                                {
                                    Title = "脚本编辑器",
                                    SelectedIcon = PackIconKind.CodeBraces,
                                    UnselectedIcon = PackIconKind.CodeBracesBox,
                                    Userlevel= EUserLevel.Admin,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "ScriptEditorView"); })
                                },
                            }
                        },

                          new CNavigationItem
                        {
                            Title = Message.数据查询.GetMessage(),
                            SelectedIcon = PackIconKind.DatabaseSearch,
                            UnselectedIcon = PackIconKind.DatabaseSearchOutline,
                            Children=new ObservableCollection<CNavigationItem>
                            {
                                new CNavigationItem
                                {
                                    Title = Message.Log查询.GetMessage(),
                                    SelectedIcon = PackIconKind.NoteSearch,
                                    UnselectedIcon = PackIconKind.NoteSearchOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "LogQueryView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.SEMILog查询.GetMessage(),
                                    SelectedIcon = PackIconKind.CloudSearch,
                                    UnselectedIcon = PackIconKind.CloudSearchOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "SEMILogQueryView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.数据查询.GetMessage(),
                                    SelectedIcon = PackIconKind.TextBoxSearch,
                                    UnselectedIcon = PackIconKind.TextBoxSearchOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "DataQueryView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.图像查询.GetMessage(),
                                    SelectedIcon = PackIconKind.ImageSearch,
                                    UnselectedIcon = PackIconKind.ImageSearchOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "ImageQueryView"); })
                                },
                                new CNavigationItem
                                {
                                    Title = Message.报警查询.GetMessage(),
                                    SelectedIcon = PackIconKind.AlarmLight,
                                    UnselectedIcon = PackIconKind.AlarmLightOutline,
                                    Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "AlarmInfoQueryView"); })
                                },
                            }
                        },

                        new CNavigationItem
                        {
                            Title = Message.用户登录.GetMessage(),
                            SelectedIcon = PackIconKind.User,
                            UnselectedIcon = PackIconKind.UserOutline,
                            Action = () => { _dialogService.ShowDialog("UserInfoView"); },
                            ItemType = NavigationItemType.UserLogin // 设置菜单项类型为用户登录
                        },
                        new CNavigationItem
                        {
                            Title = Message.用户信息管理.GetMessage(),
                            SelectedIcon = PackIconKind.AccountKey,
                            UnselectedIcon = PackIconKind.AccountKeyOutline,
                            Action = () => { _regionManager.RequestNavigate("MainContentRegion","UserInfoManagerView"); },
                            Userlevel= EUserLevel.Admin,
                        },
                        new CNavigationItem
                        {
                            Title = Message.SEMI设置.GetMessage(),
                            SelectedIcon = PackIconKind.ConsoleNetwork,
                            UnselectedIcon = PackIconKind.ConsoleNetworkOutline,
                            Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "SEMIManagerView"); })
                        },
                        new CNavigationItem
                        {
                            Title = Message.报警设置.GetMessage(),
                             Userlevel= EUserLevel.SuperAdmin,
                            SelectedIcon = PackIconKind.BellCog,
                            UnselectedIcon = PackIconKind.BellCogOutline,
                            Action = (() => { _regionManager.RequestNavigate("MainContentRegion", "AlarmInfoView"); })
                        },
                    });
            },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.Control_Error));
        }

        /// <summary>
        /// 初始化控件
        /// </summary>
        private void Init_Control()
        {
            TryCatchHelper.TryExecute(() =>
                {
                    //控制按钮功能
                    _Model_Control.AddAction(new List<Action>
                    {
                        async ()  =>
                        {
                        
                            //_StatisticService.UpdateDeviceStatus(EDeviceStatus.Ready);
                            //_ = _SecsControl._ConnectManager.Online_EtoH();
                        },
                        () =>
                        {
                            _StatisticService.UpdateDeviceStatus(EDeviceStatus.Running);
                            _ = _SecsControl._ConnectManager.Offline_EtoH();
                        },
                        () =>
                        {
                            _StatisticService.UpdateDeviceStatus(EDeviceStatus.Idle); 
                        },

                        () => 
                        { 
                            _WindowMenuService.CloseApplication(); 
                        }
                    });
                },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.Control_Error)
            );
        }

        private void Init_Display()
        {
            TryCatchHelper.TryExecute(() =>
            {
                _DisplayService.SetRowCol(2, 3);

                _DisplayService.AddDisplayTitle(   Message.存档站.GetMessage()  );
                _DisplayService.AddDisplayTitle(Message.热封站.GetMessage());
                _DisplayService.AddDisplayTitle(Message.复检站.GetMessage());
                _DisplayService.AddDisplayTitle(Message.AT_Left站.GetMessage());
                _DisplayService.AddDisplayTitle(Message.AT_Right站.GetMessage());
                _DisplayService.AddDisplayTitle(Message.标签站.GetMessage());
            },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.Control_Error));
        }

     

        

        #endregion  

        #region 初始化硬件

        public void InitHardWare()
        {
            TryCatchHelper.TryExecute(() =>
                {
                    _ImageQueryService.Model.ImageSavePath = GlobalData.Path_Image;
                    _CamManager.Config(_CamManagerService.Model.DataCollection.ToList());
                    _CamManager.Connect();
                },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.Cam_Error));

            TryCatchHelper.TryExecute(() =>
                {
                    _LightControlManager.Create(GlobalData.LightParam);
                    _LightControlManager.Connect();
                    for (int i = 1; i <= 8; i++)
                        _LightControlManager[0].Setlight(0, i);
                },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.LightControl_Error));

            TryCatchHelper.TryExecute(() =>
                {
                    _ScanManager.Create(GlobalData.ScanPara);
                    _ScanManager.Connect();
                },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.Scan_Error));

            TryCatchHelper.TryExecute(() =>
                {
                    _RFIDManager.Config(_RFIDManagerService.Model.DataCollection.ToList());
                    _RFIDManager.Connect();
                },
                (ex) => _PLCMethod.TriggerAlarm(PLCSystem.PC_Alarm, PCAlarm.RFID_Error));
        }





        #endregion 初始化硬件
    }
}