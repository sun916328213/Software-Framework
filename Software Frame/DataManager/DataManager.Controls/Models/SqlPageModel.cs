// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ISqlEntity、SqlPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、CommunityToolkit.Mvvm.ComponentModel、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System.Collections.ObjectModel;
using System.IO;
using WPFCustomControls.Controls;

namespace DataManager.Controls.Models
{
    /// <summary>
    /// SQL 行基础契约（对标 WPFControls 的 ISQLDataBase：标识 + 名称 + 时间，宿主定义实体并实现它）。
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
    /// 结果集与选中为本页扩展（对标 WPFControls 的 DataQueryModel 用法）。
    /// 行实体由宿主定义（实现 <see cref="ISqlEntity"/>）。
    /// </summary>
    public partial class SqlPageModel<T> : ConfigManagerModel<SQLParam> where T : ISqlEntity
    {
        /// <summary>
        /// 初始化 SqlPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Combine；具体条件和先后次序见方法体。
        /// </summary>
        public SqlPageModel()
        {
            // 落盘位置一次给定（基类 FilePath/FileName；完整路径走基类 FullPath）。
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            FileName = "SqlConnections.json";
        }

        [ObservableProperty]
        private SQLParam? selectedConnection;

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectedConnectionChanged。
        /// </summary>
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
