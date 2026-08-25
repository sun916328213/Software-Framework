using System.Text.Json.Serialization;

namespace DPLibrary_Base_F48
{

    public interface IItemsSourceProvider
    {
        System.Collections.IEnumerable GetItems(object target); 
    }

    public class ButtonAction
    {
        [JsonIgnore] public Action<object, EventArgs> OnBtClick { get; set; }
        [JsonIgnore] public Action<object, EventArgs> OnBtDown { get; set; }
        [JsonIgnore] public Action<object, EventArgs> OnBtUp { get; set; }

    }


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
