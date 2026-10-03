// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DisplayModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、HalconDotNet、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using HalconDotNet;
using System.Collections.ObjectModel;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// DisplayModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
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
