using Device.Camera;
using Device.Light;
using Device.RFID;
using Device.Scan;
using Packing_Machine.Data;
using Packing_Machine.Infra.Tool;
using Prism.Ioc;
using SecsGem.Core;
using WPFControl.Helper;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.Business.Flow
{
    internal class Close
    {
        readonly IContainerProvider _container;
        readonly ISecsGemRuntime _secsRuntime;

        public Close(IContainerProvider container, ISecsGemRuntime secsRuntime)
        {
            _container = container;
            _secsRuntime = secsRuntime;
        }
        
        /// <summary>
        /// 关闭
        /// </summary>
        public void Closed()
        {
            TryCatchHelper.TryExecute(() =>
            {
                GlobalData.Save();
                _container.Resolve<PrintManagerService>().Close();

                _container.Resolve<PLCTool>().StopRefresh();
                _secsRuntime.DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                _container.Resolve<CamManager>().Dispose();
                _container.Resolve<ScanManager>().Dispose();
                _container.Resolve<RFIDManager>().Dispose();

                LightControlManager _LightControlManager = _container.Resolve<LightControlManager>();
                for (int i = 1; i <= 8; i++)
                    _LightControlManager[0].Setlight(0, i);
                _LightControlManager.Dispose();
            });
        }
    }
}
