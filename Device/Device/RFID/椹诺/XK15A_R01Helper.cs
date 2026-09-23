
using Device.RFID.Abstract;
using Extension.Core.Conversion;

namespace Device.RFID
{
    internal class XK15A_R01Helper : RFIDBase_Serial
    {
        public override string Read(int page)
        {
            string pagehex = ASCIIEx.ToHex(page.ToString());
            string order = $"0A 52 50 50 {pagehex} 0D".Replace(" ", "");
            byte[] buffer = order.ToBytes();
            var d1 = SendAndReceive(buffer);
            string d2 = d1.ToASCII(EEndianType.Little_Endian);
            if (d2.Contains("FAIL")) throw new Exception("RFID write fail");
            else
                return d2.CleanString();
        }

        public override string Read(int startPage, int length)
        {
            Span<char> data = new char[length * 8];
            int index = 0;
            for (int i = startPage; i <= length; i++)
            {
                var d1 = Read(i);

                d1.AsSpan().CopyTo(data.Slice(index++ * 8, 8));
            }
            return data.ToString().CleanString();
        }

        public override void Write(int page, string data, int makeuplength = 8)
        {
            if (data.Length > 8) throw new Exception("The data length exceeds the limit");
            if (data.Length < makeuplength) data = data.PadRight(makeuplength, '\0');
            string pagehex = ASCIIEx.ToHex(page.ToString());
            string datahex = ASCIIEx.ToHex(data);
            string order = $"0A 57 50 50 {pagehex} 2C {datahex} 0D".Replace(" ", "");
            byte[] buffer = order.ToBytes();
            var d1 = SendAndReceive(buffer);
            string d2 = d1.ToASCII(EEndianType.Little_Endian);
            if (d2.Contains("FAIL")) throw new Exception("RFID write fail");
        }

        public override void Write(int startPage, List<string> data, int makeuplength = 8)
        {
            foreach (var d in data)
                Write(startPage++, d, makeuplength);
        }
    }
}
