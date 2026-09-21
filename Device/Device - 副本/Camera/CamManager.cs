using Device.Camera.Gige;
using Device.Camera.Gige.Basler;
using Device.Camera.Gige.Dahua;
using Device.Camera.Gige.Definition;
using Device.Camera.Gige.Hik;
using Device.Camera.Gige.I_Tek;
using MvCamCtrl.NET;
using System;
using System.Collections.Generic;

namespace Device.Camera
{
    public class CamManager : ManagerBase<ICamera, GigeCamParam>
    {
        public override void Config(List<GigeCamParam> param)
        {
            Devices.Clear();
            foreach (var item in param)
                Create(item);
        }

        public override ICamera Create(GigeCamParam param)
        {
            switch (param.CamBrand)
            {
                case ECamBrand.DaHua:
                    DaHuaHelper DaHuaHelp = new DaHuaHelper();
                    DaHuaHelp.CamParam = param;
                    Devices.Add(DaHuaHelp);
                    return DaHuaHelp;
                case ECamBrand.Hik:
                    HikHelper HikHelp = new HikHelper();
                    HikHelp.CamParam = param;
                    Devices.Add(HikHelp);
                    return HikHelp;
                case ECamBrand.Basler:
                    BaslerHelper BaslerHelp = new BaslerHelper();
                    BaslerHelp.CamParam = param;
                    Devices.Add(BaslerHelp);
                    return BaslerHelp;
                case ECamBrand.I_Tek:
                    I_TekManager i_TekManager = new I_TekManager();
                    i_TekManager.CamParam = param;
                    Devices.Add(i_TekManager);
                    return i_TekManager;
                default: throw new NotSupportedException($"{param.CamBrand} not support.");
            }
        }
    }
}