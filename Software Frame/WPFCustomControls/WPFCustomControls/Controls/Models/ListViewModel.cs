// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：ListViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Collections.ObjectModel;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// ListViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
    /// </summary>
    public class ListViewModel
    {
        public ObservableCollection<string> DataCollection = new ObservableCollection<string>();
    }
}
