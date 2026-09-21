using Device.RFID.CK;
using Device.RFID.Definition;
using System;
using System.Collections.Generic;

namespace Device.RFID
{
    public class RFIDManager : ManagerBase<IRFID, RFIDParam>
    {
        public override void Config(List<RFIDParam> param)
        {
            Devices.Clear();
            foreach (var item in param)
                Create(item);
        }

        public override IRFID Create(RFIDParam param)
        {
            switch (param.RFIDType)
            {
                case ERFIDType.椹诺XK15AR01:
                    var sn = new XK15A_R01Helper();
                    sn.Param= param;
                    Devices.Add(sn);
                    return sn;
                case ERFIDType.晨控S650:
                    var ck = new CK_S650Helper();
                    ck.Param= param;
                    Devices.Add(ck);
                    return ck;
                default: throw new NotSupportedException($"{param.RFIDType} not support.");
            }
        }
    }
}