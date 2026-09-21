using Device;
using Device.Scan.Definition;
using System;

namespace Device.Scan
{
  
    /// <summary>
    /// 数据回调的委托
    /// </summary>
    public delegate void ValueCallBack(byte[] values);

   

    /// <summary>
    /// 关于读码器的相关接口
    /// </summary>
    public interface IScan: IDevice
    {
        /// <summary>
        /// 扫码枪参数
        /// </summary>
        ScanParam ScanParam { get; set; }
        /// <summary>
        /// 连接器的类型
        /// </summary>
          ConnectorType Connector { get; }
        /// <summary>
        /// 软件触发
        /// </summary>
        void Trigger();
        /// <summary>
        /// 接收数据事件
        /// </summary>
        event EventHandler<byte[]> DataReceived;
    }
 
}
