using CSharp_OPTControllerAPI;
using CSTController_API;
using Device.Light.Definition;
using System;
using System.Collections.Generic;

namespace Device.Light.OPT
{
    internal class OPTHelper : LightControlBase
    {
        OPTControllerAPI Controller=new OPTControllerAPI();


        public override bool IsConnected => Controller.EnableKeepalive(true) == OPTControllerAPI.OPT_SUCCEED;



        protected override ELightResult ConnectRefactor()
        {
            var ret= Controller.InitSerialPort(Param.ComName);
            if (ret == OPTControllerAPI.OPT_SUCCEED)
                return ELightResult.Success;
            else
                throw new Exception("Light Connect Fail!!");
        }

        protected override ELightResult DisConnectRefactor()
        {
            var ret= Controller.ReleaseSerialPort();
            if (ret == OPTControllerAPI.OPT_SUCCEED)
                return ELightResult.Success;
            else
                throw new Exception("Light DisConnect Fail!!");
        }

        protected override ELightResult SetlightRefactor(int intensity, int channel = 1)
        {
            var ret= Controller.SetIntensity(channel, intensity);
            if (ret == OPTControllerAPI.OPT_SUCCEED)
                return ELightResult.Success;
            else
                throw new Exception("Light Set Fail!!");
        }

        protected override void SetlightAllRefactor(int intensity, int channelCount = 4)
        {
            for (int i = 1; i <= channelCount; i++)
            {
                var ret = Setlight(intensity, i);
                if (!ret)
                    throw new Exception("Light Set Fail!!");
            }
        }
    }
}
