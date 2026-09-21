using Device.Printer.Definition;
using Zebra.Sdk.Comm;
using Zebra.Sdk.Printer;

using ZebraSdk = Zebra.Sdk;

namespace Device.Printer.Zebra
{
    public class ZebraHelper : IPrinter
    {
        public string Name { get => Param.Name; set=>Param.Name=value; }
        
        public PrinterParam Param { get; set; }

        Connection connection;
        ZebraPrinter printer;

        public bool Connect()
        {
            connection = new TcpConnection(Param.IP, TcpConnection.DEFAULT_ZPL_TCP_PORT);
            connection.Open();
            printer = ZebraPrinterFactory.GetInstance(connection);
            return true;
        }

        public bool DisConnect()
        {
            connection.Close();
            return true;
        }

        public string GetPrinterStatus()
        {
            string msg = string.Empty;
            ZebraSdk.Printer.PrinterStatus printerStatus = printer.GetCurrentStatus();
            if (printerStatus.isReadyToPrint)
            {
                msg += "Ready";
            }
            if (printerStatus.isPaused)
            {
                if (msg == string.Empty)
                    msg += "Pause";
                else
                    msg += "/Pause";
            }
            if (printerStatus.isHeadOpen)
            {
                if (msg == string.Empty)
                    msg += "HeadOpen";
                else
                    msg += "/HeadOpen";
            }
            if (printerStatus.isPaperOut)
            {
                if (msg == string.Empty)
                    msg += "PaperOut";
                else
                    msg += "/PaperOut";

            }
            if (printerStatus.isRibbonOut)
            {
                if (msg == string.Empty)
                    msg += "RibbonOut";
                else
                    msg += "/RibbonOut";

            }
            if (printerStatus.isReceiveBufferFull)
            {
                if (msg == string.Empty)
                    msg += "ReceiveBufferFull";
                else
                    msg += "/ReceiveBufferFull";
            }
            return msg;
        }

        public bool IsPaperOut()
        {
            ZebraSdk.Printer.PrinterStatus printerStatus; printerStatus = printer.GetCurrentStatus();
            if (printerStatus.isPaperOut)
                return true;
            else
                return false;
        }

        public bool IsRibbonOut()
        {
            ZebraSdk.Printer.PrinterStatus printerStatus; printerStatus = printer.GetCurrentStatus();
            if (printerStatus.isRibbonOut)
                return true;
            else
                return false;
        }
    }
}
