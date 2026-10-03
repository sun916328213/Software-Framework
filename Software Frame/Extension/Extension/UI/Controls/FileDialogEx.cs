// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：FileDialogEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Microsoft.Win32。导入不代表所有类型都会在运行时加载。


using Microsoft.Win32;

namespace Extension.UI.Controls
{
    /// <summary>
    /// FileDialogEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public static  class FileDialogEx
    {
        static OpenFileDialog  _OpenFileDialog = new OpenFileDialog();
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 OpenFile。
        /// 正文中的关键协作调用包括 ShowDialog；具体条件和先后次序见方法体。
        /// </summary>
        public static string OpenFile()
        {
            _OpenFileDialog.FileName = "";
            var ret = _OpenFileDialog.ShowDialog();
            if (ret == true)
                return _OpenFileDialog.FileName;
            else
                return null;
        }

        static SaveFileDialog _SaveFileDialog = new SaveFileDialog();
        /// <summary>
        /// 执行保存操作；对应入口为 SaveFile。
        /// 正文中的关键协作调用包括 ShowDialog；具体条件和先后次序见方法体。
        /// </summary>
        public static string SaveFile(string FileName="")
        {
            _SaveFileDialog.FileName = FileName;
            //_SaveFileDialog.Filter = " 所有文件 (*.*)|*.*";
            var ret = _SaveFileDialog.ShowDialog();
            if (ret == true)
                return _SaveFileDialog.FileName;
            else
                return null;
        }
    }
}
