using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.CamLinkCard
{
    public abstract class CamLinkCardBase : ICamLinkCard
    {
        public abstract string Name { get  ; set  ; }

        public ECamLinkCardType CamLinkCardType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public abstract event GrabOverHandler OnGrabOver;

        public abstract bool Connect(out string pErrorInfo, string pPath_Config);

        public abstract bool DisConnect();
      
    }
}
