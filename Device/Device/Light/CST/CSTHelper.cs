using CSTController_API;
using Device.Light.Definition;
using System;

namespace  Device.Light.CST
{
    internal class CSTHelper : LightControlBase
    {
        CSTControllerAPI Controller = new CSTControllerAPI();

        public override bool IsConnected => Controller.KeepAlive() == CSTControllerAPI.SUCCESS;


        protected override ELightResult ConnectRefactor()
        {
            int comindex = Convert.ToInt32(Param.ComName.ToUpper().Replace("COM", ""));
            var ret= Controller.CreateSerialPort(comindex);
            if (ret == CSTControllerAPI.SUCCESS)
                return  ELightResult.Success;
            else
                throw new Exception("Light Connect Fail!!");
        }

        protected override ELightResult DisConnectRefactor()
        {
            var ret= Controller.ReleaseSerialPort();
            if(ret == CSTControllerAPI.SUCCESS) 
                return ELightResult.Success;
            else
                throw new Exception("Light DisConnect Fail!!");
        }

        protected override ELightResult SetlightRefactor(int intensity, int channel = 1)
        {
            var ret = Controller.SetDigitalValue(channel, intensity);
            if (ret != CSTControllerAPI.SUCCESS)
                throw new Exception("Light Set Fail!!");
            else
                return ELightResult.Success;
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
