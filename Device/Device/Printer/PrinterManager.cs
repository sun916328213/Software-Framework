using Device.Printer.Definition;
using Device.Printer.SATO;
using Device.Printer.Zebra;
using System;
using System.Collections.Generic;

namespace Device.Printer
{
    public class PrinterManager : ManagerBase<IPrinter, PrinterParam>
    {
        public override void Config(List<PrinterParam> param)
        {
            Devices.Clear();
            foreach (var item in param)
                Create(item);
        }

        public override IPrinter Create(PrinterParam param)
        {
            switch (param.PrinterType)
            {
                case EPrinterType.Zebra:
                    var ze = new ZebraHelper();
                    Devices.Add(ze);
                    return ze;
                case EPrinterType.SATO:
                    var sato = new SATOHelper();
                    Devices.Add(sato);
                    return sato;
                default: throw new NotSupportedException($"{param.PrinterType} not support.");
            }
        }
    }
}