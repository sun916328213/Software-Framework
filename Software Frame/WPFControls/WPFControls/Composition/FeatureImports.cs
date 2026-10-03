// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Services.Log.LogQueryService、WPFControls.ProgramControl.Services.Log.SEMILogQueryService、WPFControls.ProgramControl.Services.Log.SEMIManagerService、WPFControls.ProgramControl.Services.Printer.PrintService、WPFControls.ProgramControl.Services.User.UserManagerService、WPFControls.ProgramControl.ViewModels.Alarm.AlarmInfoManagerViewModel。导入不代表所有类型都会在运行时加载。
global using LogQueryService = WPFControls.ProgramControl.Services.Log.LogQueryService;
global using SEMILogQueryService = WPFControls.ProgramControl.Services.Log.SEMILogQueryService;
global using SEMIManagerService = WPFControls.ProgramControl.Services.Log.SEMIManagerService;
global using PrintService = WPFControls.ProgramControl.Services.Printer.PrintService;
global using UserManagerService = WPFControls.ProgramControl.Services.User.UserManagerService;
global using AlarmInfoManagerViewModel = WPFControls.ProgramControl.ViewModels.Alarm.AlarmInfoManagerViewModel;
global using AlarmInfoQueryViewModel = WPFControls.ProgramControl.ViewModels.Alarm.AlarmInfoQueryViewModel;
global using ImageQueryViewModel = WPFControls.ProgramControl.ViewModels.Data.ImageQueryViewModel;
global using LogQueryViewModel = WPFControls.ProgramControl.ViewModels.Log.LogQueryViewModel;
global using SEMILogQueryViewModel = WPFControls.ProgramControl.ViewModels.Log.SEMILogQueryViewModel;
global using SEMIManagerViewModel = WPFControls.ProgramControl.ViewModels.Log.SEMIManagerViewModel;
global using PrintManagerViewModel = WPFControls.ProgramControl.ViewModels.Printer.PrintManagerViewModel;
global using PrintViewModel = WPFControls.ProgramControl.ViewModels.Printer.PrintViewModel;
global using ControlViewModel = WPFControls.ProgramControl.ViewModels.Shell.ControlViewModel;
global using UserInfoManagerViewModel = WPFControls.ProgramControl.ViewModels.User.UserInfoManagerViewModel;
