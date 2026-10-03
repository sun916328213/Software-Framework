// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：EStationProcess、EHeatSeal、ETrigger、StationBase、Result。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、MachineApp.Infra.Tool、System.Collections.Generic、System.Linq、WPFControls.ProgramControl.Services、MachineApp.Infra.Device。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using MachineApp.Infra.Tool;

using System.Collections.Generic;
using System.Linq;
using WPFControls.ProgramControl.Services;
using MachineApp.Infra.Device;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using System.Collections.Concurrent;

namespace MachineApp.Business.Station
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

    /// <summary>
    /// ETrigger 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public enum ETrigger
    {
        打印标签,
        AL袋流程,
    }

    /// <summary>
    /// StationBase 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public class StationBase<T>
    {
        internal IProductionLog _LogInfoService => _logInfoService();
        internal readonly IProductionSecs _SecsMethod;

        internal readonly IProductionVision _VisionMethod;
        internal readonly IProductionDevices _devices;
        internal readonly IProductionPrinter _PrintManagerService;
        internal readonly IProductionPlc _PLCMethod;



        private readonly Func<IProductionLog> _logInfoService;
        


        /// <summary>
        /// 初始化 StationBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Secs、Vision、Devices、Printer、Plc；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public StationBase(StationServices services)
        {
            try
            {
                _logInfoService = services.LogInfo;
                _SecsMethod = services.Secs();

                _VisionMethod = services.Vision();
                _devices = services.Devices();

                _PrintManagerService = services.Printer();
                _PLCMethod = services.Plc();


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

    /// <summary>
    /// Result 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public class Result
    { 
        public EPLCResult CheckResult { get; set; }= EPLCResult.NG;
    }
}
