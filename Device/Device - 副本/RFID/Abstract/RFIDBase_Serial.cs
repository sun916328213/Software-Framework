using Communication.Serial;
using Device.RFID.Definition;
using System;
using System.Collections.Generic;
using System.Text;

namespace Device.RFID.Abstract
{
    internal abstract class RFIDBase_Serial : IRFID
    {
        public RFIDParam Param { get; set; }

       internal SerialHelper SerialHelper { get; set; } = new SerialHelper();

        public bool IsConnected => SerialHelper.IsConnect;

        public string Name
        {
            get => Param.Name;
            set => Param.Name = value;
        }

        public bool Connect()
        {
            if (Param == null) throw new Exception("Serial Param not set");
            SerialHelper.SerialParam = Param;
            return SerialHelper.Connect();
        }

        public bool DisConnect()
        {
            return SerialHelper.DisConnect();
        }

        public abstract string Read(int page);
 

        public abstract string Read(int startPage, int length);
 

        public abstract void Write(int page, string data, int makeuplength );



        public abstract void Write(int startPage, List<string> data, int makeuplength );
       
    }
}
