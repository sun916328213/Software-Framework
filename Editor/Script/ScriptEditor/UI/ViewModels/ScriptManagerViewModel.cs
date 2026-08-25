using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ScriptEditor.Views;
using ScriptEditor.Models;
using ScriptEditor.Services;

namespace ScriptEditor.ViewModels
{
    /// <summary>
    /// 脚本管理器 ViewModel，仅负责数据绑定和命令暴露。
    /// </summary>
    /// <remarks>
    /// <para>所有业务逻辑委托给 ScriptManagerService，本类不含有业务逻辑。</para>
    /// <para>View 层通过绑定 Model 属性获取运行时状态，通过命令触发 Service 方法。</para>
    /// </remarks>
    /// <author>ScriptEditor Team</author>
    /// <version>1.0</version>
    public class ScriptManagerViewModel : ObservableObject
    {
        #region 字段

        private readonly ScriptManagerService _service;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造注入 ScriptManagerService（由 Prism 容器自动解析）。
        /// </summary>
        /// <param name="service">脚本管理器服务实例。</param>
        public ScriptManagerViewModel(ScriptManagerService service)
        {
            _service = service;

            AddScriptCommand = new RelayCommand(() => _service.AddScript());
            RemoveScriptCommand = new RelayCommand<Models.ScriptItem>(view => _service.RemoveScript(view));
            RemoveAllScriptCommand = new RelayCommand(() => _service.RemoveAllScripts());
            MoveUpCommand = new RelayCommand(() => _service.MoveUp(), () => Model.SelectedItem != null);
            MoveDownCommand = new RelayCommand(() => _service.MoveDown(), () => Model.SelectedItem != null);
            MoveToTopCommand = new RelayCommand(() => _service.MoveToTop(), () => Model.SelectedItem != null);
            MoveToBottomCommand = new RelayCommand(() => _service.MoveToBottom(), () => Model.SelectedItem != null);
            CopyScriptCommand = new RelayCommand(() => _service.CopyScript(), () => Model.SelectedItem != null);
            // AsyncRelayCommand 自动管理 CanExecute：执行期间 IsRunning=true → CanExecute=false
            RunAllCommand = new AsyncRelayCommand(_service.RunAllAsync, CanRunOrLoop);
            // StopAll 始终可用（停止脚本、取消循环等）
            StopAllCommand = new RelayCommand(_service.StopAll);
            LoopRunCommand = new AsyncRelayCommand(_service.LoopRunAsync, CanRunOrLoop);
            StopLoopCommand = new RelayCommand(_service.StopLoop, CanStopLoop);
            SaveTaskCommand = new AsyncRelayCommand(_service.SaveTaskAsync);
            SaveAsTaskCommand = new AsyncRelayCommand(_service.SaveAsTaskAsync);
            LoadTaskCommand = new AsyncRelayCommand(_service.LoadTaskAsync);
            _service.Model.StateChanged += OnServiceStateChanged;
            _service.Model.CollectionChanged += OnServiceCollectionChanged;
            _service.Model.PropertyChanged += OnModelPropertyChanged;
        }

        #endregion

        #region Model（View 直接绑定到此属性）

        /// <summary>运行时状态模型，View 直接绑定 <c>{Binding Model.XXX}</c>。</summary>
        public ScriptManagerModel Model => _service.Model;

        /// <summary>脚本只读集合，供 View 层 TabControl.ItemsSource 绑定。</summary>
        public ReadOnlyObservableCollection<Models.ScriptItem> Items => _service.Items;

        /// <summary>脚本总数（Service 计算）。</summary>
        public int TotalScriptCount => _service.TotalScriptCount;

        #endregion

        #region 命令

        /// <summary>添加新脚本命令（默认标题）。</summary>
        public IRelayCommand AddScriptCommand { get; }

        /// <summary>删除指定脚本命令（带确认对话框）。</summary>
        public IRelayCommand<Models.ScriptItem> RemoveScriptCommand { get; }

        /// <summary>删除全部脚本命令（带确认对话框）。</summary>
        public IRelayCommand RemoveAllScriptCommand { get; }

        /// <summary>并行执行全部脚本命令（执行期间自动禁用）。</summary>
        public IAsyncRelayCommand RunAllCommand { get; }

        /// <summary>全部终止执行命令。</summary>
        public IRelayCommand StopAllCommand { get; }

        /// <summary>开始循环执行命令（循环期间自动禁用）。</summary>
        public IAsyncRelayCommand LoopRunCommand { get; }

        /// <summary>停止循环执行命令（仅循环期间可用）。</summary>
        public IRelayCommand StopLoopCommand { get; }

        /// <summary>保存任务到 .taskproj 文件命令。</summary>
        public IAsyncRelayCommand SaveTaskCommand { get; }

        /// <summary>另存为任务命令（始终弹出保存对话框）。</summary>
        public IAsyncRelayCommand SaveAsTaskCommand { get; }

        /// <summary>从 .taskproj 文件加载任务命令。</summary>
        public IAsyncRelayCommand LoadTaskCommand { get; }

        /// <summary>将选中脚本上移一位命令。</summary>
        public IRelayCommand MoveUpCommand { get; }

        /// <summary>将选中脚本下移一位命令。</summary>
        public IRelayCommand MoveDownCommand { get; }

        /// <summary>将选中脚本移至最上方命令。</summary>
        public IRelayCommand MoveToTopCommand { get; }

        /// <summary>将选中脚本移至最下方命令。</summary>
        public IRelayCommand MoveToBottomCommand { get; }

        /// <summary>复制选中脚本命令（插入到原脚本之后）。</summary>
        public IRelayCommand CopyScriptCommand { get; }

        #endregion

        #region CanExecute 判定

        /// <summary>执行/循环命令 CanExecute：未运行且未循环时可用。</summary>
        private bool CanRunOrLoop() => !_service.Model.IsLooping && !_service.Model.IsRunning;

        /// <summary>停止循环命令 CanExecute：循环中时可用。</summary>
        private bool CanStopLoop() => _service.Model.IsLooping;

        #endregion

        #region 事件处理

        /// <summary>服务状态变化回调：刷新所有命令的 CanExecute 状态。</summary>
        private void OnServiceStateChanged()
        {
            RunAllCommand.NotifyCanExecuteChanged();
            StopAllCommand.NotifyCanExecuteChanged();
            LoopRunCommand.NotifyCanExecuteChanged();
            StopLoopCommand.NotifyCanExecuteChanged();
        }

        /// <summary>服务集合变化回调：通知 View 刷新脚本总数和移动命令。</summary>
        private void OnServiceCollectionChanged()
        {
            OnPropertyChanged(nameof(TotalScriptCount));
            RefreshMoveCommands();
        }

        /// <summary>Model 属性变化回调：选中项变化时刷新移动命令。</summary>
        private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScriptManagerModel.SelectedItem))
                RefreshMoveCommands();
        }

        private void RefreshMoveCommands()
        {
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
            MoveToTopCommand.NotifyCanExecuteChanged();
            MoveToBottomCommand.NotifyCanExecuteChanged();
            CopyScriptCommand.NotifyCanExecuteChanged();
        }

        #endregion
    }
}