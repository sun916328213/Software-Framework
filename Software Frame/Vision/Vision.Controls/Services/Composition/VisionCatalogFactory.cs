#nullable enable
using System.IO;
using Vision.Controls.Models;
using Vision.Controls.Catalog;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Imaging;

namespace Vision.Controls;

/// <summary>视觉流程的默认目录及桌面文件交互；不创建节点、不加载模型或连接设备。</summary>
public static class VisionCatalogFactory
{
    public static VisionBrowserWorkspace CreateWorkspace() => new(CreateCatalog());

    public static VisionBrowserCatalog CreateCatalog()
    {
        var providers = AIProviderCatalog.ProviderNames.Select(name =>
        {
            IAIProvider? provider = null;
            try { provider = AIProviderCatalog.Create(name); return new ProviderEntry(name, provider.IsAvailable); }
            catch { return new ProviderEntry(name, false); }
            finally { (provider as IDisposable)?.Dispose(); }
        }).ToArray();
        var tools = Vision.Controls.Catalog.HalconToolCatalog.Entries.Concat(Vision.Controls.Catalog.OpenCvToolCatalog.Entries)
            .Concat(Vision.Controls.Catalog.AIToolCatalog.Entries).Concat(Vision.Controls.Catalog.CommonToolCatalog.Entries).ToArray();
        return new VisionBrowserCatalog(tools, providers,
            AIProviderCatalog.Create, LoadImageFile, PickTemplateModelPath,
            PickProjectPath, Vision.Tools.OpenCv.Acquisition.VisionImageFileLoader.Load);
    }

    private static string? PickTemplateModelPath(string filter, bool save)
    {
        Microsoft.Win32.FileDialog dialog = save
            ? new Microsoft.Win32.SaveFileDialog { OverwritePrompt = true, Title = "保存模板/模型", Filter = filter }
            : new Microsoft.Win32.OpenFileDialog { CheckFileExists = true, Title = "加载模板/模型", Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PickProjectPath(bool save)
    {
        const string filter = "Vision 工程 (*.visionproj)|*.visionproj|JSON (*.json)|*.json|全部 (*.*)|*.*";
        Microsoft.Win32.FileDialog dialog = save
            ? new Microsoft.Win32.SaveFileDialog { OverwritePrompt = true, Title = "另存工程包（包含当前输入图像）", Filter = "Vision 工程包 (*.visionproj)|*.visionproj", DefaultExt = ".visionproj", AddExtension = true }
            : new Microsoft.Win32.OpenFileDialog { CheckFileExists = true, Title = "导入工程", Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static VisionImage? LoadImageFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择图像", Filter = "图像|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|全部|*.*" };
        return dialog.ShowDialog() == true ? Vision.Tools.OpenCv.Acquisition.VisionImageFileLoader.Load(dialog.FileName) : null;
    }

}
