// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Abstractions、MotionControl.Core.Definitions、MotionControl.Runtime.Vendors.ADLINK、MotionControl.Runtime.Vendors.Googol、MotionControl.Runtime.Vendors.Inovance、MotionControl.Runtime.Vendors.Leadshine。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Vendors.ADLINK;
using MotionControl.Runtime.Vendors.Googol;
using MotionControl.Runtime.Vendors.Inovance;
using MotionControl.Runtime.Vendors.Leadshine;
using MotionControl.Runtime.Vendors.Simulation;
using MotionControl.Runtime.Vendors.ZMotion;

namespace MotionControl.Runtime.Services
{
    /// <summary>
    /// 运动控制卡管理类（注册制工厂）
    /// <para>内置仿真/凌华/雷赛品牌注册，开箱即用；</para>
    /// <para>第三方新增品牌：继承MotionControlBase后，以自定义MotionDriverId调用Register，无需修改本项目源码。</para>
    /// </summary>
    public sealed class MotionManager : IDisposable
    {
        private readonly Dictionary<MotionDriverId, Func<MotionCardParam, IMotionCard>> _factories = new();
        private readonly object _lifecycleSync = new();
        private bool _disposed;

        /// <summary>当前由管理器持有的运动控制卡。</summary>
        public MotionDeviceCollection<IMotionCard> Devices { get; } = new();

        /// <summary>
        /// 初始化 MotionManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        public MotionManager()
        {
            // 内置品牌默认注册（重复Register同类型会覆盖）
            Register(EMotionCardType.Simulation, p => new SimulationMotionCard());
            Register(EMotionCardType.ADLINK, p => new AdlinkHelper());
            Register(EMotionCardType.Leadshine, p => new LeadshineHelper());
            Register(EMotionCardType.Googol, p => new GoogolHelper());
            Register(EMotionCardType.ZMotion, p => new ZMotionHelper());
            Register(EMotionCardType.Inovance, p => new InovanceHelper());
        }

        /// <summary>
        /// 注册控制卡工厂（同一类型重复注册将覆盖旧实现）
        /// </summary>
        /// <param name="type">控制卡类型标识</param>
        /// <param name="factory">创建实例的工厂委托</param>
        public void Register(EMotionCardType type, Func<MotionCardParam, IMotionCard> factory)
            => Register(MotionDriverId.From(type), factory);

        /// <summary>
        /// 使用字符串标识注册控制卡工厂，适合配置文件和第三方驱动。
        /// </summary>
        public void Register(string driverId, Func<MotionCardParam, IMotionCard> factory)
            => Register(new MotionDriverId(driverId), factory);

        /// <summary>
        /// 使用稳定驱动标识注册控制卡工厂；同一标识重复注册将覆盖旧实现。
        /// </summary>
        public void Register(MotionDriverId driverId, Func<MotionCardParam, IMotionCard> factory)
        {
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                if (string.IsNullOrWhiteSpace(driverId.Value))
                    throw new ArgumentException("驱动标识不能为空。", nameof(driverId));
                ArgumentNullException.ThrowIfNull(factory);
                _factories[driverId] = factory;
            }
        }

        /// <summary>
        /// 指定类型是否已注册
        /// </summary>
        public bool IsRegistered(EMotionCardType type) => IsRegistered(MotionDriverId.From(type));

        /// <summary>指定字符串驱动标识是否已注册。</summary>
        public bool IsRegistered(string driverId) => IsRegistered(new MotionDriverId(driverId));

        /// <summary>指定驱动标识是否已注册。</summary>
        public bool IsRegistered(MotionDriverId driverId)
        {
            lock (_lifecycleSync)
                return _factories.ContainsKey(driverId);
        }

