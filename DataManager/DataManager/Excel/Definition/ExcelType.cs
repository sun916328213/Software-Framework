using System.Text.Json.Serialization;

namespace DataManager.Excel.Definition
{
    /// <summary>
    /// Excel工具类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExcelType
    {
        EPPlus,
        MiniExcel,
        NPOI,
    }
}