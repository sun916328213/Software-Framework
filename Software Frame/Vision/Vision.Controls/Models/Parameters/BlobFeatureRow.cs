using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Vision.Controls.ViewModels;

/// <summary>范围行只持有编辑值；由工作空间拥有的编辑会话提交到原参数对象。</summary>
public sealed class BlobFeatureRow(string feature, double min, double max) : ObservableObject
{
    [DisplayName("特征")]
    public string Feature { get; } = feature;
    private double _min = min;
    private double _max = max;
    [DisplayName("下限")]
    public double Min { get => _min; set => SetProperty(ref _min, value); }
    [DisplayName("上限")]
    public double Max { get => _max; set => SetProperty(ref _max, value); }
}
