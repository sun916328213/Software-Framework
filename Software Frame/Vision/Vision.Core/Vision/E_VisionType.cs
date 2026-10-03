// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：E_VisionType。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core.Vision
{
    /// <summary>
    /// E_VisionType 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public enum E_VisionType
    {
        Halcon,
        VisionPro,
        OpenCV
    }
}
