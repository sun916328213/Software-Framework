using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Device.RFID.Definition
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ERFIDType
    {
        晨控S650,
        椹诺XK15AR01
    }
}
