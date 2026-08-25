using DPLibrary_Base_F48.Tools;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;

namespace DPLibrary_Halcon_F48
{


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_BarCodeType
    {
        Auto,
        Code128,
        EAN13,
        UPC_A,
        UPC_E,
        Code39,
        ITF,
        Codabar
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_2DCodeType
    {
        QRCode,
        DataMatrix
    }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum E_QuietZoneType
    {
        QZ_true,
        QZ_false,
        QZ_tolerant
    }

    public class HCodeResult : IToolResult
    {
        /// <summary>
        /// 条码类型
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// 条码实际内容
        /// </summary>
        public string Code { get; set; }
        /// <summary>
        /// 条码置信度
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 条码质量
        /// </summary>
        public double Quality { get; set; }
        /// <summary>
        /// 条码角度
        /// </summary>
        public double R { get; set; }
        /// <summary>
        /// 中心坐标
        /// </summary>
        public Point Center { get; set; }
        /// <summary>
        /// 条码高度
        /// </summary>
        public double Height { get; set; }
        /// <summary>
        /// 条码宽度
        /// </summary>
        public double Width { get; set; }

        [Browsable(false)]
        public HROI ResultRegion { get; set; }
        public bool IsFailed { get; set; }

        public void Dispose()
        {
            ResultRegion.Dispose();
        }

        public override string ToString()
        {
            return $"Code:{Code}";
        }

    }

    public static class HBarcodeTypeHelper
    {
        public static string ToHalconString(this E_BarCodeType type)
        {
            string str = string.Empty;
            switch (type)
            {
                case E_BarCodeType.Auto:
                    return "auto";
                case E_BarCodeType.Code128:
                    return "Code 128";
                case E_BarCodeType.Code39:
                    return "Code 39";
                case E_BarCodeType.EAN13:
                    return "EAN-13";
                default:
                    return type.ToString();
            }
        }

        public static string ToHalconString(this E_2DCodeType type)
        {
            string str = string.Empty;
            switch (type)
            {
                case E_2DCodeType.DataMatrix:
                    return "Data Matrix ECC 200";
                case E_2DCodeType.QRCode:
                    return "QR Code";
                default:
                    return type.ToString();
            }
        }
    }
}
