using CommunityToolkit.Mvvm.ComponentModel;
using WPFControl.CustomControl.Models;
using Extension.Core.ConvertEx;

namespace WPFControl.ProgramControl.Models
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
