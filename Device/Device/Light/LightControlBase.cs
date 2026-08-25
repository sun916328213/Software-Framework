using CSharp_OPTControllerAPI;
using Device.Light.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Device.Light
{
    internal abstract class LightControlBase : ILightControl
    {
        public virtual string Name { get => Param.Name; set => Param.Name = value; }
        public  virtual LightParam Param { get; set; }
        public abstract bool IsConnected { get; }


        public bool Connect()
        {
            var iRet = ConnectRefactor();
            if (iRet == 0)
                return true;
            else
                return false;
        }

        protected abstract ELightResult ConnectRefactor();


        public   bool DisConnect()
        {
            var iRet = DisConnectRefactor();
            if (iRet == 0)
                return true;
            else
                return false;
        }
        protected abstract ELightResult DisConnectRefactor();


        public   bool Setlight(int intensity, int channel = 1)
        {
            var iRet = SetlightRefactor(intensity, channel);
            if (iRet == 0)
                return true;
            else
                return false;
        }
        protected abstract ELightResult SetlightRefactor(int intensity, int channel = 1);

        public bool SetlightAll(int intensity, int channelCount = 4)
        {
            SetlightAllRefactor(intensity, channelCount);
            return true;
        }

        protected abstract void SetlightAllRefactor(int intensity, int channelCount);
    }
}
