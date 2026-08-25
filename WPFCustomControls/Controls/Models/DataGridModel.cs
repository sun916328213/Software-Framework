using System.Collections.ObjectModel;

namespace WPFCustomControls.Controls
{
    /// <summary>
    /// DataGrid数据模型
    /// 纯粹的数据结构容器，不包含业务逻辑
    /// </summary>
    public class DataGridModel<T>
    {
        /// <summary>
        /// 数据源
        /// </summary>
        public ObservableCollection<T> DataCollection { get; } = new ObservableCollection<T>();

        /// <summary>
        /// 保留的最大数据条数
        /// </summary>
        public int RetainNum { get; set; } = 200;
    }
}
