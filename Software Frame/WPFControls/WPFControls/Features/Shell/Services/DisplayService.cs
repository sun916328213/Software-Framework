// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DisplayService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Extension.UI.Controls、HalconDotNet、System.Windows、WPFControls.ProgramControl.Views.Display、WPFControls.Controls.Diagnostics。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Extension.UI.Controls;
using HalconDotNet;
using System.Windows;
using WPFControls.ProgramControl.Views.Display;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using FastDeepCloner;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// DisplayService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class DisplayService
    {
        private readonly TryCatchHelper _errors;
        /// <summary>
        /// 初始化 DisplayService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public DisplayService(TryCatchHelper? errors = null) => _errors = errors ?? new();

        public DisplayModel Model { get; set; } = new DisplayModel();

        /// <summary>
        /// 在 UI 线程上执行集合变更。
        /// DisplayTitles/VisionTools 是 ObservableCollection，一旦被 UI 绑定（DisplayView 显示后
        /// 挂接 CollectionView），从后台线程（脚本经 Task.Run 执行）修改会抛
        /// NotSupportedException。这里统一投递到 UI 线程，保证任何线程调用都安全。
        /// </summary>
        private static void OnUiThread(Action action)
        {
            var dispatcher = global::System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// 设置行列
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        public void SetRowCol(int row, int col)
        {
            _errors.TryExecute(() => OnUiThread(() =>
            {
                Model.RowCount = row;
                Model.ColumnCount = col;
                ClearCore();
            }));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Clear。
        /// 正文中的关键协作调用包括 TryExecute、OnUiThread；具体条件和先后次序见方法体。
        /// </summary>
        public void Clear()
        {
            _errors.TryExecute(() => OnUiThread(ClearCore));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClearCore。
        /// 正文中的关键协作调用包括 Clear；具体条件和先后次序见方法体。
        /// </summary>
        private void ClearCore()
        {
            Model.DisplayTitles.Clear();
            Model.DisplayImages.Clear();
        }

        /// <summary>
        /// 添加显示标题（集合变更统一在 UI 线程执行，可从脚本后台线程调用）。
        /// 推荐脚本使用此方法。
        /// </summary>
        public void AddDisplayTitle(string title)
        {
            OnUiThread(() =>
            {
                Model.DisplayTitles.Add(title);

                // 确保显示图像集合中有相应数量的元素
                while (Model.DisplayImages.Count < Model.DisplayTitles.Count)
                {
                    Model.DisplayImages.Add(null);
                }
            });
        }

        /// <summary>
        /// 显示图像
        /// </summary>
        /// <param name="index"></param>
        /// <param name="image"></param>
        public void ShowImage(int index, HObject image)
        {
            _errors.TryExecute(() =>
            {
                OnUiThread(() =>
                {
                    // 确保显示图像集合中有足够数量的元素
                    while (Model.DisplayImages.Count <= index)
                    {
                        Model.DisplayImages.Add(null);
                    }
                    // 设置指定索引的图像
                    Model.DisplayImages[index] = image;

                    // 触发通知确保UI更新
                    Model.NotifyDisplayImagesChanged();
                });

                // 保持在工作线程延时，避免阻塞 UI
                DelayEx.Delay(200);
            });
        }

    }
}