// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：E_PropertyEditorType。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    /// <summary>
    /// E_PropertyEditorType 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public enum E_PropertyEditorType
    {
        /// <summary>
        /// 字符串
        /// </summary>
        Text,
        /// <summary>
        /// 整形数据
        /// </summary>
        Int,
        /// <summary>
        /// 浮点形数据
        /// </summary>
        Double,
        /// <summary>
        /// 单选
        /// </summary>
        SingleSelect,
        /// <summary>
        /// 多选
        /// </summary>
        MultiSelect,
        /// <summary>
        /// 选择
        /// </summary>
        Checkbox,
        /// <summary>
        /// 用于命令操作的按钮，类中需要实现处理动作
        /// </summary>
        Button,
        /// <summary>
        /// 用于列表展示的数据
        /// </summary>
        DataGridList,
        /// <summary>
        /// 用于列表展示带数据的集合类
        /// </summary>
        DataClassList,
    }
}
