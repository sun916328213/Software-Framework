using DPLibrary_Halcon_F48.Tools;
using Extension.Core.SystemEx;
using HalconDotNet;
using System.Windows;
using WPFControl.CustomControl.Views.Display;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using Extension.Core.ImageEx;
using Extension.Core.DataEx;
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
            Model.VisionTools.Clear();
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

                // 确保VisionTools集合中有相应数量的元素
                while (Model.VisionTools.Count < Model.DisplayTitles.Count)
                {
                    Model.VisionTools.Add(null);
                }
            });
        }

        /// <summary>
        /// 显示图像
        /// </summary>
        /// <param name="index"></param>
        /// <param name="visionTool"></param>
        public void ShowImage(int index, HVisionTool visionTool)
        {
            TryCatchHelper.TryExecute(() =>
            {
                OnUiThread(() =>
                {
                    // 确保VisionTools集合中有足够数量的元素
                    while (Model.VisionTools.Count <= index)
                    {
                        Model.VisionTools.Add(null);
                    }
                    // 设置指定索引的VisionTool
                    Model.VisionTools[index] = visionTool;

                    // 触发通知确保UI更新
                    Model.NotifyVisionToolsChanged();
                });

                // 保持在工作线程延时，避免阻塞 UI
                DelayEx.Delay(200);
            });
        }

        public void ShowImage(int index, HObject image)
        {
            HTool_NPoints hTool_NPoints = new HTool_NPoints();
            hTool_NPoints.Image=image;
            OnUiThread(() =>
            {
                // 设置指定索引的VisionTool
                Model.VisionTools[index] = hTool_NPoints;
                // 触发通知确保UI更新
                Model.NotifyVisionToolsChanged();
            });

            // 保持在工作线程延时，避免阻塞 UI
            DelayEx.Delay(200);
        }

    }
}