using Extension.UI.Controls;
using HalconDotNet;
using System.Windows;
using WPFControl.CustomControl.Views.Display;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using FastDeepCloner;

namespace WPFControl.ProgramControl.Services
{
    public class DisplayService
    {
        public DisplayModel Model { get; set; } = new DisplayModel();

        /// <summary>
        /// 在 UI 线程上执行集合变更。
        /// DisplayTitles/VisionTools 是 ObservableCollection，一旦被 UI 绑定（DisplayView 显示后
        /// 挂接 CollectionView），从后台线程（脚本经 Task.Run 执行）修改会抛
        /// NotSupportedException。这里统一投递到 UI 线程，保证任何线程调用都安全。
        /// </summary>
        private static void OnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
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
            TryCatchHelper.TryExecute(() => OnUiThread(() =>
            {
                Model.RowCount = row;
                Model.ColumnCount = col;
                ClearCore();
            }));
        }

        public void Clear()
        {
            TryCatchHelper.TryExecute(() => OnUiThread(ClearCore));
        }

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
            TryCatchHelper.TryExecute(() =>
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