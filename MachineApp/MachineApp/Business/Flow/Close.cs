// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Close。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、MachineApp.Infra.Device、MachineApp.Data、MachineApp.Infra.Tool、SecsGem.Core、WPFControls.Controls.Diagnostics。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using MachineApp.Infra.Device;
using MachineApp.Data;
using MachineApp.Infra.Tool;

using SecsGem.Core;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Services;

namespace MachineApp.Business.Flow
{
    /// <summary>
    /// Close 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    internal class Close
    {
        private readonly TryCatchHelper _errors;

        readonly Func<IProductionPrinter> _printer;
        readonly Func<IProductionPlc> _plc;
        readonly Func<IProductionDevices> _devices;
        readonly ISecsGemRuntime _secsRuntime;

        /// <summary>
        /// 初始化 Close 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public Close(Func<IProductionPrinter> printer, Func<IProductionPlc> plc, Func<IProductionDevices> devices, ISecsGemRuntime secsRuntime, TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            _printer = printer;
            _plc = plc;
            _devices = devices;
            _secsRuntime = secsRuntime;
        }
        
        /// <summary>
        /// 关闭
        /// </summary>
        public async Task ClosedAsync()
        {
            _errors.TryExecute(GlobalData.Save);
            _errors.TryExecute(() => _printer().Close());
            await _errors.TryExecuteAsync(() => _plc().StopRefreshAsync());
            await _errors.TryExecuteAsync(() => _secsRuntime.DisconnectAsync());
            await _errors.TryExecuteAsync(async () => await _devices().DisposeAsync());
        }
    }
}
