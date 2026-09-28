// ─────────────────────────────────────────────────────────────────────────────
// P3 —— ImageFileTool（Acquisition）。
// 迁移源：DPLibrary_Halcon_F48/Tools/ImageFile/HTool_ImageFile.cs（文件采图）。
//
// P3 只做标准化搬运：参数（路径）/ 结果（宽高 + VisionImage 元信息）/ 校验（路径非空、
// 扩展名白名单）/ 执行（ReadImage + GetImageSize）。像素回传走 VisionImage？
// 完整像素拷贝放在 P4（OpenCV 对比需要统一的像素语义）；P3 结果先给尺寸与来源，
// 满足“结果不再只暴露 HTuple/HObject”，同时不把大像素缓冲塞进回归基线。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Acquisition;

/// <summary>文件采图参数。</summary>
public sealed class ImageFileParameters
{
    /// <summary>图像文件路径。</summary>
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>文件采图结果。</summary>
public sealed class ImageFileResult : ToolResult
{
    /// <summary>图像宽度（像素）。</summary>
    public int Width { get; set; }

    /// <summary>图像高度（像素）。</summary>
    public int Height { get; set; }

    /// <summary>实际加载的路径。</summary>
    public string LoadedPath { get; set; } = string.Empty;
}

/// <summary>
/// 文件采图工具（Acquisition）：Workbench Image Loader 的后端之一。
/// </summary>
public sealed class ImageFileTool : HalconToolBase<ImageFileParameters, ImageFileResult>
{
    private static readonly string[] SupportedExtensions =
        [".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff"];

    /// <inheritdoc/>
    public override string Name => "ImageFile";

    /// <inheritdoc/>
    protected override ValidationResult ValidateCore(ImageFileParameters parameters)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(parameters.FilePath))
        {
            result.AddError("PATH_EMPTY", "图像路径为空。", nameof(ImageFileParameters.FilePath));
            return result;
        }

        var extension = Path.GetExtension(parameters.FilePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
            result.AddWarning("EXT_UNKNOWN", $"扩展名 {extension} 不在常见列表内，仍尝试加载。", nameof(ImageFileParameters.FilePath));
        return result;
    }

    /// <inheritdoc/>
    protected override ImageFileResult RunCore(HObject? image, ImageFileParameters parameters)
    {
        if (!File.Exists(parameters.FilePath))
            throw new FileNotFoundException($"图像文件不存在：{parameters.FilePath}", parameters.FilePath);

        HObject? loaded = null;
        try
        {
            HOperatorSet.ReadImage(out loaded, parameters.FilePath);
            var (width, height) = HalconImageConverter.GetSize(loaded);
            return new ImageFileResult
            {
                Width = width,
                Height = height,
                LoadedPath = parameters.FilePath,
            };
        }
        finally
        {
            loaded?.Dispose();
        }
    }
}
