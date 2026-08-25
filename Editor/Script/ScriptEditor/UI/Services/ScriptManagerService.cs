using Extension.Core.ConvertEx;
using Extension.Tool;
using ScriptEditor.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WPFCustomControls.Window.Views;

namespace ScriptEditor.Services
{
    /// <summary>
    /// 脚本管理器服务层，封装所有脚本管理的业务逻辑。
    /// </summary>
    /// <remarks>
    /// <para>核心职责：</para>
    /// <list type="bullet">
    ///   <item>脚本 CRUD（添加/删除/初始化）——集合元素为轻量 <see cref="ScriptItem"/>（纯数据），
    ///         不包含任何 WPF 控件。重量级 RoslynPad 编辑器由 ScriptEditorView 在
    ///         所属标签页被选中时按需初始化。</item>
    ///   <item>执行调度（单次执行、循环执行、全部终止）——通过 ScriptItem 内部无头
    ///         <see cref="ScriptEngine"/> 完成，不依赖编辑器控件。</item>
    ///   <item>任务持久化（.taskproj JSON 格式保存/加载）</item>
    /// </list>
    /// <para>依赖服务：FileService（文件对话框）、ScriptEditorService（全局引用管理）。</para>
    /// </remarks>
    /// <author>ScriptEditor Team</author>
    /// <version>1.1</version>
    public class ScriptManagerService
    {
        #region 常量/字段

        /// <summary>脚本项集合（支持 AddRange 批量添加，仅触发一次 Reset 通知）。</summary>
        private readonly RangeObservableCollection<ScriptItem> _items = new();

        /// <summary>只读包装，供 View 层绑定。</summary>
        private readonly ReadOnlyObservableCollection<ScriptItem> _itemsReadOnly;

        /// <summary>文件对话框服务。</summary>
        private readonly FileService _fileService;

        /// <summary>脚本编辑器服务（全局引用管理、脚本目录）。</summary>
        private readonly ScriptEditorService _editorService;

        #endregion

        #region 构造函数

        public ScriptManagerService(FileService fileService, ScriptEditorService editorService)
        {
            _itemsReadOnly = new ReadOnlyObservableCollection<ScriptItem>(_items);
            _fileService = fileService;
            _editorService = editorService;
            _items.CollectionChanged += OnItemsCollectionChanged;
        }

        #endregion

        #region Model

        /// <summary>运行时状态模型，遵循项目 MVVM 模式，View 层直接绑定。</summary>
        public ScriptManagerModel Model { get; } = new ScriptManagerModel();

        #endregion

        #region 属性

        /// <summary>脚本项只读集合，供 View 层 ItemsSource 绑定。</summary>
        public ReadOnlyObservableCollection<ScriptItem> Items => _itemsReadOnly;

        /// <summary>当前脚本总数。</summary>
        public int TotalScriptCount => _items.Count;

        #endregion

        #region 脚本排序

        /// <summary>将选中脚本上移一位。</summary>
        public void MoveUp()
        {
            var item = Model.SelectedItem;
            if (item == null) return;
            var oldIndex = _items.IndexOf(item);
            if (oldIndex <= 0) return;
            _items.Move(oldIndex, oldIndex - 1);
            Model.SelectedItem = item;
            Model.StatusText = $"已上移脚本：{item.Title}";
        }

        /// <summary>将选中脚本下移一位。</summary>
        public void MoveDown()
        {
            var item = Model.SelectedItem;
            if (item == null) return;
            var oldIndex = _items.IndexOf(item);
            if (oldIndex < 0 || oldIndex >= _items.Count - 1) return;
            _items.Move(oldIndex, oldIndex + 1);
            Model.SelectedItem = item;
            Model.StatusText = $"已下移脚本：{item.Title}";
        }

        /// <summary>将选中脚本移至最上方。</summary>
        public void MoveToTop()
        {
            var item = Model.SelectedItem;
            if (item == null) return;
            var oldIndex = _items.IndexOf(item);
            if (oldIndex <= 0) return;
            _items.Move(oldIndex, 0);
            Model.SelectedItem = item;
            Model.StatusText = $"已移至最上方：{item.Title}";
        }

