using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vision.Core;
using Cv = Vision.Tools.OpenCv.Inspection;
using Halcon = Vision.Tools.Halcon.Inspection;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>复用原参数面板的 Blob 编辑会话；不拥有工具，不改变算法 DTO/JSON 契约。</summary>
public sealed class BlobParametersEditorViewModel : ObservableObject, IDisposable
{
    private readonly Action<int> _setMinGray;
    private readonly Action<int> _setMaxGray;
    private readonly Action<int> _setMinArea;
    private readonly Action<BlobFilterRelation> _setRelation;
    private readonly Action<IReadOnlyList<BlobFeatureRow>> _setFeatures;
    private readonly Action _changed;
    private readonly ObservableCollection<BlobFeatureRow> _ranges;
    private bool _disposed;
    private int _minGray;
    private int _maxGray;
    private int _minArea;
    private BlobFilterRelation _relation;

    private BlobParametersEditorViewModel(int minGray, int maxGray, int minArea, BlobFilterRelation relation,
        IEnumerable<BlobFeatureRow> ranges, string[] supportedFeatures,
        Action<int> setMinGray, Action<int> setMaxGray, Action<int> setMinArea, Action<BlobFilterRelation> setRelation,
        Action<IReadOnlyList<BlobFeatureRow>> setFeatures, Action changed)
    {
        _minGray = minGray;
        _maxGray = maxGray;
        _minArea = minArea;
        _relation = relation;
        _setMinGray = setMinGray;
        _setMaxGray = setMaxGray;
        _setMinArea = setMinArea;
        _setRelation = setRelation;
        _setFeatures = setFeatures;
        _changed = changed;
        _ranges = new(ranges);
        Ranges = new(_ranges);
        // 保留已加载的未知特征及重复范围，不在打开编辑器时悄悄改配置。
        AvailableFeatures = supportedFeatures.Concat(_ranges.Select(r => r.Feature)).Distinct().ToArray();
        SelectedFeatures = new(_ranges.Select(r => r.Feature).Distinct());
        foreach (var row in _ranges) row.PropertyChanged += OnRangeChanged;
        SelectedFeatures.CollectionChanged += OnSelectionChanged;
    }

    public static BlobParametersEditorViewModel? TryCreate(object? parameters, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        return parameters switch
        {
            Cv.BlobParameters p when p.MinGray is >= 0 and <= 255 && p.MaxGray is >= 0 and <= 255 && p.MinArea >= 0 &&
                p.Features != null && p.Features.All(f => f != null && f.Feature != null) => new(
                p.MinGray, p.MaxGray, p.MinArea, (BlobFilterRelation)p.Logic,
                p.Features.Select(f => new BlobFeatureRow(f.Feature, f.Min, f.Max)),
                [.. Cv.BlobTool.SupportedFeatures],
                value => p.MinGray = value, value => p.MaxGray = value, value => p.MinArea = value,
                value => p.Logic = (Cv.BlobFeatureLogic)value,
                rows => p.Features = rows.Select(r => new Cv.BlobFeatureRange { Feature = r.Feature, Min = r.Min, Max = r.Max }).ToList(), changed),
            Halcon.BlobParameters p when p.MinGray is >= 0 and <= 255 && p.MaxGray is >= 0 and <= 255 && p.MinArea >= 0 &&
                p.Features != null && p.Features.All(f => f != null && f.Feature != null) => new(
                p.MinGray, p.MaxGray, p.MinArea, (BlobFilterRelation)p.Logic,
                p.Features.Select(f => new BlobFeatureRow(f.Feature, f.Min, f.Max)),
                // 两个 Blob 后端的多选项保持一致，避免界面能力不一致。
                [.. Cv.BlobTool.SupportedFeatures],
                value => p.MinGray = value, value => p.MaxGray = value, value => p.MinArea = value,
                value => p.Logic = (Halcon.BlobFeatureLogic)value,
                rows => p.Features = rows.Select(r => new Halcon.BlobFeatureRange { Feature = r.Feature, Min = r.Min, Max = r.Max }).ToList(), changed),
            _ => null // 未迁移工具或损坏的集合保留原参数入口，由后端报告错误。
        };
    }

