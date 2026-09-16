using Communication;
using Communication.MC;
using Extension.Core.ConvertEx;
using Extension.Core.FileEx;
using Packing_Machine.Business.Order;
using Packing_Machine.Data;
using Packing_Machine.Infra.Core;
using Packing_Machine.Infra.Tool;
using Prism.Regions;
using Prism.Services.Dialogs;
using Secs;
using WPFControl.ProgramControl.Services;

public class Script
{

    private static CommunicationManager _CommunicationManager;
    private static MCHelper _MCHelper;
    private static PLCTool _PLCMethod;
    private static SecsControl _SecsControl;


    private static AlarmInfoManagerService _AlarmInfoService;


    private static ControlService _ControlService;
    private static StatisticService _StatisticService;
    private static WindowMenuService _WindowMenuService;
    private static MenuService _MenuService;

    private static IRegionManager _IRegionManager;
    private static DialogService _DialogService;

    public static void Initialize()
    {
        // 初始化逻辑
        _MCHelper = ContainerManager.Resolve<MCHelper>();
        _CommunicationManager = ContainerManager.Resolve<CommunicationManager>();
        _PLCMethod = ContainerManager.Resolve<PLCTool>();

        _SecsControl = ContainerManager.Resolve<SecsControl>();


        _AlarmInfoService = ContainerManager.Resolve<AlarmInfoManagerService>();

        _ControlService = ContainerManager.Resolve<ControlService>();
        _StatisticService = ContainerManager.Resolve<StatisticService>();
        _WindowMenuService = ContainerManager.Resolve<WindowMenuService>();
        _MenuService = ContainerManager.Resolve<MenuService>();

        _IRegionManager = ContainerManager.Resolve<IRegionManager>();
        _DialogService = ContainerManager.Resolve<DialogService>();
    }

    public static void Run()
    {
        Console.WriteLine("开始执行脚本");
        Init_PLC();
//        Init_SEMI();
    }


    #region 通讯
    private static void Init_PLC()
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
        var d1 = _MCHelper.Connect(1);
        if (!d1) throw new Exception("PLC connect fail");
        _CommunicationManager.LoadConfig(registerInfos);
        _PLCMethod.CompleteAll(PLCSystem.PLC_SysTrigger, PLCSystem.PLC_SysTrigger, 1000);
    }

    private static void Init_SEMI()
    {
        _SecsControl.Config();
        var d = _SecsControl.Connect(false, "127.0.0.1", 5000);

        var alarm = _AlarmInfoService.GetAlarms().Select(x => new Secs.Variable.AlarmInfo { ID = x.ID.ToString(), Description = x.Info_EN, Enable = true }).ToList();
        _SecsControl._VIDManager.AddAlarm(alarm);
        _SecsControl._StatusManager.Config(VIDOrder.DeviceStatus, VIDOrder.DeviceMode, EventOrder.Device_Status, EventOrder.Device_Mode);
        _SecsControl._StatusManager.StartMonitoringAsync().ConfigureAwait(false);
        _SecsControl._ConnectManager.WaitOnlineAndOfflineAsync().ConfigureAwait(false);
    }
    #endregion


}
