using Device.Light;
using Prism.Ioc;
using System;
using WPFControl.CustomControl.Models;
using WPFControl.Helper;

namespace WPFControl.CustomControl.Services
{
    public class LightControlService
    {
        /// <summary>
        /// 管理类
        /// </summary>
        internal LightControlManager LightControlManager;

        int ChannelCount = 8;

        public LightControlService(IContainerProvider container)
        {
            LightControlManager= container.Resolve<LightControlManager>();
        }

        /// <summary>
        /// 设置光源亮度
        /// </summary>
        /// <param name="name">光源控制器名称</param>
        /// <param name="intensity">亮度</param>
        /// <param name="channel">通道</param>
        public void SetLight(string name,int intensity,int channel)
        {
            TryCatchHelper.TryExecute(() =>
            {
                LightControlManager[name].Setlight(intensity, channel + 1);
            });
        }

        public void OpenAll(string name, int intensity)
        {
            TryCatchHelper.TryExecute(() =>
            {
                for(int index=1;index<=ChannelCount;index++)
                LightControlManager[name].Setlight(intensity, index);
            });
        }

        public void CloaseAll(string name )
        {
            TryCatchHelper.TryExecute(() =>
            {
                for (int index = 1; index <= ChannelCount; index++)
                    LightControlManager[name].Setlight(0,  index);
            });
        }
    }
}
