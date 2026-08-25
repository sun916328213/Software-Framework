using Device.Camera.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Camera.Gige.Definition
{
    /// <summary>
    /// Gige相机控制参数
    /// </summary>
   public interface IGigeCamControlParam
    {
        /// <summary>
        /// 相机名称
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// 相机的曝光时间,单位ms
        /// </summary>
        int Exposure { get; set; }
        /// <summary>
        /// 相机的增益
        /// </summary>
        double Gain { get; set; }
        /// <summary>
        /// 相机的触发方式  
        /// </summary>
        ETriggerMode TriggerMode { get; set; }
    }

    /// <summary>
    /// Gige相机参数
    /// </summary>
    interface IGigeCamBaseParam
    {
        /// <summary>
        /// 相机名称
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// 相机的IP地址
        /// </summary>
        string IP { get; set; }
        /// <summary>
        /// 超时,单位ms
        /// </summary>
        int TimeOut { get; set; }
        /// <summary>
        /// 图像输出类型
        /// </summary>
        EImageFormat ImageType { get; set; }
        /// <summary>
        /// 相机品牌
        /// </summary>
        ECamBrand CamBrand { get; set; }
        /// <summary>
        /// 相机接口
        /// </summary>
        ECamInterface CamInterface { get; set; }
    }
}
