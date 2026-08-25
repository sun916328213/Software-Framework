using System;
using System.Threading.Tasks;
using Communication.Serial;
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
         private   SerialHelper mPort = new SerialHelper();

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
            mPort.SerialParam = ScanParam;
            mPort.DataReceivedEvent += (sender, bytes) =>
            {
                Task.Run(() =>
                {
                    mValueCallBack(sender, bytes);
                });
            };
            return mPort.Connect();
        }

        public bool DisConnect()
        {
            return mPort.DisConnect();
        }

        public abstract void Trigger();
      
    }
}
