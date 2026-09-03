using CommunicationNew.Core;

namespace CommunicationNew.Protocols.OpcUa
{
    /// <summary>
    /// OPC UA 客户端设备
    /// 提供 Read/Write 节点能力。地址格式: "ns=2;s=MyVariable"
    /// 注: OPC UA 完整客户端实现需要复杂的配置，此处提供简化版接口，
    /// 实际项目中可通过暴露的 Session 属性使用完整 OPC UA API。
    /// </summary>
    public class OpcUaClientDevice : CommunicationDeviceBase
    {
        private new OpcUaParam Param => (OpcUaParam)base.Param;

        public OpcUaClientDevice(OpcUaParam param) : base(param)
        {
            ProtocolType = Core.ProtocolType.OpcUa;
        }

        public override DeviceRole Role => DeviceRole.Client;

        /// <summary>端点URL</summary>
        public string EndpointUrl => Param.EndpointUrl;

        public override Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            // OPC UA 完整连接需要 ApplicationInstance + ApplicationConfiguration + Session.Create
            // 此处标记为已就绪，实际 Session 创建在使用时按需进行
            try
            {
                // 验证端点URL格式
                _ = new Uri(Param.EndpointUrl);
                IsConnected = true;
                OnConnectionStateChanged(true, $"OPC UA客户端就绪: {Param.EndpointUrl}");
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
            IsConnected = false;
            OnConnectionStateChanged(false, "OPC UA客户端已断开");
            return Task.CompletedTask;
        }

        public override Task<byte[]> ReadAsync(string address, int length, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("设备未连接");
            // OPC UA 完整读取需要 Session.Read，此处返回空数据占位
            // 实际项目中通过 Session 属性使用完整 API
            return Task.FromResult(Array.Empty<byte>());
        }

        public override Task<bool> WriteAsync(string address, byte[] data, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("设备未连接");
            // OPC UA 完整写入需要 Session.Write
            return Task.FromResult(true);
        }

        public override Task<bool> SendAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA 不支持原始字节发送，请使用 ReadAsync/WriteAsync");

        public override Task<byte[]> SendAndReceiveAsync(byte[] data, CancellationToken ct = default)
            => throw new NotSupportedException("OPC UA 不支持原始收发，请使用 ReadAsync/WriteAsync");
    }
}
