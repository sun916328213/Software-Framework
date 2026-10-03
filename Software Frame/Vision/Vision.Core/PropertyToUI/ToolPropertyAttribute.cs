// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：IItemsSourceProvider、ButtonAction、ToolPropertyAttribute。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using System.Text.Json.Serialization;

namespace Vision.Core
{

    /// <summary>
    /// IItemsSourceProvider 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public interface IItemsSourceProvider
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetItems。
        /// </summary>
        System.Collections.IEnumerable GetItems(object target); 
    }

    /// <summary>
    /// ButtonAction 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public class ButtonAction
    {
        [JsonIgnore] public Action<object, EventArgs> OnBtClick { get; set; }
        [JsonIgnore] public Action<object, EventArgs> OnBtDown { get; set; }
        [JsonIgnore] public Action<object, EventArgs> OnBtUp { get; set; }

    }


    /// <summary>
    /// ToolPropertyAttribute 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：Attribute。具体约束按这些契约及各成员实现解释。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ToolPropertyAttribute : Attribute
    {
        /// <summary>
        /// 显示的名称
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// 单元的类型
        /// </summary>
        public E_PropertyEditorType EditorType { get; set; }
        /// <summary>
        /// 组别
        /// </summary>
        public string Group { get; set; }
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        #region 用于List进行展示来源
        /// <summary>
        /// 描述？可通过枚举自动生成
        /// </summary>
        public object[] Options { get; set; }

        /// <summary>
        /// List下拉的来源绑定提供者，全名类型
        /// </summary>
        public Type ListItemsSourceType{ get; set; }
        public string ListItemsSourceName{ get; set; }
        #endregion
 
        /// <summary>
        /// 最小值
        /// </summary>
        public double Min { get; set; } = double.NaN;
        /// <summary>
        /// 最大值
        /// </summary>
        public double Max { get; set; } = double.NaN;
        /// <summary>
        /// 是否显示
        /// </summary>
        public bool IsDisplay { get; set; } = true;
        
        /// <summary>
        /// 步长，不设置的话整数默认1，小数默认0.001
        /// </summary>
        public double Step { get; set; }
        /// <summary>
        /// 顺序，默认 0，最靠前
        /// </summary>
        public int Order { get; set; } = 0;

        #region 用于按钮类属性，绑定事件
        public string OnClick { get; set; } = string.Empty;

        #endregion


        #region 用于展示类列表详细元素时的相关参数
        /// <summary>
        /// 显示用于绑定的属性，只使用与列表
        /// </summary>
        public string BindingProp { get; set; } = ".";
        /// <summary>
        /// 显示用于控制缩放的属性，只使用与列表
        /// </summary>
        public string BindingIsHit { get; set; } = string.Empty;
        #endregion
    }
}
