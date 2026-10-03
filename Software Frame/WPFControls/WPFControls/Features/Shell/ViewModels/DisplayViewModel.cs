// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DisplayViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、HalconDotNet、System.Collections.ObjectModel、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;
using System.Collections.ObjectModel;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// DisplayViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
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
