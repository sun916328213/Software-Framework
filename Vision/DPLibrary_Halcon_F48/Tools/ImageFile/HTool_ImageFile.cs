using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Tools;
using DPLibrary_Halcon_F48.Tools.FindLine;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.ImageFile
{
    public class HTool_ImageFile : HVisionTool
    {
        public override E_ToolType ToolType => E_ToolType.OtherTool;

        [ToolPropertyAttribute(DisplayName = "工具名称", Group = "1.基础", Description = "用于标识当前工具的唯一名称，方便在多设备场景下区分")]
        /// <summary>
        /// 工具名称
        /// </summary>
        public override string Name { get; set; } = $"ImageFile_{ToolMge.Instance.GetToolCount<HTool_ImageFile>() + 1}";


        [ToolPropertyAttribute(Group = "1.基础", DisplayName = "图像路径", Description = "图像路径")]
        public string  ImagePath { get; set; }  

        public override void Config()
        {
           
        }

        public override void Pause()
        {
            
        }

        public override void Reset()
        {
            
        }

        public override void Run()
        {
            if(string .IsNullOrEmpty(ImagePath))
            { 
                MessageBox.Show("请选择图像路径");
                return; 
            }
            HOperatorSet.ReadImage(out HObject image, ImagePath);
            Image = image;
        }
    }
}
