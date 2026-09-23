using System;
using System.Threading.Tasks;
using CommunicationNew.Protocols.Serial;
using Device.Scan.Definition;
using Device.Scan;

namespace Device.Scan
{
    internal abstract class ScanBase : IScan
    {
        public ScanParam ScanParam { get; set; }

        public abstract ConnectorType Connector { get; }

        public string Name { get; set; }

        /// <summary>
        /// 对于的连接串口
        /// </summary>
         private   SerialPortDevice mPort;

        /// <summary>
        /// 数据获取后的回调
        /// </summary>
        EventHandler<byte[]> mValueCallBack;
        public event EventHandler<byte[]> DataReceived
        {
            add
            {
                if (mValueCallBack != null && mValueCallBack.GetInvocationList().Contains(value))
                {
                    return;
                }
                mValueCallBack += value;
            }
            remove
            {
                mValueCallBack -= value;
            }
        }

        public bool Connect()
        {
            mPort?.Dispose();
            mPort = new SerialPortDevice(ScanParam);
            mPort.DataReceived += (sender, e) =>
            {
                Task.Run(() =>
                {
                    mValueCallBack(sender, e.Data);
                });
            };
            return Task.Run(() => mPort.ConnectAsync()).GetAwaiter().GetResult();
        }

        public bool DisConnect()
        {
            if (mPort == null) return true;
            Task.Run(() => mPort.DisconnectAsync()).GetAwaiter().GetResult();
            return true;
        }

        public abstract void Trigger();
      
    }
}
