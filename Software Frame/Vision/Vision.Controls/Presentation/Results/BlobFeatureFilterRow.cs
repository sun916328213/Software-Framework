using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vision.Controls.ViewModels;

#nullable enable
namespace Vision.Controls.Presentation;

/// <summary>结果页展示的已勾选特征行：下限/上限仍是参数页同一份数据，另附本次实测范围。</summary>
public sealed class BlobFeatureFilterRow : ObservableObject, IDisposable
{
    private readonly BlobFeatureRow _source;
    private bool _disposed;
    private double? _observedMin;
    private double? _observedMax;
    public BlobFeatureFilterRow(BlobFeatureRow source)
    {
        _source = source;
        _source.PropertyChanged += OnSourceChanged;
    }
    public string Feature => _source.Feature;
    public double Min { get => _source.Min; set { ObjectDisposedException.ThrowIf(_disposed, this); _source.Min = value; } }
    public double Max { get => _source.Max; set { ObjectDisposedException.ThrowIf(_disposed, this); _source.Max = value; } }
    public double? ObservedMin { get => _observedMin; private set => SetProperty(ref _observedMin, value); }
    public double? ObservedMax { get => _observedMax; private set => SetProperty(ref _observedMax, value); }
    internal void UpdateObserved(double? min, double? max)
    {
        ObservedMin = min; ObservedMax = max;
    }
    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BlobFeatureRow.Min)) OnPropertyChanged(nameof(Min));
        else if (e.PropertyName == nameof(BlobFeatureRow.Max)) OnPropertyChanged(nameof(Max));
    }
    public void Dispose() { _disposed = true; _source.PropertyChanged -= OnSourceChanged; }
}
