// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：QuerySave、Message。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Extension.UI.Controls、WPFCustomControls.Window.Views。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Extension.UI.Controls;
using WPFCustomControls.Window.Views;

namespace WPFControls.Method
{
    /// <summary>
    /// QuerySave 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class QuerySave
    {
        /// <summary>
        /// Message 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
        /// </summary>
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
            finally
            {
                fileLoading.Close();
            }
        }
    }
}
