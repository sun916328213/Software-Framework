// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：IToolResult。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Core、System、System.Collections.Generic、System.ComponentModel、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using Vision.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core.Tools
{
    /// <summary>
    /// IToolResult 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public interface IToolResult:IDisposable
    {
        bool IsFailed { get; set; }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        string ToString();
    }
}
