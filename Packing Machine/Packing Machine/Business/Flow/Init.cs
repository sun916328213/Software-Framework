using Extension.Core.SystemEx;
using Extension.Struct;
using MaterialDesignThemes.Wpf;
using Packing_Machine.Data;
using Packing_Machine.Infra.Core;
using Prism.Regions;
using Prism.Services.Dialogs;
using Secs;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Models;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.Business.Flow
{
    public class Init
    {
        private static ControlService _ControlService;
        private static StatisticService _StatisticService;
        private static WindowMenuService _WindowMenuService;
        private static MenuService _MenuService;

        private static IRegionManager _IRegionManager;
        private static DialogService _DialogService;

        private static SecsControl _SecsControl;

        public Init()
        {
            // 初始化逻辑
            _ControlService = ContainerManager.Resolve<ControlService>();
            _StatisticService = ContainerManager.Resolve<StatisticService>();
            _WindowMenuService = ContainerManager.Resolve<WindowMenuService>();
            _MenuService = ContainerManager.Resolve<MenuService>();

            _IRegionManager = ContainerManager.Resolve<IRegionManager>();
            _DialogService = ContainerManager.Resolve<DialogService>();

            _SecsControl = ContainerManager.Resolve<SecsControl>();
        }

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
        }

        private void Init_Display()
        {
            var _DisplayService = ContainerManager.Resolve<DisplayService>();

            _DisplayService.Clear();
            _DisplayService.SetRowCol(2, 3);

            _DisplayService.AddDisplayTitle("1");
            _DisplayService.AddDisplayTitle("2");
            _DisplayService.AddDisplayTitle("3");
            _DisplayService.AddDisplayTitle("4");
            _DisplayService.AddDisplayTitle("5");
            _DisplayService.AddDisplayTitle("6");
        }

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