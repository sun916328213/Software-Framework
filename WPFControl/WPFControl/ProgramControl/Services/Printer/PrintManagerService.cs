using Device.PrintSoftware;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.Platform.Diagnostics;
using Extension.Platform.Native;
using Extension.Platform.Memory;
using Extension.Platform.Processes;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using WPFCustomControls.Window.Views;
using Prism.Ioc;
using WPFControl.CustomControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class PrintManagerService
    {
        internal PrintManagerModel Model { get; set; } = new PrintManagerModel();

        private IPrintSoftware PrintSoftware { get; set; }
        public PrintManagerService(IContainerProvider container)
        {
            PrintSoftware= container.Resolve<IPrintSoftware>();
        }

        /// <summary>
        /// 打印标签
        /// </summary>
        /// <param name="pIndex">标签列表Index</param>
        /// <returns></returns>
        public async Task Print(int pIndex)
        {
           await TryCatchHelper.TryExecuteAsync(async () =>
            {
                if(Model.PrintInfos.Count==0)
                    throw new Exception("PrintSoftware is null!!");

               await PrintLabel(Model.PrintInfos[pIndex].PrintInfo);
 
            });
        }

        /// <summary>
        /// 获取打印信息
        /// </summary>
        /// <param name="pIndex"></param>
        /// <returns></returns>
        public PrintInfo GetPrintInfo(int pIndex)
        {
            return TryCatchHelper.TryExecute(() =>
              {
                  return Model.PrintInfos[pIndex].PrintInfo;
              });
        }

        public void Close()
        {
            TryCatchHelper.TryExecute(() =>
            {
                ProcessEx.CloseProcess("BarTender");
            });
        }

        /// <summary>
        /// 添加标签打印列表
        /// </summary>
        /// <param name="print">标签信息</param>
        /// <returns></returns>
        public void Add(PrintInfo print)
        {
            TryCatchHelper.TryExecute(() =>
            {
                if (print == null)
                {
                    MessagePrompt messagePrompt = new MessagePrompt();
                    messagePrompt.Title = "提示";
                    messagePrompt.Message = "请设置后再进行添加!!";
                    messagePrompt.Visibility_Cancel = Visibility.Hidden;
                    messagePrompt.Show();
                    return;
                }
 
                Model.PrintInfos.Add(new PrintInfoManager { PrintInfo = print.DeepClone() });
            });
        }

        /// <summary>
        /// 修改标签打印列表
        /// </summary>
        /// <param name="pIndex">标签信息Index</param>
        /// <param name="pInfo">标签信息</param>
        /// <returns></returns>
        public void Modify(int pIndex, PrintInfoManager pInfo)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.PrintInfos[pIndex] = pInfo;
            });
        }

        /// <summary>
        /// 清除标签列表
        /// </summary>
        /// <returns></returns>
        public void RemoveAt()
        {
            TryCatchHelper.TryExecute(() =>
            {
                if (Model.SelectPrintIndex == -1)
                {
                    MessagePrompt messagePrompt = new MessagePrompt();
                    messagePrompt.Title = "提示";
                    messagePrompt.Message = "请先选择后再进行操作!!";
                    messagePrompt.Visibility_Cancel = Visibility.Hidden;
                    messagePrompt.Show();
                    return;
                }

                Model.PrintInfos.RemoveAt(Model.SelectPrintIndex);
            });
        }

        /// <summary>
        /// 清除标签所有列表
        /// </summary>
        /// <returns></returns>
        public void RemoveAll()
        {
            TryCatchHelper.TryExecute(() =>
            {
                    MessagePrompt messagePrompt = new MessagePrompt();
                    messagePrompt.Title = "提示";
                    messagePrompt.Message = "请确认是否全部删除!!";
                    var ret=messagePrompt.ShowDialog();

                if (ret == true)
                {
                    int count = Model.PrintInfos.Count;
                    for (int index = 0; index < count; index++)
                        Model.PrintInfos.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// 标签打印列表顺序上移
        /// </summary>
        /// <returns></returns>
        public void Up()
        {
            TryCatchHelper.TryExecute(() =>
            {
                int pIndex = Model.SelectPrintIndex;
                var data = Model.PrintInfos[pIndex];
                pIndex = pIndex - 1;
                if (pIndex < 0) pIndex = 0;
                Model.PrintInfos.Remove(data);
                Model.PrintInfos.Insert(pIndex, data);
            });
        }

        /// <summary>
        /// 标签打印列表顺序下移
        /// </summary>
        /// <returns></returns>

        public void Down()
        {
            TryCatchHelper.TryExecute(() =>
            {
                int pIndex = Model.SelectPrintIndex;
                var data = Model.PrintInfos[pIndex];
                pIndex = pIndex + 1;
                if (pIndex >= Model.PrintInfos.Count - 1) pIndex = Model.PrintInfos.Count - 1;
                Model.PrintInfos.Remove(data);
                Model.PrintInfos.Insert(pIndex, data);
            });
        }

        public void TestPrint()
        {
            TryCatchHelper.TryExecute( async () =>
            {
                MessagePrompt messagePrompt = new MessagePrompt("提示", "请确实是否打印");
                var ret = messagePrompt.ShowDialog();
                if (ret == false) return;

                foreach (var item in Model.PrintInfos)
                {
                  await  PrintLabel(item.PrintInfo);
                }
                   
            });
        }

        private async  Task  PrintLabel(PrintInfo print)
        {
            await Task.Run(() =>
             {
                 PrintSoftware.Open(print.LabelPath);
                 PrintSoftware.Print();
                 PrintSoftware.Close();
             });
        }

        /// <summary>
        /// 根据路径打印标签
        /// </summary>
        /// <param name="labelPath"></param>
        public async Task PrintLabel_Path(string labelPath, string printerName)
        {
            await Task.Run(() =>
            {
                
                PrintSoftware.Open(labelPath);
                PrintSoftware.PrintName = printerName;
                PrintSoftware.Print();
                PrintSoftware.Close();
            });
        }
    }
}
