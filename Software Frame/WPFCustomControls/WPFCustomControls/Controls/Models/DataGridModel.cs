// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：DataGridModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Collections.ObjectModel;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// DataGrid数据模型
    /// 纯粹的数据结构容器，不包含业务逻辑
    /// </summary>
    public class DataGridModel<T>
    {
        /// <summary>
        /// 数据源
        /// </summary>
        public ObservableCollection<T> DataCollection { get; } = new ObservableCollection<T>();

        /// <summary>
        /// 保留的最大数据条数
        /// </summary>
        public int RetainNum { get; set; } = 200;
    }
}
