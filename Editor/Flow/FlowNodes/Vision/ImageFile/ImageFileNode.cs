using FlowEditor.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DPLibrary_Halcon_F48.Tools.ImageFile;
using FlowEditor.Core;
using FlowNodes.Vision.Definition;
using HalconDotNet;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Vision
{
    [FlowTool("加载图像", "Image", ToolGroup = "视觉", ToolSubGroup = "ImageFile", SortIndex = 40, Description = "从本地加载图像文件")]
    internal class ImageFileNode : HVisionToolNodeBase<HTool_ImageFile>
    {
        private HObject? _outputImage;
        /// <summary>加载完成的图像（可绑定到输出端口）。</summary>
        [NodeProperty(DisplayName = "输出图像", Group = "1.基础信息", Order = 2, Description = "加载完成的图像", IsOutput = true)]
        [JsonIgnore]
        public HObject? OutputImage
        {
            get => _outputImage;
            set => SetProperty(ref _outputImage, value);
        }

        [NodeProperty(DisplayName = "输入图像", Group = "1.基础信息", Order = 2, IsDisplay = false)]
        [JsonIgnore]
        public override HObject Image { get => base.Image; set => base.Image = value; }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            EnsureRegistered();
            visionTool.Run();
            OutputImage = visionTool.Image;
            await Task.CompletedTask;
        }
    }
}
