using CommunityToolkit.Mvvm.ComponentModel;
using HalconDotNet;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.Models
{
    public class DisplayModel : ObservableObject
    {
        /// <summary>
        /// 显示标题集合（新方式：纯数据，脚本可安全添加，WPF 绑定自动创建 UI）。
        /// 使用此集合即可避免线程安全问题，无需 Dispatcher.Invoke。
        /// </summary>
        public ObservableCollection<string> DisplayTitles { get; set; } = new ObservableCollection<string>();

        private int _rowCount;
        /// <summary>
        /// 显示行数
        /// </summary>
        public int RowCount
        {
            get => _rowCount;
            set => SetProperty(ref _rowCount, value);
        }

        private int _columnCount;
        /// <summary>
        /// 显示列数
        /// </summary>
        public int ColumnCount
        {
            get => _columnCount;
            set => SetProperty(ref _columnCount, value);
        }

        ObservableCollection<HObject> _DisplayImages = new ObservableCollection<HObject>();
        /// <summary>
        /// 用于多个显示控件的图像集合（与 DisplayTitles 按索引对齐）。
        /// 2026-09-29：旧 HVisionTool 工具模型随 DPLibrary 删除，显示链改为直接显示 HObject，
        /// 上游（脚本/站点）把要显示的图像传进来即可。
        /// </summary>
        public ObservableCollection<HObject> DisplayImages
        {
            get => _DisplayImages;
            set => SetProperty(ref _DisplayImages, value);
        }

        /// <summary>
        /// 设置指定索引的显示图像并触发通知
        /// </summary>
        /// <param name="index">索引</param>
        /// <param name="image">图像实例</param>
        public void SetDisplayImage(int index, HObject image)
        {
            // 确保集合中有足够数量的元素
            while (DisplayImages.Count <= index)
            {
                DisplayImages.Add(null);
            }

            // 设置指定索引的元素
            DisplayImages[index] = image;

            // 触发集合更改通知
            OnPropertyChanged(nameof(DisplayImages));
        }

        /// <summary>
        /// 触发显示图像集合属性更改通知
        /// </summary>
        public void NotifyDisplayImagesChanged()
        {
            OnPropertyChanged(nameof(DisplayImages));
        }
    }
}
