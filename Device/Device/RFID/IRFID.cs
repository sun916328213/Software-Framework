using System.Collections.Generic;
using Device.RFID.Definition;

namespace Device.RFID
{
    public  interface IRFID:IDevice
    {
        /// <summary>
        /// RFID参数
        /// </summary>
        RFIDParam Param { get; set; }
        /// <summary>
        /// 是否连接
        /// </summary>
        bool IsConnected { get; }
 
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="page">页码</param>
        /// <returns></returns>
        string Read(int page);
        /// <summary>
        /// 读取RFID数据
        /// </summary>
        /// <param name="startPage">开始页码</param>
        /// <param name="length">页码长度</param>
        /// <returns></returns>
        string Read(int startPage, int length);
        /// <summary>
        /// 写入RFID数据
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="data">数据</param>
        /// <param name="makeuplength">补全长度</param>
        void Write(int page, string data, int makeuplength=8 );
        /// <summary>
        /// 读取RFID数据
        /// </summary>
        /// <param name="startPage">开始页码</param>
        /// <param name="data">数据</param>
        /// <param name="makeuplength">补全长度</param>
        void Write(int startPage, List<string> data, int makeuplength=8 );
    }
}
