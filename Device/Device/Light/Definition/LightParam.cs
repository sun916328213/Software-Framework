using CommunityToolkit.Mvvm.ComponentModel;
using Extension.DesignPattern.ObserverPattern;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Light.Definition
{
    /// <summary>
    /// 光源控制器参数
    /// </summary>
    public class LightParam
    {
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 串口号
        /// </summary>
        public string ComName { get; set; }
        public ELightType LightType { get; set; } = ELightType.CST;
    }

    
    /// <summary>
    /// 光源控制器执行结果
    /// </summary>
    internal enum ELightResult
    { 
       Success,
       Fail,
    }

    /// <summary>
    /// 光源检测项目
    /// </summary>
    public class LightItem : ObservableObject
    {
        private int lightName, channelindex, lightintensity;
        /// <summary>
        /// 光源名称
        /// </summary>
        public int LightName
        {
            get => lightName;
            set => SetProperty(ref lightName, value);
        }
        /// <summary>
        /// 光源通道 1-4/1-8
        /// </summary>
        public int ChannelIndex
        {
            get => channelindex;
            set => SetProperty(ref channelindex, value);
        }
        /// <summary>
        /// 光源亮度 0-255
        /// </summary>
        public int LightIntensity
        {

            get => lightintensity;
            set => SetProperty(ref lightintensity, value);
        }
    }
}
