using Device.Printer.Definition;
using System;

namespace Device.Printer.SATO
{
    internal class SATOHelper : IPrinter
    {
        public PrinterParam Param { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool Connect()
        {
            throw new NotImplementedException();
        }

        public bool DisConnect()
        {
            throw new NotImplementedException();
        }

        public string GetPrinterStatus()
        {
            throw new NotImplementedException();
        }

        public bool IsPaperOut()
        {
            throw new NotImplementedException();
        }

        public bool IsRibbonOut()
        {
            throw new NotImplementedException();
        }
    }
}
