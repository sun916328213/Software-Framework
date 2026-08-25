using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Packing_Machine.Data
{
    public static  class PCAlarm
    {
        public const short Tool_Error = 1;
        public const short Control_Error = 2;


        public const short Cam_Error = 3;
        public const short LightControl_Error = 4;
        public const short Scan_Error = 5;
        public const short RFID_Error = 6;

        public const short Community_Error = 7;
        public const short Secs_Error = 8;

    }
}
