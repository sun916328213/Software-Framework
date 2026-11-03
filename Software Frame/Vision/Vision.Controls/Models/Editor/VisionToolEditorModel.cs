using CommunityToolkit.Mvvm.ComponentModel;
using Vision.Controls.ViewModels;

#nullable enable
namespace Vision.Controls.Models;

/// <summary>单工具调试状态；不包含工具目录、工程路径或文件存储。</summary>
public partial class VisionToolEditorModel : ObservableObject
{
    [ObservableProperty] private string _toolIdentity = string.Empty;
    public IReadOnlyList<ProviderEntry> ProviderEntries { get; init; } = [];
    [ObservableProperty] private Vision.Core.Imaging.VisionImage? _inputImage;
    [ObservableProperty] private bool _hasImage;
    partial void OnInputImageChanged(Vision.Core.Imaging.VisionImage? value) => HasImage = value != null;
    [ObservableProperty] private bool _isClosed;
    [ObservableProperty] private global::Vision.Core.Roi.Roi? _executionSearchRoi;
    [ObservableProperty] private bool _supportsProvider;
    [ObservableProperty] private bool _supportsTemplateInput;
    partial void OnSupportsTemplateInputChanged(bool value)
    {
        if (!value && SelectedImageView == ImageViewKind.Training) SelectedImageView = ImageViewKind.Input;
    }
    [ObservableProperty] private bool _supportsCropRegion;
    [ObservableProperty] private TemplateEditorViewModel? _templateEditor;
    [ObservableProperty] private bool _supportsModelLoading;
    // 共享页面的显示偏好，不是算法参数或参数 JSON 的一部分。
    [ObservableProperty] private ImageViewKind _selectedImageView = ImageViewKind.Input;
    [ObservableProperty] private bool _showOutputImage;
    [ObservableProperty] private bool _showRoiImage;
    partial void OnShowOutputImageChanged(bool value) { if (value) ShowRoiImage = false; }
    partial void OnShowRoiImageChanged(bool value) { if (value) ShowOutputImage = false; }
    [ObservableProperty] private bool _showRois;
    [ObservableProperty] private bool _showResults;
    partial void OnSelectedImageViewChanged(ImageViewKind value)
    {
        // ROI 预设用于对照原图调整搜索域，不能把底图换成阈值等算法中间图；
        // 宿主仍可通过底图命令显式选择算法中间图。
        ShowRoiImage = false;
        ShowOutputImage = value == ImageViewKind.Result;
        ShowRois = value == ImageViewKind.Roi;
        ShowResults = value == ImageViewKind.Result;
    }
    [ObservableProperty] private bool _showResultBounds;
    [ObservableProperty] private bool _showResultCenters;
    [ObservableProperty] private ToolCardViewModel? _currentCard;
    [ObservableProperty] private object? _currentParameters;
    [ObservableProperty] private BlobParametersEditorViewModel? _blobEditor;
    [ObservableProperty] private string _selectedProviderName = string.Empty;
    [ObservableProperty] private string _imageInfoText = "无图像（请加载图像文件）";

}