        /// <summary>按配置重新创建设备集合。</summary>
        public void Config(List<MotionCardParam> param)
        {
            ArgumentNullException.ThrowIfNull(param);
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();

                // 先完整创建设备，避免中途失败时破坏当前可用配置。
                var replacements = CreateReplacementDevices(param);
                try
                {
                    DisconnectOrThrow(Devices.Snapshot(), "替换配置");
                }
                catch
                {
                    DisconnectBestEffort(replacements);
                    throw;
                }
                Devices.ReplaceWith(replacements);
            }
        }

        /// <summary>根据配置创建并持有一个运动控制卡。</summary>
        public IMotionCard Create(MotionCardParam param)
        {
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                var device = CreateDevice(param);
                Devices.Add(device);
                return device;
            }
        }

        /// <summary>连接当前集合中的全部控制卡。</summary>
        public void Connect() => ConnectAllResult();

        /// <summary>连接全部控制卡，并按设备顺序返回每张卡的结果。</summary>
        public MotionBatchResult ConnectAllResult()
        {
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                return ExecuteBatch(Devices.Snapshot(), device => device.ConnectResult(), "连接");
            }
        }

        /// <summary>在线程池中连接当前集合中的全部控制卡。</summary>
        public async Task ConnectAsync() => _ = await ConnectAllResultAsync().ConfigureAwait(false);

        /// <summary>在线程池中连接全部控制卡，并返回逐设备结果。</summary>
        public Task<MotionBatchResult> ConnectAllResultAsync(CancellationToken cancellationToken = default)
            => Task.Run(ConnectAllResult, cancellationToken);

        /// <summary>断开当前集合中的全部控制卡。</summary>
        public void Disconnect() => DisconnectAllResult();

        /// <summary>断开全部控制卡，并按设备顺序返回每张卡的结果。</summary>
        public MotionBatchResult DisconnectAllResult()
        {
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                return ExecuteBatch(Devices.Snapshot(), device => device.DisconnectResult(), "断开");
            }
        }

        /// <summary>添加已创建的控制卡。</summary>
        public void Add(IMotionCard device)
        {
            ArgumentNullException.ThrowIfNull(device);
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                Devices.Add(device);
            }
        }

        /// <summary>断开并移除控制卡；断开失败时保留设备并抛出异常。</summary>
        public void Remove(IMotionCard device)
        {
            ArgumentNullException.ThrowIfNull(device);
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                if (!Devices.Contains(device))
                    return;

                DisconnectOrThrow([device], "移除设备");
                Devices.Remove(device);
            }
        }

        /// <summary>断开并清空全部控制卡；断开失败时保留集合并抛出异常。</summary>
        public void Clear()
        {
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                DisconnectOrThrow(Devices.Snapshot(), "清空设备");
                Devices.Clear();
            }
        }

        /// <summary>根据设备名称查找控制卡。</summary>
        public IMotionCard? this[string deviceName]
            => Devices.FirstOrDefault(device => device.Name == deviceName);

        /// <summary>根据索引获取控制卡。</summary>
        public IMotionCard this[int index] => Devices[index];

        /// <summary>获取当前控制卡快照。</summary>
        public List<IMotionCard> DeviceCollection() => Devices.Snapshot();

        /// <summary>获取当前控制卡名称快照。</summary>
        public List<string> DeviceNameCollection() => Devices.Select(device => device.Name).ToList();

        /// <summary>当前控制卡数量。</summary>
        public int Count => Devices.Count;

        /// <summary>
        /// 紧急停止所有卡的所有轴
        /// </summary>
        public void StopAll() => StopAllResult();

        /// <summary>紧急停止全部控制卡，并按设备顺序返回每张卡的结果。</summary>
        public MotionBatchResult StopAllResult()
        {
            lock (_lifecycleSync)
            {
                ThrowIfDisposed();
                return ExecuteBatch(Devices.Snapshot(),
                    device => device.StopAllResult(EStopMode.Emergency), "紧急停止");
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 DisconnectBestEffort、DisposeAndSnapshot；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Dispose()
        {
            lock (_lifecycleSync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                DisconnectBestEffort(Devices.DisposeAndSnapshot());
            }
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateDevice。
        /// 正文中的关键协作调用包括 ThrowIfNull、ResolveDriverId、TryGetValue、nameof、factory；具体条件和先后次序见方法体。
        /// </summary>
        private IMotionCard CreateDevice(MotionCardParam param)
        {
            ArgumentNullException.ThrowIfNull(param);
            var driverId = param.ResolveDriverId();
            if (!_factories.TryGetValue(driverId, out var factory))
                throw new NotSupportedException($"驱动 {driverId} 未注册，请先调用{nameof(Register)}");
            var device = factory(param)
                ?? throw new InvalidOperationException($"驱动 {driverId} 的工厂返回了空实例。");
            device.Param = param;
            return device;
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateReplacementDevices。
        /// 正文中的关键协作调用包括 Add、CreateDevice、DisconnectBestEffort；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private List<IMotionCard> CreateReplacementDevices(IEnumerable<MotionCardParam> parameters)
        {
            var devices = new List<IMotionCard>();
            try
            {
                foreach (var parameter in parameters)
                    devices.Add(CreateDevice(parameter));
                return devices;
            }
            catch
            {
                DisconnectBestEffort(devices);
                throw;
            }
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectOrThrow。
        /// 正文中的关键协作调用包括 ExecuteBatch、ToList、DisconnectResult、Select、Where；具体条件和先后次序见方法体。
        /// </summary>
        private static void DisconnectOrThrow(IEnumerable<IMotionCard> devices, string operation)
        {
            var result = ExecuteBatch(devices.ToList(), device => device.DisconnectResult(), operation);
            var failures = result.Results
                .Where(item => !item.Result.IsSuccess)
                .Select(item => $"{item.DeviceName}: {item.Result.Message ?? item.Result.Code.ToString()}")
                .ToList();

            if (failures.Count > 0)
                throw new InvalidOperationException($"{operation}失败，以下设备未能断开：{string.Join("；", failures)}");
        }

        /// <summary>
        /// 执行断开连接操作；对应入口为 DisconnectBestEffort。
        /// 正文中的关键协作调用包括 DisconnectResult；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static void DisconnectBestEffort(IEnumerable<IMotionCard> devices)
        {
            foreach (var device in devices)
            {
                try
                {
                    device.DisconnectResult();
                }
                catch
                {
                    // 清理阶段必须继续处理其余设备。
                }
            }
        }

        /// <summary>
        /// 执行当前入口的处理流程；对应入口为 ExecuteBatch。
        /// 正文中的关键协作调用包括 operation、Failure、Add；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private static MotionBatchResult ExecuteBatch(
            IReadOnlyList<IMotionCard> devices,
            Func<IMotionCard, MotionResult> operation,
            string operationName)
        {
            var results = new List<MotionDeviceResult>(devices.Count);
            for (var index = 0; index < devices.Count; index++)
            {
                var device = devices[index];
                MotionResult result;
                try
                {
                    result = operation(device);
                }
                catch (Exception exception)
                {
                    result = MotionResult.Failure(
                        EMotionResult.Fail,
                        $"{operationName}设备时发生异常：{exception.Message}");
                }
                string deviceName;
                try
                {
                    deviceName = device.Name;
                }
                catch
                {
                    deviceName = $"Device[{index}]";
                }
                results.Add(new MotionDeviceResult(index, deviceName, result));
            }
            return new MotionBatchResult(results);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ThrowIfDisposed。
        /// 正文中的关键协作调用包括 ThrowIf；具体条件和先后次序见方法体。
        /// </summary>
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}



