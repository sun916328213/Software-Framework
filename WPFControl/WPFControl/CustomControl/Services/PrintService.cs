using Device.Printer;
using Device.PrintSoftware;
using Extension.Core.ControlEx;
using Extension.Core.DataEx;
using Extension.Core.ImageEx;
using Extension.Core.SystemEx;
using Extension.Struct;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.CustomControl.Models;
using WPFControl.Helper;
using WPFCustomControls.Window.Views;

namespace WPFControl.CustomControl.Services
{
    internal class PrintService
    {
        /// <summary>
        /// 打印数据模型
        /// </summary>
        internal PrintModel Model = new PrintModel();
 
        /// <summary>
        /// 打印管理类
        /// </summary>
        internal IPrintSoftware   printManager;

        public PrintService(IContainerProvider container)
        {
            printManager = container.Resolve<IPrintSoftware>();
            // 在UI线程更新Model.PrintNames
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Model.PrintNames = new ObservableCollection<string>(printManager.Printers);
            }));
        }
      

        /// <summary>
        /// 打开标签文件
        /// </summary>
        public async Task OpenFile()
        {
            await TryCatchHelper.TryExecuteAsync(async () =>
            {
                Model.PrintInfo = null;
                Model.LabelFormat = FileDialogEx.OpenFile();
                if (Model.LabelFormat != null)
                {
                    FileLoading fileLoading = new FileLoading();
                    fileLoading.Show();
                    await printManager.OpenAsync(Model.LabelFormat);

                    fileLoading.Close();

                    var bitmap =   printManager.Preview();

                    Model.LabelImage = bitmap.ToImageSource();
                    var keys = printManager.GetValue();
                    Model.LabelField = new ObservableCollection<KeyValue>(keys );
                }
                else
                {
                    MessagePrompt messagePrompt = new MessagePrompt("提示", "未选择存储路径，请选择！！");
                    messagePrompt.Visibility_Cancel = System.Windows.Visibility.Hidden;
                    messagePrompt.Show();
                }
            });
        }

        /// <summary>
        /// 获取标签Key
        /// </summary>
        public void GetKey()
        {
            TryCatchHelper.TryExecute(() =>
            {
                var keys = printManager.GetValue();
                if (keys == null) return;
                Model.LabelField = new ObservableCollection<KeyValue>(keys);
            });
        }

        /// <summary>
        /// 设置Value
        /// </summary>
        public void SetValue()
        {
            TryCatchHelper.TryExecute(() =>
          {
              if (printManager.PrintName == null)
              {
                  MessagePrompt messagePrompt = new MessagePrompt("提示", "未设置打印机！！");
                  messagePrompt.Visibility_Cancel = System.Windows.Visibility.Hidden;
                  messagePrompt.Show();
                  return;
              }
              Model.PrintInfo = new PrintInfo()
              {
                  LabelFieldInfo = Model.LabelField.ToList(),
                  LabelName = printManager.LabelName,
                  LabelPath = printManager.LabelPath,
                  PrintName = printManager.PrintName,
              };

              printManager.SetValue(Model.PrintInfo.LabelFieldInfo);
              Bitmap bitmap = printManager.Preview();
              Model.LabelImage = bitmap.ToImageSource();
          });
        }

        /// <summary>
        /// 打印
        /// </summary>
        public async Task Print()
        {
            var d1 = await TryCatchHelper.TryExecuteWithResultAsync(async () =>
              {
                  await printManager.PrintAsync();
              });
            if (d1 == false)
            {
                Action action = () =>
                {
                    MessagePrompt messagePrompt = new MessagePrompt("提示", "打印失败，请检测参数！！");
                    messagePrompt.Visibility_Cancel = System.Windows.Visibility.Hidden;
                    messagePrompt.Show();
                };
                action.UIInvoke();
            }
        }

        /// <summary>
        /// 设置打印机名称
        /// </summary>
        /// <param name="name"></param>
        public void SetPrinterName(string name)
        {
            TryCatchHelper.TryExecute(() =>
            {
                printManager.PrintName = name;
            });
        }
    }
}