        /// <summary>将选中脚本移至最下方。</summary>
        public void MoveToBottom()
        {
            var item = Model.SelectedItem;
            if (item == null) return;
            var oldIndex = _items.IndexOf(item);
            if (oldIndex < 0 || oldIndex >= _items.Count - 1) return;
            _items.Move(oldIndex, _items.Count - 1);
            Model.SelectedItem = item;
            Model.StatusText = $"已移至最下方：{item.Title}";
        }

        /// <summary>复制选中脚本，插入到原脚本之后。</summary>
        public void CopyScript()
        {
            var item = Model.SelectedItem;
            if (item == null) return;
            var oldIndex = _items.IndexOf(item);
            if (oldIndex < 0) return;

            var clone = new ScriptItem
            {
                Title = $"{item.Title} - 副本",
                Code = item.Code
            };
            _items.Insert(oldIndex + 1, clone);
            Model.SelectedItem = clone;
            Model.StatusText = $"已复制脚本：{clone.Title}";
        }

        #endregion

        #region 脚本管理

        /// <summary>
        /// 添加新脚本（默认标题"新脚本 N"）。纯同步方法，创建轻量数据对象不涉及 UI 控件。
        /// </summary>
        /// <returns>新创建的 ScriptItem 实例。</returns>
        public ScriptItem AddScript()
        {
            var index = _items.Count + 1;
            var item = new ScriptItem { Title = $"新脚本 {index}", Code = _editorService.GetDefaultTemplate() };

            // 在独立 UI 线程上创建 FileLoading 窗口，主线程阻塞时动画不受影响
            var ready = new System.Threading.ManualResetEventSlim(false);
            WPFCustomControls.Window.Views.FileLoading? loadingWindow = null;
            var thread = new System.Threading.Thread(() =>
            {
                loadingWindow = new WPFCustomControls.Window.Views.FileLoading { Text = "正在加载编辑器..." };
                loadingWindow.Show();
                ready.Set();
                System.Windows.Threading.Dispatcher.Run();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();
            item.LoadingWindow = loadingWindow;

            _items.Add(item);
            System.Diagnostics.Debug.WriteLine($"[ScriptManager] AddScript: added '{item.Title}', _items.Count={_items.Count}");

            Model.SelectedItem = item;
            Model.StatusText = $"已添加脚本：{item.Title}";
            return item;
        }

        /// <summary>
        /// 添加脚本项（可指定标题和可选的文件路径）。
        /// </summary>
        /// <param name="title">脚本标题（Tab 头显示）。</param>
        /// <param name="filePath">可选的脚本文件路径，存在时自动加载代码。</param>
        /// <returns>新创建的 ScriptItem 实例。</returns>
        public ScriptItem AddScriptItem(string title, string? filePath = null)
        {
            var item = new ScriptItem { Title = title };
            if (filePath != null && System.IO.File.Exists(filePath))
            {
                var code = System.IO.File.ReadAllText(filePath);
                item.Code = code;
            }
            _items.Add(item);
            return item;
        }

        /// <summary>
        /// 删除脚本（弹出确认对话框，用户确认后终止执行并从集合移除）。
        /// </summary>
        /// <param name="item">要删除的脚本项。</param>
        /// <returns>删除成功返回 true，用户取消或 item 为 null 返回 false。</returns>
        public bool RemoveScript(ScriptItem? item)
        {
            if (item == null) return false;

            var prompt = new MessagePrompt("确认删除", $"确定要删除\"{item.Title}\"吗？此操作不可撤销。", Visibility.Visible);
            if (prompt.ShowDialog() != true) return false;

            item.Terminate();
            var title = item.Title;
            _items.Remove(item);
            Model.StatusText = $"已删除脚本：{title}";
            return true;
        }

        /// <summary>
        /// 删除全部脚本（弹出确认对话框，用户确认后终止所有并从集合移除）。
        /// </summary>
        public void RemoveAllScripts()
        {
            if (_items.Count == 0) return;

            var prompt = new MessagePrompt("确认删除全部", "确定要删除全部脚本吗？此操作不可撤销。", Visibility.Visible);
            if (prompt.ShowDialog() != true) return;

            foreach (var item in _items.ToList())
            {
                item.Terminate();
                _items.Remove(item);
            }
            Model.StatusText = "已删除全部脚本";
        }

        /// <summary>
        /// 预编译所有脚本（无头后台编译，加速首次执行）。编辑器不再在启动时创建，
        /// 因此此处不再初始化 RoslynPad，仅预热编译缓存。
        /// </summary>
        public async Task LoadAsync()
        {
            foreach (var item in _items)
            {
                await item.PreCompileAsync();
            }
        }

        #endregion

        #region 任务持久化

        /// <summary>
        /// 保存当前任务：如果已有路径则直接覆盖保存，否则弹出另存为对话框。
        /// </summary>
        public async Task SaveTaskAsync()
        {
            if (!string.IsNullOrEmpty(Model.CurrentFilePath))
            {
                SaveToFile(Model.CurrentFilePath);
            }
            else
            {
                await SaveAsTaskAsync();
            }
        }

        /// <summary>
        /// 另存为：始终弹出保存对话框，让用户选择新路径。
        /// </summary>
        public async Task SaveAsTaskAsync()
        {
            var path = _fileService.ShowSaveDialog(
                "任务文件 (*.taskproj)|*.taskproj|所有文件 (*.*)|*.*",
                "MyTask.taskproj",
                _editorService.Model.ScriptsDirectoryPath);
            if (string.IsNullOrEmpty(path)) return;

            SaveToFile(path);
            Model.CurrentFilePath = path;
            await Task.CompletedTask;
        }

        /// <summary>
        /// 将当前任务序列化并写入指定路径。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        private void SaveToFile(string path)
        {
            try
            {
                var model = new TaskProjectModel
                {
                    Name = Model.TaskName,
                    Scripts = _items.ToList(),
                    References = _editorService.AdditionalReferencePaths.ToList()
                };

                var json = JsonEx.ToJson(model);
                System.IO.File.WriteAllText(path, json);
                Model.StatusText = $"保存成功：{path}";
            }
            catch (Exception ex)
            {
                new MessagePrompt("错误", $"保存任务失败:\n{ex.Message}", Visibility.Collapsed).ShowDialog();
                Model.StatusText = "保存失败";
            }
        }

        /// <summary>
        /// 将当前任务保存到指定路径（供外部服务调用，如启动流程保存）。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        public async Task SaveToPathAsync(string path)
        {
            SaveToFile(path);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 从 .taskproj 文件加载任务（弹出文件选择对话框）。
        /// </summary>
        public async Task LoadTaskAsync()
        {
            FileLoading fileLoading = new FileLoading();

            var path = _fileService.ShowOpenDialog(
                "任务文件 (*.taskproj)|*.taskproj|所有文件 (*.*)|*.*");
            if (string.IsNullOrEmpty(path)) return;

            fileLoading.Text = "正在加载任务...";
            fileLoading.Show();
            // 让出 UI 线程让 FileLoading 窗口完成首帧渲染（布局 + 圆形进度条动画启动）
            await Dispatcher.Yield(DispatcherPriority.Render);
            await LoadTaskFromPathAsync(path);
            // 短暂停留让用户看到"加载完成"状态
            await Task.Delay(300);
            fileLoading.Close();
        }

        /// <summary>
        /// 从指定路径加载任务（纯加载逻辑，不包含进度指示）。
        /// </summary>
        /// <param name="path">.taskproj 文件的绝对路径。</param>
        public async Task LoadTaskFromPathAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

            Exception? loadError = null;
            try
            {
                // ---- 后台 I/O：读取文件 + JSON 解析 ----
                Model.StatusText = "正在解析任务文件...";
                var model = await Task.Run(() =>
                {
                    var json = System.IO.File.ReadAllText(path);
                    return JsonEx.FromJson<TaskProjectModel>(json);
                });

                if (model?.Scripts == null)
                {
                    new MessagePrompt("错误", "任务文件格式不正确或为空。", Visibility.Collapsed).ShowDialog();
                    Model.StatusText = "加载失败";
                    return;
                }

                // ---- UI 线程：清除旧脚本 ----
                foreach (var item in _items.ToList())
                {
                    item.Terminate();
                    _items.Remove(item);
                }
                // 加载共享 DLL 引用：直接操作 Model 数据层，最后一次性刷新 UI
                _editorService.Model.ClearReferenceData();
                if (model.References != null)
                {
                    foreach (var refPath in model.References)
                        _editorService.Model.AddReferenceData(refPath);
                }
                _editorService.Model.RefreshReferenceUI();
 
                var total = model.Scripts.Count;
                var tempItems = new List<ScriptItem>(total);
 
                for (int i = 0; i < total; i++)
                {
                    var script = model.Scripts[i];

                    var item = new ScriptItem { Title = script.Title, Code = script.Code };
                    item.SetReferences(script.References ?? new List<string>());
                    tempItems.Add(item);
                }
 
                for (int i = 0; i < tempItems.Count; i++)
                {
                    _items.Add(tempItems[i]);
                }
 
                if (_items.Count > 0)
                    Model.SelectedItem = _items[0];
                if (!string.IsNullOrEmpty(model.Name))
                    Model.TaskName = model.Name;
                Model.CurrentFilePath = path;

                Model.StatusText = $"加载完成，共 {_items.Count} 个脚本";
            }
            catch (Exception ex)
            {
                loadError = ex;
            }

            if (loadError != null)
            {
                new MessagePrompt("错误", $"加载任务失败:\n{loadError.Message}", Visibility.Collapsed).ShowDialog();
                Model.StatusText = "加载失败";
            }
        }

        #endregion

        #region 执行调度

        /// <summary>
        /// 全部终止执行（停止循环 + 终止所有脚本）。
        /// </summary>
        public void StopAll()
        {
            StopLoop();
            foreach (var item in _items)
                item.Terminate();
            Model.StatusText = "已全部终止";
        }

        /// <summary>
        /// 并行执行所有脚本（通过每个 ScriptItem 的无头引擎执行，无需编辑器控件）。
        /// </summary>
        /// <remarks>
        /// 并行安全：各脚本使用独立 <see cref="ScriptEngine"/> 实例，且引擎的
        /// <c>RunAsync</c> 将编译结果捕获到局部变量后再执行，编译完成先后不定
        /// 不会导致"脚本1执行了脚本2代码"的时序错乱。
        /// </remarks>
        public async Task RunAllAsync()
        {
            Model.IsRunning = true;
            Model.StatusText = "正在执行所有脚本...";
            Model.NotifyStateChanged();

            // 预设容量避免扩容
            var runningTasks = new List<Task>(_items.Count);
            foreach (var item in _items)
            {
                item.ItemStatus = "执行中...";
                runningTasks.Add(item.ExecuteAsync());
            }

            if (runningTasks.Count > 0)
                await Task.WhenAll(runningTasks);

            foreach (var item in _items)
                item.ItemStatus = "就绪";

            Model.IsRunning = false;
            Model.StatusText = "所有脚本执行完成";
            Model.NotifyStateChanged();
        }

        /// <summary>
        /// 开始循环执行。
        /// 直接委托给每个 ScriptItem 的无头引擎自身的循环逻辑。
        /// </summary>
        public async Task LoopRunAsync()
        {
            // 先停止旧循环，避免重入
            StopLoop();

            Model.IsLooping = true;
            Model.StatusText = "循环执行中...";
            Model.NotifyStateChanged();

            // 快照脚本列表
            var itemsSnapshot = _items.ToList();

            // 并行启动所有脚本的循环，LoopRunAsync 内部自行管理线程和生命周期
            var tasks = itemsSnapshot
                .Select(item => item.LoopRunAsync())
                .ToList();

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);

            Model.IsLooping = false;
            await Dispatcher.Yield(DispatcherPriority.Background);
            foreach (var item in _items)
                item.ItemStatus = "就绪";
            Model.StatusText = "循环执行已停止";
            Model.NotifyStateChanged();
        }

        /// <summary>
        /// 停止循环执行。
        /// </summary>
        public void StopLoop()
        {
            foreach (var item in _items)
                item.StopLoop();
            Model.StatusText = "正在停止循环...";
        }

        #endregion

        #region 内部方法

        /// <summary>集合变化事件处理：通知 Model 触发 CollectionChanged。</summary>
        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Model.NotifyCollectionChanged();
        }

        #endregion
    }
}