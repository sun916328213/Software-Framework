using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Printer.Definition
{
    public class PrinterParam
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// IP地址
        /// </summary>
        public string IP { get; set; }
        /// <summary>
        /// 打印机类型
        /// </summary>
        public EPrinterType PrinterType { get; set; }= EPrinterType.Zebra;
    }
}
