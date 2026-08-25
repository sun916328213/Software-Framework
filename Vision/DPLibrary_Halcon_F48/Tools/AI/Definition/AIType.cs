using System.Text.Json.Serialization;

namespace DPLibrary_Halcon_F48.Tools.AI.Definition
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ToolType
    {
        Classification,
        Segmentation,
        Detection,
        
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AIBrand
    {
        Aiditron,
        Invi
    }
}
