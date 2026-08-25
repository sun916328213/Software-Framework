using System.Reflection;

namespace Extension.Core.ControlEx
{
    /// <summary>
    /// DataGridView扩展类 Winforms
    /// </summary>
    //public class DataGridViewEx<T>
    //{  /// <summary>
    //   /// 数据源
    //   /// </summary>
    //    private List<T> _AllData = new List<T>();
    //    /// <summary>
    //    /// 存储T的属性信息
    //    /// </summary>
    //    private List<PropertyInfo> properties;

    //    public DataGridViewEx()
    //    {
    //        // 获取类型的所有公共属性
    //        Type type = typeof(T);
    //        properties = type.GetProperties().ToList();
    //    }

    //    /// <summary>
    //    /// 设置DataGridView虚模式
    //    /// </summary>
    //    /// <param name="dataGridView"></param>
    //    private void SetVirtualMode(DataGridView dataGridView)
    //    {
    //        //设置DataGridView参数
    //        Type dgvType = dataGridView.GetType();
    //        //开启双缓冲
    //        PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
    //        pi.SetValue(dataGridView, true, null);
    //        dataGridView.VirtualMode = true;// 启用虚拟模式
    //        dataGridView.AllowUserToAddRows = false; // 防止用户添加行
    //        dataGridView.CellValueNeeded -= DataGridView_CellValueNeeded;
    //        dataGridView.CellValueNeeded += DataGridView_CellValueNeeded;
    //        dataGridView.Rows.Clear();
    //    }


    //    /// <summary>
    //    /// 显示数据
    //    /// </summary>
    //    /// <param name="pDataGridView">DataGridView控件</param>
    //    /// <param name="pData">数据集</param>
    //    public void ShowData(DataGridView pDataGridView, List<T> pData)
    //    {
    //        SetVirtualMode(pDataGridView);
    //        _AllData = pData;
    //        pDataGridView.BeginInvoke(new Action(() =>
    //        {
    //            pDataGridView.RowCount = pData.Count;
    //        }));
    //    }

    //    /// <summary>
    //    /// 虚模式填充数据事件
    //    /// </summary>
    //    /// <param name="sender"></param>
    //    /// <param name="e"></param>
    //    private void DataGridView_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
    //    {
    //        try
    //        {
    //            DataGridView dataGridView = (DataGridView)sender;

    //            // If this is the row for new records, no values are needed.   
    //            if (e.RowIndex == dataGridView.RowCount)
    //                return;

    //            // 获取当前行的数据对象
    //            object item = _AllData[e.RowIndex];
    //            // 通过反射获取属性值
    //            PropertyInfo property = properties[e.ColumnIndex];
    //            object value = property.GetValue(item);

    //            // 处理null值
    //            e.Value = value ?? DBNull.Value;

    //        }
    //        catch { }
    //    }
    //}
}