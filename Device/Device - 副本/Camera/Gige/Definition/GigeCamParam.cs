using Device.Camera.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Camera.Gige.Definition
{
    /// <summary>
    /// Gige相机参数
    /// </summary>
    public class GigeCamParam : IGigeCamBaseParam, IGigeCamControlParam
    {
        public string Name { get; set; }
        public string IP { get; set; }
        public int Exposure { get; set; } = 20;
        public double Gain { get; set; } = 0;
        public ETriggerMode TriggerMode { get; set; }= ETriggerMode.OFF;
        public int TimeOut { get; set; }= 1000;
        public EImageFormat ImageType { get; set; }
        public ECamBrand CamBrand { get; set; }
        public ECamInterface CamInterface { get; set; }
    }

}
