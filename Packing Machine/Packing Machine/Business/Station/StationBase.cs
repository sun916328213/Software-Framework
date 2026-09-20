using Packing_Machine.Infra.Tool;
using Prism.Ioc;
using System.Collections.Generic;
using System.Linq;
using Device;
using WPFControl.ProgramControl.Services;
using Device.RFID;
using Device.Light;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using System.Collections.Concurrent;

namespace Packing_Machine.Business.Station
{
    /// <summary>
    /// 工站流程
    /// </summary>
    public enum EStationProcess
    {
        进站流程,
        配件流程,
        贴标流程,
        入袋流程,
        出站流程,
    }

    /// <summary>
    /// 热封流程
    /// </summary>
    public enum EHeatSeal
    {
        抽真空流程,
        冲氮气流程,
        热封流程,
    }

    public enum ETrigger
    {
        打印标签,
        AL袋流程,
    }

    public class StationBase<T>
    {
        internal LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();
        internal readonly SecsTool _SecsMethod;

        internal readonly VisionTool _VisionMethod;
        internal readonly RFIDManager _RFIDManager;
        internal readonly PrintManagerService _PrintManagerService;
        internal readonly PLCTool _PLCMethod;

        internal readonly LightControlManager _LightControlManager;

        private readonly IContainerProvider _container;
        


        public StationBase(IContainerProvider container)
        {
            try
            {
                _container = container;
                _SecsMethod = container.Resolve<SecsTool>();

                _VisionMethod = container.Resolve<VisionTool>();
                _RFIDManager = container.Resolve<RFIDManager>();

                _PrintManagerService = container.Resolve<PrintManagerService>();
                _PLCMethod = container.Resolve<PLCTool>();

                _LightControlManager = container.Resolve<LightControlManager>();
            }
            catch
            {
            }
        }

        public ConcurrentDictionary<string, T> Infos { get; set; } = new ConcurrentDictionary<string, T>();


        /// <summary>
        /// 根据ID获取并移除数据
        /// </summary>
        /// <returns></returns>
        public T GetAndRemove(string ID)
        {
            var d1 = Infos.TryRemove(ID, out T value);
            if (d1)
                return value;
            else
                return default;
        }

        /// <summary>
        /// 添加或者更新数据
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        public void AddUpdate(string id,T value)
        { 
            if (Infos.TryGetValue(id, out var value1))
            {
                Infos.TryUpdate(id, value, value);
            }
            else
                Infos.TryAdd(id, value);
        }
    }

    public class Result
    { 
        public EPLCResult CheckResult { get; set; }= EPLCResult.NG;
    }
}
