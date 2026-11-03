using System.ComponentModel;

namespace Vision.Controls.ViewModels;

public enum BlobFilterRelation
{
    [Description("全部范围同时满足")] And,
    [Description("满足任意一个范围")] Or
}
