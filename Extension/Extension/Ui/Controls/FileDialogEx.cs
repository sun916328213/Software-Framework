

using Microsoft.Win32;

namespace Extension.UI.Controls
{
    public static  class FileDialogEx
    {
        static OpenFileDialog  _OpenFileDialog = new OpenFileDialog();
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
