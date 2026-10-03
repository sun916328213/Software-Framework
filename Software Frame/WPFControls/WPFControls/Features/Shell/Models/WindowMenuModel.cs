// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：WindowMenuModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、WPFControls.Controls.Models、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using WPFControls.Controls.Models;
using Extension.Core.Conversion;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// 窗口菜单模型
    /// </summary>
    public class WindowMenuModel : ObservableObject
    {
        string iTitle;
        /// <summary>
        /// 应用程序标题
        /// </summary>
        public string Title
        {
            get => iTitle;
            set => SetProperty(ref iTitle, value);
        }


        bool iIsMaximized = false;
        bool iIsMinimized = false;

        /// <summary>
        /// 是否最大化
        /// </summary>
        public bool IsMaximized
        {
            get => iIsMaximized;
            set => SetProperty(ref iIsMaximized, value);
        }


        /// <summary>
        /// 是否最小化
        /// </summary>
        public bool IsMinimized
        {
            get => iIsMinimized;
            set => SetProperty(ref iIsMinimized, value);
        }
    }
}
