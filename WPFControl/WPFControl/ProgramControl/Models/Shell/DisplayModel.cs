using CommunityToolkit.Mvvm.ComponentModel;
using DPLibrary_Halcon_F48.Tools;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Views.Display;

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

        ObservableCollection<HVisionTool> _VisionTools=new ObservableCollection<HVisionTool>();
        /// <summary>
        /// 用于多个显示控件的VisionTool集合
        /// </summary>
        public ObservableCollection<HVisionTool> VisionTools
        {
            get => _VisionTools;
            set => SetProperty(ref _VisionTools, value);
        }
        
        /// <summary>
        /// 设置指定索引的VisionTool并触发通知
        /// </summary>
        /// <param name="index">索引</param>
        /// <param name="tool">VisionTool实例</param>
        public void SetVisionTool(int index, HVisionTool tool)
        {
            // 确保集合中有足够数量的元素
            while (VisionTools.Count <= index)
            {
                VisionTools.Add(null);
            }
            
            // 设置指定索引的元素
            VisionTools[index] = tool;
            
            // 触发集合更改通知
            OnPropertyChanged(nameof(VisionTools));
        }

        /// <summary>
        /// 触发VisionTools属性更改通知
        /// </summary>
        public void NotifyVisionToolsChanged()
        {
            OnPropertyChanged(nameof(VisionTools));
        }
    }
}