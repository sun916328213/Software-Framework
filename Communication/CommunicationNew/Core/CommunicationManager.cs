using System.Collections.Concurrent;
using CommunicationNew.Composition;

namespace CommunicationNew.Core
{
    /// <summary>
    /// 通信设备统一管理器 —— 管理所有通信设备的生命周期。
    /// 外部系统通过此管理器获取设备，无需关心具体协议实现。
    /// </summary>
    public class CommunicationManager : IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, ICommunicationDevice> _devices = new();
        private readonly ICommunicationDeviceFactory _factory;
        private bool _disposed;

        /// <summary>使用可注入的设备 factory 创建设备管理器。</summary>
        public CommunicationManager(ICommunicationDeviceFactory? factory = null)
        {
            _factory = factory ?? CommunicationDefaults.CreateFactory();
        }

        /// <summary>所有已注册的设备</summary>
        public IReadOnlyCollection<ICommunicationDevice> Devices => _devices.Values.ToList().AsReadOnly();

        /// <summary>
        /// 创建并注册一个通信设备
        /// </summary>
        public ICommunicationDevice CreateDevice(CommunicationParam param)
        {
            var device = _factory.Create(param);
            _devices[device.DeviceId] = device;
            return device;
        }

        /// <summary>
        /// 注册已有设备
        /// </summary>
        public void RegisterDevice(ICommunicationDevice device)
        {
            _devices[device.DeviceId] = device;
        }

        /// <summary>
        /// 按ID获取设备
        /// </summary>
        public ICommunicationDevice? GetDevice(string deviceId)
        {
            return _devices.TryGetValue(deviceId, out var device) ? device : null;
        }

        /// <summary>
        /// 按名称获取设备
        /// </summary>
        public ICommunicationDevice? GetDeviceByName(string name)
        {
            return _devices.Values.FirstOrDefault(d => d.DeviceName == name);
        }

        /// <summary>
        /// 按协议类型获取设备列表
        /// </summary>
        public List<ICommunicationDevice> GetDevicesByType(ProtocolType type)
        {
            return _devices.Values.Where(d => d.ProtocolType == type).ToList();
        }

        /// <summary>
        /// 移除并释放设备
        /// </summary>
        public async Task<bool> RemoveDeviceAsync(string deviceId)
        {
            if (_devices.TryRemove(deviceId, out var device))
            {
                await device.DisconnectAsync();
                await device.DisposeAsync();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 连接所有设备
        /// </summary>
        public async Task ConnectAllAsync(CancellationToken ct = default)
        {
            var tasks = _devices.Values.Select(d => d.ConnectAsync(ct)).ToList();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 断开所有设备
        /// </summary>
        public async Task DisconnectAllAsync()
        {
            var tasks = _devices.Values.Select(d => d.DisconnectAsync()).ToList();
            await Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            if (_disposed) return;
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>断开并异步释放管理器拥有的全部设备。</summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            var devices = _devices.Values.ToArray();
            _devices.Clear();
            var errors = new List<Exception>();
            foreach (var device in devices)
            {
                try
                {
                    await device.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                    await device.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // 先释放其余设备，结束后将全部错误交给调用方处理。
                    errors.Add(ex);
                }
            }

            GC.SuppressFinalize(this);
            if (errors.Count > 0)
            {
                throw new AggregateException("一个或多个通信设备释放失败。", errors);
            }
        }
    }
}
