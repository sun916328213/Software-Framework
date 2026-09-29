using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;
using System.Collections.ObjectModel;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
    public class DisplayViewModel : ObservableObject
    {
        private readonly DisplayService _service;

        // 行数属性
        public int RowCount => _service.Model.RowCount;

        // 列数属性
        public int ColumnCount => _service.Model.ColumnCount;

        // 显示标题集合（纯数据绑定驱动 UI 创建，线程安全）
        public ObservableCollection<string> DisplayTitles => _service.Model.DisplayTitles;

        // 显示图像集合（与显示标题按索引对齐）
        public ObservableCollection<HObject> DisplayImages => _service.Model.DisplayImages;

        // 构造函数
        public DisplayViewModel(DisplayService service)
        {
            _service = service;
        }

        // 设置行列命令
        public IRelayCommand<(int row, int col)> SetRowColCommand => new RelayCommand<(int row, int col)>(param =>
        {
            _service.SetRowCol(param.row, param.col);
            OnPropertyChanged(nameof(RowCount));
            OnPropertyChanged(nameof(ColumnCount));
        });

        // 添加显示控件命令
        public IRelayCommand<string> AddDisplayCommand => new RelayCommand<string>(title =>
        {
            _service.AddDisplayTitle(title);
            OnPropertyChanged(nameof(DisplayTitles));
        });

        // 清除所有显示控件命令
        public IRelayCommand ClearCommand => new RelayCommand(() =>
        {
            _service.Clear();
            OnPropertyChanged(nameof(DisplayTitles));
            OnPropertyChanged(nameof(DisplayImages));
        });

        // 显示图像命令
        public IRelayCommand<(int index, HObject image)> ShowImageCommand => new RelayCommand<(int index, HObject image)>(param =>
        {
            _service.ShowImage(param.index, param.image);
            OnPropertyChanged(nameof(DisplayImages));
        });

        // 通知布局更新命令（供View调用）
        public void NotifyLayoutUpdated()
        {
            OnPropertyChanged(nameof(DisplayTitles));
        }
    }
}
