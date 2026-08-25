using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.CamLinkCard
{
    /// <summary>
    /// 相机采集卡品牌
    /// </summary>
    public enum ECamLinkCardType
    {
        Matrox,
        Euresys,
    }

    /// <summary>
    /// 采集卡采集图像完成委托
    /// </summary>
    /// <param name="image"></param>
    public delegate void GrabOverHandler(string name, object image);

    internal interface ICamLinkCard
    {
        /// <summary>
        /// 采集卡名称
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// 相机采集卡品牌
        /// </summary>
        ECamLinkCardType CamLinkCardType { get; set; }
        /// <summary>
        /// 采集图像完成事件
        /// </summary>
        event GrabOverHandler OnGrabOver;
        /// <summary>
        /// 连接采集卡
        /// </summary>
        /// <param name="pErrorInfo">初始化返回错误</param>
        /// <param name="pPath_Config">初始化文件路径</param>
        bool Connect(out string pErrorInfo, string pPath_Config);
        /// <summary>
        /// 关闭采集卡
        /// </summary>
        /// <returns></returns>
        bool DisConnect();
    }
}
