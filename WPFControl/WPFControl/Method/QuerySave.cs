using Extension.UI.Controls;
using WPFCustomControls.Window.Views;

namespace WPFControl.Method
{
    public class QuerySave
    {
        private static class Message
        {
            public static readonly string[] 文件保存中 = { "文件保存中", "File saving in progress" };
            public static readonly string[] 提示 = { "提示", "Title" };
            public static readonly string[] 未选择存储路径请选择 = { "未选择存储路径,请选择", "No storage path selected, please select" };
          
        }

        /// <summary>
        /// 查询保存CSV
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="datas"></param>
        /// <returns></returns>
        public async Task QuerySaveCSVAsync<T>(IEnumerable<T> datas)
        {
            FileLoading fileLoading = new FileLoading();
            try
            {
                var ret = FileDialogEx.SaveFile();
                if (ret != null)
                {
                    fileLoading.Text = LangSwitchEx.GetMessage(Message.文件保存中)+ "......";
                    fileLoading.Show();
                    string filter = ".csv";
                    if (!ret.Contains(filter)) ret += filter;
                    await Extension.IO.Csv.CSVEx.ExportToCsvAsync(datas, ret);
                    await Task.Delay(500);
                    fileLoading.Close();
                }
                else
                {
                    MessagePrompt messagePrompt = new MessagePrompt(LangSwitchEx.GetMessage(Message.提示) , LangSwitchEx.GetMessage(Message.未选择存储路径请选择));
                    messagePrompt.Visibility_Cancel = System.Windows.Visibility.Hidden;
                    messagePrompt.Show();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                fileLoading.Close();
            }
        }
    }
}
