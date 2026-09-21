using Device.Light.CST;
using Device.Light.Definition;
using Device.Light.OPT;
using System;
using System.Collections.Generic;

namespace Device.Light
{
    public class LightControlManager : ManagerBase<ILightControl, LightParam>
    {
        public override void Config(List<LightParam> param)
        {
            Devices.Clear();
            foreach (var item in param)
                Create(item);
        }

        public override ILightControl Create(LightParam param)
        {
            switch (param.LightType)
            {
                case ELightType.CST:
                    var cst = new CSTHelper();
                    cst.Param = param;
                    Devices.Add(cst);
                    return cst;
                case ELightType.OPT:
                    var opt = new OPTHelper();
                    opt.Param = param;
                    Devices.Add(opt);
                    return opt;
                default:
                    throw new NotSupportedException($"{param.LightType} not support.");
            }
        }
    }
}