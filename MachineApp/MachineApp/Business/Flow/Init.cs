// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Init。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Controls、Extension.Core.Models、Extension.Imaging.Models、Extension.Automation.Device、MaterialDesignThemes.Wpf、MachineApp.Data。导入不代表所有类型都会在运行时加载。
using Extension.UI.Controls;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using MaterialDesignThemes.Wpf;
using MachineApp.Data;

using Prism.Regions;
using Prism.Services.Dialogs;
using SecsGem.Core;
using System.Collections.ObjectModel;
using WPFControls.Controls.Models;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace MachineApp.Business.Flow
{
    /// <summary>
    /// Init 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public class Init
    {
        private readonly ControlService _ControlService;
        private readonly StatisticService _StatisticService;
        private readonly WindowMenuService _WindowMenuService;
        private readonly MenuService _MenuService;

        private readonly IRegionManager _IRegionManager;
        private readonly DialogService _DialogService;

        private readonly ISecsGemRuntime _secsRuntime;

        private readonly Func<DisplayService> _displayService;

        /// <summary>
        /// 初始化 Init 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 getControlService、getStatisticService、getWindowMenuService、getMenuService、getIRegionManager；具体条件和先后次序见方法体。
        /// </summary>
        public Init(ISecsGemRuntime secsRuntime,
            Func<ControlService> getControlService,
            Func<StatisticService> getStatisticService,
            Func<WindowMenuService> getWindowMenuService,
            Func<MenuService> getMenuService,
            Func<IRegionManager> getIRegionManager,
            Func<DialogService> getDialogService,
            Func<DisplayService> getDisplayService)
        {
            _secsRuntime = secsRuntime;
            _displayService = getDisplayService;
            // 初始化逻辑
            _ControlService = getControlService();
            _StatisticService = getStatisticService();
            _WindowMenuService = getWindowMenuService();
            _MenuService = getMenuService();

            _IRegionManager = getIRegionManager();
            _DialogService = getDialogService();

        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 InitControl。
        /// 正文中的关键协作调用包括 Init_Menu、Init_Control、Init_Display；具体条件和先后次序见方法体。
        /// </summary>
        public void InitControl()
        {
            Init_Menu();
            Init_Control();
            Init_Display();
        }

        #region 控件
        private void Init_Control()
        {
            //控制按钮功能
            _ControlService.AddAction(new List<Action>
            {
                 async ()  =>
                 {
             
                     //_StatisticService.UpdateDeviceStatus(EDeviceStatus.Ready);
                     //_ = _secsRuntime.GoOnlineRemoteAsync();
                 },
                 () =>
                     {
                         _StatisticService.UpdateDeviceStatus(EDeviceStatus.Running);
                         _ = _secsRuntime.GoOfflineAsync();
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
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Init_Display。
        /// 正文中的关键协作调用包括 _displayService、Clear、SetRowCol、AddDisplayTitle；具体条件和先后次序见方法体。
        /// </summary>
        private void Init_Display()
        {
            var _DisplayService = _displayService();

            _DisplayService.Clear();
            _DisplayService.SetRowCol(2, 3);

            _DisplayService.AddDisplayTitle("1");
            _DisplayService.AddDisplayTitle("2");
            _DisplayService.AddDisplayTitle("3");
            _DisplayService.AddDisplayTitle("4");
            _DisplayService.AddDisplayTitle("5");
            _DisplayService.AddDisplayTitle("6");
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Init_Menu。
        /// 正文中的关键协作调用包括 Clear、AddRange、GetMessage、RequestNavigate、ShowDialog；具体条件和先后次序见方法体。
        /// </summary>
        private void Init_Menu()
        {
            _MenuService.Clear();
            _MenuService.AddRange(new List<CNavigationItem>
             {
                 new CNavigationItem
                 {
                     Title = Message.主页.GetMessage(),
                     SelectedIcon = PackIconKind.Home,
                     Icon = PackIconKind.HomeOutline,
                     Action = (() => {
                         _IRegionManager.RequestNavigate("MainContentRegion", "Home");
                     })
                 },
                 new CNavigationItem
                 {
                     Title = "Test",
                     Icon = PackIconKind.TestTube,
                     Action = (() => {
                         _IRegionManager.RequestNavigate("MainContentRegion", "Test");
                     })
                 },
                
                 new CNavigationItem
                 {
                     Title = Message.配方.GetMessage(),
                     SelectedIcon = PackIconKind.ReceiptTextEdit,
                     Icon = PackIconKind.ReceiptTextEditOutline,
                     //Userlevel= EUserLevel.Admin,
                     Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "Recipe"); })
                 },
                 new CNavigationItem
                 {
                     Title = Message.打印设置.GetMessage(),
                     SelectedIcon = PackIconKind.Printer,
                     Icon = PackIconKind.PrinterOutline,
                     Action = (() =>
                     {
                         _IRegionManager.RequestNavigate("MainContentRegion", "PrintManagerView");
                     })
                 },
                 new CNavigationItem
                 {
                     Title = Message.视觉查看.GetMessage(),
                     SelectedIcon = PackIconKind.Video,
                     Icon = PackIconKind.VideoOutline,
                     Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "DisplayView"); }),
                 },

                 new CNavigationItem
                 {
                     Title ="文件配置",
                     SelectedIcon = PackIconKind.PencilRuler,
                     Icon = PackIconKind.PencilRulerOutline,

                     Children=new ObservableCollection<CNavigationItem>
                     {
                         new CNavigationItem
                         {
                             Title = "Log配置",
                             SelectedIcon = PackIconKind.PencilBoxMultiple,
                              Icon = PackIconKind.PencilBoxMultipleOutline,
                             Action = (() =>
                             {
                                 _IRegionManager.RequestNavigate("MainContentRegion", "LogConfigView");
                             })
                         }
                     }
                 },

                 new CNavigationItem
                 {
                     Title = Message.流程.GetMessage(),
                     SelectedIcon = PackIconKind.ListBox,
                     Icon = PackIconKind.ListBoxOutline,
                     Children = new ObservableCollection<CNavigationItem>
                     {
                         new CNavigationItem
                         {
                             Title = "加载流程" ,
                             Icon = PackIconKind.DownloadBoxOutline,
                            // Userlevel= EUserLevel.SuperAdmin,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "StartupFlowView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.初始化流程.GetMessage(),
                             SelectedIcon = PackIconKind.Refresh,
                             Icon = PackIconKind.Refresh,
                            //  Userlevel= EUserLevel.SuperAdmin,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "InitializeFlowView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.自动流程.GetMessage(),

                             Icon = PackIconKind.RepeatVariant,
                            //  Userlevel= EUserLevel.SuperAdmin,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "AutoFlowView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.关闭流程.GetMessage(),

                             Icon = PackIconKind.CloseThick,
                            //  Userlevel= EUserLevel.SuperAdmin,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "ShutdownFlowView"); })
                         },
                     }
                 },

                 new CNavigationItem
                 {
                     Title =Message.检测项设置.GetMessage(),
                     SelectedIcon = PackIconKind.PencilRuler,
                     Icon = PackIconKind.PencilRulerOutline,

                     Children=new ObservableCollection<CNavigationItem>
                     {
                         new CNavigationItem
                         {
                             Title = Message.视觉设置.GetMessage(),
                             SelectedIcon = PackIconKind.PencilBoxMultiple,
                              Icon = PackIconKind.PencilBoxMultipleOutline,
                             Action = (() =>
                             {
                                 _IRegionManager.RequestNavigate("MainContentRegion", "VisionView");
                             })
                         }
                     }
                 },

                 new CNavigationItem
                 {
                     Title = Message.硬件设置.GetMessage(),
                     SelectedIcon = PackIconKind.Cog ,
                     Icon = PackIconKind.CogOutline,
                     Children=new ObservableCollection<CNavigationItem>
                     {
                         new CNavigationItem
                         {
                             Title = Message.Cam设置.GetMessage(),
                             SelectedIcon = PackIconKind.Camera,
                             Icon = PackIconKind.CameraOutline,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "CamManagerView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.RFID设置.GetMessage(),
                             SelectedIcon = PackIconKind.CastConnected,
                             Icon = PackIconKind.Cast,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "RFIDManagerView"); })
                         },
                         new CNavigationItem
                         {
                             Title = "扫码枪设置",
                             Icon = PackIconKind.Barcode,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "ScanManagerView"); })
                         },
                         new CNavigationItem
                         {
                             Title = "光源控制器设置",
                             Icon = PackIconKind.CarLightDimmed,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "LightControlManagerView"); })
                         },

                     }
                 },

                 new CNavigationItem
                 {
                     Title = "查询",
                     SelectedIcon = PackIconKind.DatabaseSearch,
                     Icon = PackIconKind.DatabaseSearchOutline,
                     Children=new ObservableCollection<CNavigationItem>
                     {
                         new CNavigationItem
                         {
                             Title = Message.Log查询.GetMessage(),
                             SelectedIcon = PackIconKind.NoteSearch,
                             Icon = PackIconKind.NoteSearchOutline,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "LogQueryView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.SEMILog查询.GetMessage(),
                             SelectedIcon = PackIconKind.CloudSearch,
                             Icon = PackIconKind.CloudSearchOutline,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "SEMILogQueryView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.数据查询.GetMessage(),
                             SelectedIcon = PackIconKind.TextBoxSearch,
                             Icon = PackIconKind.TextBoxSearchOutline,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "DataQueryView"); })
                         },
                         new CNavigationItem
                         {
                             Title = "产能查询",

                             Icon = PackIconKind.EqualizerOutline,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "CapacityStatisticsView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.图像查询.GetMessage(),
                             SelectedIcon = PackIconKind.ImageSearch,
                             Icon = PackIconKind.ImageSearchOutline,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "ImageQueryView"); })
                         },
                         new CNavigationItem
                         {
                             Title = Message.报警查询.GetMessage(),
                             SelectedIcon = PackIconKind.AlarmLight,
                             Icon = PackIconKind.AlarmLightOutline,
                             Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "AlarmInfoQueryView"); })
                         },
                     }
                 },

                 new CNavigationItem
                 {
                     Title = Message.用户登录.GetMessage(),
                     SelectedIcon = PackIconKind.User,
                     Icon = PackIconKind.UserOutline,
                     Action = () => { _DialogService.ShowDialog("UserInfoView"); },
                     ItemType = NavigationItemType.UserLogin // 设置菜单项类型为用户登录
                 },
                 new CNavigationItem
                 {
                     Title = Message.用户信息管理.GetMessage(),
                     SelectedIcon = PackIconKind.AccountKey,
                     Icon = PackIconKind.AccountKeyOutline,
                     Action = () => { _IRegionManager.RequestNavigate("MainContentRegion","UserInfoManagerView"); },
                     Userlevel= EUserLevel.Admin,
                 },
                 new CNavigationItem
                 {
                     Title = Message.SEMI设置.GetMessage(),
                     SelectedIcon = PackIconKind.ConsoleNetwork,
                     Icon = PackIconKind.ConsoleNetworkOutline,
                     Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "SEMIManagerView"); })
                 },
                 new CNavigationItem
                 {
                     Title = Message.报警设置.GetMessage(),
                      Userlevel= EUserLevel.SuperAdmin,
                     SelectedIcon = PackIconKind.BellCog,
                     Icon = PackIconKind.BellCogOutline,
                     Action = (() => { _IRegionManager.RequestNavigate("MainContentRegion", "AlarmInfoManagerView"); })
                 },
             });
        }
        #endregion
    }
}