    [ToolProperty(DisplayName = "最小灰度值", Group = "1.灰度分割", Description = "前景灰度下限，包含边界；范围 0～255。", EditorType = E_PropertyEditorType.Int, Min = 0, Max = 255, Step = 1, Order = 0)]
    public int MinGray
    {
        get => _minGray;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (value is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(value), "灰度必须在 0～255 内。");
            if (_minGray == value) return;
            _setMinGray(value);
            SetProperty(ref _minGray, value);
            _changed();
        }
    }

    [ToolProperty(DisplayName = "最大灰度值", Group = "1.灰度分割", Description = "前景灰度上限，包含边界；必须不小于最小灰度值。", EditorType = E_PropertyEditorType.Int, Min = 0, Max = 255, Step = 1, Order = 1)]
    public int MaxGray
    {
        get => _maxGray;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (value is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(value), "灰度必须在 0～255 内。");
            if (_maxGray == value) return;
            _setMaxGray(value);
            SetProperty(ref _maxGray, value);
            _changed();
        }
    }

    [ToolProperty(DisplayName = "最小面积", Group = "1.灰度分割", Description = "连通域像素面积下限，小于该值的区域在特征筛选前直接丢弃；0 表示不限制。", EditorType = E_PropertyEditorType.Int, Min = 0, Step = 1, Order = 2)]
    public int MinArea
    {
        get => _minArea;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "最小面积不能为负数。");
            if (_minArea == value) return;
            _setMinArea(value);
            SetProperty(ref _minArea, value);
            _changed();
        }
    }

    [ToolProperty(DisplayName = "参数关系", Group = "2.特征筛选", Order = 0, Description = "And：全部范围同时满足；Or：满足任意一个范围。", EditorType = E_PropertyEditorType.SingleSelect)]
    public BlobFilterRelation Relation
    {
        get => _relation;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_relation == value) return;
            _setRelation(value);
            SetProperty(ref _relation, value);
            _changed();
        }
    }

    public IReadOnlyList<string> AvailableFeatures { get; }
    [Browsable(false)]
    public bool HasSelectedFeatures => SelectedFeatures.Count > 0;
    [ToolProperty(DisplayName = "特征多选", Group = "2.特征筛选", Order = 1,
        EditorType = E_PropertyEditorType.MultiSelect, IsContentEditable = true, ListItemsSourceName = nameof(AvailableFeatures),
        Description = "展开下拉列表勾选参与筛选的特征；勾选后在下表生成范围行，取消勾选即移除。未勾选的特征不参与筛选。")]
    public ObservableCollection<string> SelectedFeatures { get; }
    [ToolProperty(DisplayName = "范围表格", Group = "2.特征筛选", Order = 2,
        EditorType = E_PropertyEditorType.DataGridList, IsContentEditable = true,
        IsVisibleProperty = nameof(HasSelectedFeatures),
        Description = "每个已勾选特征对应一行；是否同时满足全部范围由“参数关系”决定。OpenCV 的 phi 为有符号方向角，单位为弧度；HALCON 特征口径以实际 SDK 为准。")]
    public ReadOnlyObservableCollection<BlobFeatureRow> Ranges { get; }

    private void OnSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_disposed) return;
        foreach (var row in _ranges.Where(r => !SelectedFeatures.Contains(r.Feature)).ToArray())
        {
            row.PropertyChanged -= OnRangeChanged;
            _ranges.Remove(row);
        }
        foreach (var feature in SelectedFeatures.Distinct())
        {
            if (_ranges.Any(r => r.Feature == feature)) continue;
            // phi 为有符号方向角；新选时不应默认排除负角，已有配方范围保持原值。
            var row = new BlobFeatureRow(feature, feature == "phi" ? -99999 : 0, 99999);
            row.PropertyChanged += OnRangeChanged;
            _ranges.Add(row);
        }
        CommitFeatures();
        OnPropertyChanged(nameof(HasSelectedFeatures));
    }
    private void OnRangeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed) CommitFeatures();
    }
    private void CommitFeatures()
    {
        _setFeatures(_ranges);
        _changed();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SelectedFeatures.CollectionChanged -= OnSelectionChanged;
        foreach (var row in _ranges) row.PropertyChanged -= OnRangeChanged;
    }
}
