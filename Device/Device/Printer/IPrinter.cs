using Device.Printer.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device.Printer
{
    public interface IPrinter : IDevice
    {
        /// <summary>
        /// 打印机参数
        /// </summary>
        PrinterParam Param { get; set; }
        /// <summary>
        /// 获取打印机状态
        /// </summary>
        /// <returns></returns>
        string GetPrinterStatus();
        /// <summary>
        /// 色带是否用尽
        /// </summary>
        /// <returns></returns>
        bool IsRibbonOut();
        /// <summary>
        /// 纸张是否用尽
        /// </summary>
        /// <returns></returns>
        bool IsPaperOut();
    }
}
