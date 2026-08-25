using CommunityToolkit.Mvvm.ComponentModel;
using WPFControl.CustomControl.Models;

namespace WPFControl.ProgramControl.Models
{

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