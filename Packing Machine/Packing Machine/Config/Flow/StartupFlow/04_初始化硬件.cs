using Communication;
using Communication.MC;
using Extension.Core.CompareEx;
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
    CamManager _CamManager;
    CamManagerService _CamManagerService;

    LightControlManager _LightControlManager;
    LightControlManagerService _LightControlManagerService;

    ScanManager _ScanManager;
    ScanManagerService _ScanManagerService;

    RFIDManager _RFIDManager;
    RFIDManagerService _RFIDManagerService;

    public void Initialize()
    {
        // 初始化逻辑
        _CamManager = ContainerManager.Resolve<CamManager>();
        _CamManagerService = ContainerManager.Resolve<CamManagerService>();

        _LightControlManager = ContainerManager.Resolve<LightControlManager>();
        _LightControlManagerService = ContainerManager.Resolve<LightControlManagerService>();

        _ScanManager = ContainerManager.Resolve<ScanManager>();
        _ScanManagerService = ContainerManager.Resolve<ScanManagerService>();

        _RFIDManager = ContainerManager.Resolve<RFIDManager>();
        _RFIDManagerService = ContainerManager.Resolve<RFIDManagerService>();
    }

    public void Run()
    {
        _CamManager.Config(_CamManagerService.Model.DataCollection.ToList());
        Console.WriteLine("相机加载成功");
        _CamManager.Connect();
        Console.WriteLine("相机连接成功");

        _LightControlManager.Config(_LightControlManagerService.Model.DataCollection.ToList());
        Console.WriteLine("光源控制器加载成功");
        _LightControlManager.Connect();
        Console.WriteLine("光源控制器连接成功");
        for (int index = 0; index < _LightControlManager.Count; index++)
        {
            for (int i = 1; i <= 8; i++)
                _LightControlManager[index].Setlight(0, i);
        }

        _ScanManager.Config(_ScanManagerService.Model.DataCollection.ToList());
        Console.WriteLine("扫码枪加载成功");
        _ScanManager.Connect();
        Console.WriteLine("扫码枪连接成功");

        _RFIDManager.Config(_RFIDManagerService.Model.DataCollection.ToList());
        Console.WriteLine("RFID加载成功");
        _RFIDManager.Connect();
        Console.WriteLine("RFID连接成功");
    }
}