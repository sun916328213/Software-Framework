// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：CStatusBar、StatusBarModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、WPFControls.Controls.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using WPFControls.Controls.Models;

namespace WPFControls.ProgramControl.Models
{

    /// <summary>
    /// CStatusBar 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class CStatusBar
    {
        /// <summary>
        /// 当前用户信息
        /// </summary>
        public UserInfo UserInfo { get; set; }

        /// <summary>
        /// 当前时间
        /// </summary>
        public string DateTime { get; set; }
        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorInfo { get; set; }
    }
    /// <summary>
    /// StatusBarModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class StatusBarModel : ObservableObject
    {
        private CStatusBar statusBar = new CStatusBar();
        internal CStatusBar StatusBar
        {
            get => statusBar;
            set => SetProperty(ref statusBar, value);

        }
    }
}