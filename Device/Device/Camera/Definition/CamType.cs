using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Device.Camera.Gige.Definition
{
    [JsonConverter (typeof(JsonStringEnumConverter))]
    /// <summary>
    /// 相机品牌
    /// </summary>
    public enum ECamBrand
    {
        Hik,
        DaHua,
        Basler,
        I_Tek,
        Xenics
    }
    [JsonConverter (typeof(JsonStringEnumConverter))]
    /// <summary>
    /// 相机接口类型
    /// </summary>
    public enum ECamInterface
    {
        Gige,
        Camlink,
    }

    [JsonConverter (typeof(JsonStringEnumConverter))]
    /// <summary>
    /// 相机触发方式
    /// </summary>
    public enum ETriggerMode
    {
        /// <summary>
        /// 关闭软件触发
        /// </summary>
        OFF,
        /// <summary>
        /// 软件触发
        /// </summary>
        Software,
        /// <summary>
        /// 自由运行
        /// </summary>
        Free,
        /// <summary>
        /// 帧触发 Line1
        /// </summary>
        Frame_Line1,
        /// <summary>
        /// 帧触发 Line2
        /// </summary>
        Frame_Line2,
        /// <summary>
        /// 帧触发+自由运行
        /// </summary>
        Frame_Free,
        /// <summary>
        /// 帧触发+编码器
        /// </summary>
        Frame_Encoder
    }
}
