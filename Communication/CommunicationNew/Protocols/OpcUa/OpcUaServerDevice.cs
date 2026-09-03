using CommunicationNew.Core;

namespace CommunicationNew.Protocols.OpcUa
{
    /// <summary>
    /// OPC UA 服务器设备
    /// 维护内存中的节点数据存储，提供统一读写接口。
    /// 完整 OPC UA Server 需要 StandardServer + 配置文件 + 节点空间定义。
    /// 此处提供简化版实现，实际项目中可扩展集成完整 OPC UA Server。
    /// </summary>
    public class OpcUaServerDevice : CommunicationDeviceBase
    {
        private new OpcUaParam Param => (OpcUaParam)base.Param;
        private readonly Dictionary<string, byte[]> _nodeValues = new();

        public OpcUaServerDevice(OpcUaParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.OpcUa;
        }

        public override DeviceRole Role => DeviceRole.Server;

        /// <summary>已注册的节点列表</summary>
        public IReadOnlyCollection<string> Nodes => _nodeValues.Keys.ToList().AsReadOnly();

        /// <summary>注册一个变量节点</summary>
        public void RegisterNode(string nodeIdString, byte[] initialValue = null!)
        {
            _nodeValues[nodeIdString] = initialValue ?? Array.Empty<byte>();
        }

        public override Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                // 完整 OPC UA Server 需要: StandardServer 实例 + ApplicationConfiguration + NodeManager 注册
                // 此处标记为已启动，数据存储使用内存字典
                IsConnected = true;
                OnConnectionStateChanged(true, $"OPC UA服务器已启动: {Param.EndpointUrl}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                OnConnectionStateChanged(false, ex.Message);
                return Task.FromResult(false);
            }
        }

        public override Task DisconnectAsync(CancellationToken ct = default)
        {
            _nodeValues.Clear();
            IsConnected = false;
            OnConnectionStateChanged(false, "OPC UA服务器已停止");
            return Task.CompletedTask;
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("设备未启动");
            if (_nodeValues.TryGetValue(address, out var value))
            {
                var result = new byte[Math.Min(length, value.Length)];
                Array.Copy(value, result, result.Length);
                return Task.FromResult(result);
            }
            return Task.FromResult(Array.Empty<byte>());
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("设备未启动");
            _nodeValues[address] = data;
            return Task.FromResult(true);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA服务器不支持原始字节发送");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA服务器不支持原始收发");
    }
}
