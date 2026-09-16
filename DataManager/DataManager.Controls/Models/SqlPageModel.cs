using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.SQL.Definition;
using System.Collections.ObjectModel;
using System.IO;
using WPFCustomControls.Controls;

namespace DataManager.Controls.Models
{
    /// <summary>
    /// SQL 行基础契约（对标 WPFControl 的 ISQLDataBase：标识 + 名称 + 时间，宿主定义实体并实现它）。
    /// </summary>
    public interface ISqlEntity
    {
        long Id { get; set; }
        string Name { get; set; }
        DateTime? CreateTime { get; set; }
    }
 
    /// <summary>
    /// SQL 测试页状态 Model：继承通用配置管理模型（档案存 <see cref="ConfigManagerModel{T}.DataCollection"/>，
    /// 落盘位置走基类 <see cref="ConfigManagerModel{T}.FilePath"/> / FileName），
    /// 结果集与选中为本页扩展（对标 WPFControl 的 DataQueryModel 用法）。
    /// 行实体由宿主定义（实现 <see cref="ISqlEntity"/>）。
    /// </summary>
    public partial class SqlPageModel<T> : ConfigManagerModel<SQLParam> where T : ISqlEntity
    {
        public SqlPageModel()
        {
            // 落盘位置一次给定（基类 FilePath/FileName；完整路径走基类 FullPath）。
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            FileName = "SqlConnections.json";
        }

        [ObservableProperty]
        private SQLParam? selectedConnection;

        partial void OnSelectedConnectionChanged(SQLParam? value)
        {
            // 档案切换 = 连接要素变更，直接复位状态；stale 实例由流程按指纹重建。
            IsConnected = false;
            StatusText = "未连接";
        }

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private string statusText = "未连接";

        [ObservableProperty]
        private ObservableCollection<T> results = new();

        [ObservableProperty]
        private T? selectedEntity;

        /// <summary>当前档案（空即报“请先添加”）。</summary>
        public SQLParam SelectedProfile() =>
            SelectedConnection ?? throw new InvalidOperationException("请先添加数据库连接档案");
    }
}
