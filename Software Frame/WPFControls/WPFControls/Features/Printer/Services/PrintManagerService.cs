// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PrintManagerService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Device.Core.LabelPrinting、Device.Controls.Services、Extension.Core.Buffers、Extension.IO.Compression、Extension.Integration.Cloning。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Device.Core.LabelPrinting;
using Device.Controls.Services;
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
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFCustomControls.Window.Views;
using Prism.Ioc;
using WPFControls.Controls.Models;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// PrintManagerService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class PrintManagerService
    {
        private readonly TryCatchHelper _errors;

        internal PrintManagerModel Model { get; set; } = new PrintManagerModel();

        private readonly ILabelJobService _jobs;
        private readonly LabelPrintWorkspace _workspace;
        private readonly CancellationTokenSource _shutdown = new();
        /// <summary>
        /// 初始化 PrintManagerService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public PrintManagerService(ILabelJobService jobs, LabelPrintWorkspace workspace, TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            _jobs = jobs;
            _workspace = workspace;
        }

        /// <summary>
        /// 打印标签
        /// </summary>
        /// <param name="pIndex">标签列表Index</param>
        /// <returns></returns>
        public async Task Print(int pIndex)
        {
           await _errors.TryExecuteAsync(async () =>
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
            return _errors.TryExecute(() =>
              {
                  return Model.PrintInfos[pIndex].PrintInfo;
              });
        }

        /// <summary>
        /// 执行关闭步骤；对应入口为 Close。
        /// 正文中的关键协作调用包括 TryExecute、Cancel；具体条件和先后次序见方法体。
        /// </summary>
        public void Close()
        {
            _errors.TryExecute(() =>
            {
                _shutdown.Cancel();
            });
        }

        /// <summary>
        /// 添加标签打印列表
        /// </summary>
        /// <param name="print">标签信息</param>
        /// <returns></returns>
        public void Add(PrintInfo print)
        {
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
            {
                int pIndex = Model.SelectPrintIndex;
                var data = Model.PrintInfos[pIndex];
                pIndex = pIndex + 1;
                if (pIndex >= Model.PrintInfos.Count - 1) pIndex = Model.PrintInfos.Count - 1;
                Model.PrintInfos.Remove(data);
                Model.PrintInfos.Insert(pIndex, data);
            });
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 TestPrint。
        /// 正文中的关键协作调用包括 TryExecute、ShowDialog、PrintLabel；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public void TestPrint()
        {
            _errors.TryExecute( async () =>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PrintLabel。
        /// 正文中的关键协作调用包括 PrintAsync、ToOptions、SelectedProfileOrThrow；具体条件和先后次序见方法体。
        /// </summary>
        private Task PrintLabel(PrintInfo print) => _jobs.PrintAsync(
            _workspace.Model.SelectedProfileOrThrow().ToOptions(),
            new LabelPrintRequest(print.LabelPath, print.PrintName,
                print.LabelFieldInfo?.ToDictionary(field => field.Key, field => field.Value)), _shutdown.Token);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 PrintLabel_Path。
        /// 正文中的关键协作调用包括 PrintAsync、ToOptions、SelectedProfileOrThrow；具体条件和先后次序见方法体。
        /// </summary>
        public Task PrintLabel_Path(string labelPath, string printerName) => _jobs.PrintAsync(
            _workspace.Model.SelectedProfileOrThrow().ToOptions(),
            new LabelPrintRequest(labelPath, printerName), _shutdown.Token);
    }
}
