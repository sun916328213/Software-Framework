using Device.Light.Definition;

namespace Device.Light
{
    public interface ILightControl:IDevice
    {
        /// <summary>
        /// 光源参数
        /// </summary>
        LightParam Param { get; set; }
        /// <summary>
        /// 是否连接
        /// </summary>
        bool IsConnected { get; }
       
        /// <summary>
        /// 设置光源亮度
        /// </summary>
        /// <param name="intensity">亮度</param>
        /// <param name="channel">通道</param>
        bool Setlight(int intensity,int channel=1);
        /// <summary>
        /// 设置所有光源亮度
        /// </summary>
        /// <param name="intensity"></param>
        /// <param name="channelCount"></param>
        /// <returns></returns>
        bool SetlightAll(int intensity, int channelCount = 4);
    }
}
