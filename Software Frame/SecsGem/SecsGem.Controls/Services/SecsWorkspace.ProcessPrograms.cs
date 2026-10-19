using System.Text;
using SecsGem.Runtime.Order.S7;
using Secs4Net.Sml;

namespace SecsGem.Controls.Services;

/// <summary>配方通信流程由 Controls 统一管理；借用宿主数据，不负责切换当前配方。</summary>
public sealed partial class SecsWorkspace
{
    private Func<string[]>? _recipeIds;
    private Func<string, byte[]?>? _readHostRecipe;
    private Action<string, byte[], bool>? _updateHostRecipe;
    private Func<string?>? _currentHostPpid;
    private Action<string, string?>? _recipeReceived;
    private Action<string[]>? _deleteHostRecipes;
    private Action<string>? _formattedReceived;
    private Action<string, ProcessProgramData>? _updateHostFormattedRecipe;
    private Func<string, ProcessProgramData?>? _readHostFormattedRecipe;

    /// <summary>宿主只提供类型、集合和当前 PPID；JSON 转换在 Controls，下载/读取不会改变当前 PPID。</summary>
    public void ConfigureRecipeSource<T>(IDictionary<string, T> recipes, Func<string?> currentPpid, Action? changed = null,
        Action<string, string?>? received = null, Action<IReadOnlyList<string>>? deleted = null,
        Action<string>? formattedReceived = null, IReadOnlyList<string>? formattedParameters = null,
        ushort formattedCommandCode = 1, IFormattedRecipeCodec<T>? formattedCodec = null) where T : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(currentPpid);
        if (formattedCodec != null && formattedParameters != null)
            throw new ArgumentException("请在 formattedCodec 与简单 formattedParameters 映射之间选择一种。");
        var codec = formattedCodec ?? (formattedParameters is null ? null
            : new ParameterBlockRecipeCodec<T>(formattedParameters, formattedCommandCode));
        _recipeReceived = received;
        _formattedReceived = formattedReceived;
        _deleteHostRecipes = ids =>
        {
            if (ids.Length == 0) recipes.Clear();
            else foreach (var id in ids) recipes.Remove(id);
            deleted?.Invoke(ids);
            changed?.Invoke();
        };
        _recipeIds = () => recipes.Keys.ToArray();
        _readHostRecipe = id => recipes.TryGetValue(id, out var recipe) ? ProcessProgramCommunication.Serialize(recipe) : null;
        ProcessProgramData EncodeRecipe(string id, T recipe)
        {
            var program = codec!.Encode(id, recipe);
            if (program.PPID != id) throw new InvalidOperationException($"格式化编解码器返回的 PPID 与请求 {id} 不一致。");
            SecsGem.Runtime.SecsGemRuntime.ValidateFormattedProcessProgram(program);
            return program;
        }
        _readHostFormattedRecipe = codec is null ? null : id =>
            recipes.TryGetValue(id, out var recipe) ? EncodeRecipe(id, recipe) : null;
        void CommitRecipe(string id, T recipe)
        {
            recipes[id] = recipe;
            ReportRecipeReceived(id, null);
            try { changed?.Invoke(); }
            catch (Exception ex) { AddLog("警告", $"配方数据已更新，但宿主界面通知失败：{ex.Message}"); }
        }
        _updateHostRecipe = (id, body, publishFormatted) =>
        {
            T recipe;
            try
            {
                recipe = ProcessProgramCommunication.Deserialize<T>(body);
                // 同一业务配方同时发布格式化表示，S7F25 不必依赖 Host 曾经下载过 S7F23。
                if (codec != null && publishFormatted) _runtime.StoreFormattedProcessProgram(EncodeRecipe(id, recipe));
            }
            catch (Exception ex)
            {
                ReportRecipeReceived(id, ex.Message); // 原始 PPID 仍可见，失败不覆盖旧的有效数据。
                throw;
            }
            CommitRecipe(id, recipe); // 完整解析/验证后提交，不改变宿主的当前 PPID。
            if (codec != null && publishFormatted) ReportFormattedRecipeReceived(id);
        };
        _updateHostFormattedRecipe = codec is null ? null : (id, program) =>
        {
            T recipe;
            try
            {
                recipe = codec.Decode(program);
                ProcessProgramCommunication.Serialize(recipe); // 自定义编解码器也不能绕过宿主声明的校验。
            }
            catch (Exception ex)
            {
                ReportRecipeReceived(id, ex.Message);
                throw;
            }
            CommitRecipe(id, recipe);
        };
        _currentHostPpid = currentPpid;
        Model.HasRecipeSource = true;
        Model.RecipeRequestSml = "";
        Model.RecipeJsonPreview = "";
        // 绑定数据源前已接收的下载也要补入宿主列表，不依赖任何页面加载或按钮。
        foreach (var entry in _runtime.GetProcessPrograms())
        {
            if (entry.HasBinary)
            {
                var body = _runtime.GetUnformattedProcessProgram(entry.PPID);
                if (body != null) SyncDownloadedRecipe(entry.PPID, body, publishFormatted: !entry.HasFormatted);
            }
            if (entry.HasFormatted) SyncFormattedRecipe(entry.PPID);
        }
        RefreshProcessPrograms();
    }

    private void ClearRecipeSource()
    {
        _recipeIds = null;
        _readHostRecipe = null;
        _updateHostRecipe = null;
        _currentHostPpid = null;
        _recipeReceived = null;
        _deleteHostRecipes = null;
        _formattedReceived = null;
        _updateHostFormattedRecipe = null;
        _readHostFormattedRecipe = null;
        Model.HasRecipeSource = false;
    }

    private void OnFormattedProcessProgramReceived(string ppid) => Post(() => SyncFormattedRecipe(ppid));

    private void SyncFormattedRecipe(string ppid) => RunRecipe(() =>
    {
        // 仅同步实际存储且仍存在的目录项；迟到通知不能恢复已删除配方。
        var program = _runtime.GetFormattedProcessProgram(ppid);
        if (program is null) return;
        ReportFormattedRecipeReceived(ppid);
        _updateHostFormattedRecipe?.Invoke(ppid, program);
        Model.RecipeStatus = _updateHostFormattedRecipe is null
            ? $"S7F23 PPID={ppid} 已接收格式化正文，已通知配方列表；未配置业务映射，不改变宿主工艺参数。"
            : $"S7F23 PPID={ppid} 工艺参数映射及校验成功，配方列表已更新、可手动切换；当前配方未切换。";
        AddLog("配方", Model.RecipeStatus);
        RefreshProcessPrograms();
    }, $"S7F23 PPID={ppid} 工艺参数映射/列表同步失败：");

    private void OnProcessProgramsDeleted(string[] ids)
    {
        var snapshot = ids.ToArray();
        Post(() => RunRecipe(() =>
        {
            _deleteHostRecipes?.Invoke(snapshot);
            Model.RecipeStatus = snapshot.Length == 0
                ? "S7F17 已删除全部配方，配方管理列表已清空；当前快照未自动切换。"
                : $"S7F17 已删除 {string.Join(", ", snapshot)}，配方管理列表已刷新。";
            AddLog("配方", Model.RecipeStatus);
            RefreshProcessPrograms();
        }, "S7F17 已删除但宿主列表刷新失败："));
    }

    private void ReportFormattedRecipeReceived(string ppid)
    {
        try { _formattedReceived?.Invoke(ppid); }
        catch (Exception ex) { AddLog("警告", $"配方 {ppid} 格式化状态通知失败：{ex.Message}"); }
    }

    private void ReportRecipeReceived(string ppid, string? error)
    {
        try { _recipeReceived?.Invoke(ppid, error); }
        catch (Exception ex) { AddLog("警告", $"配方 {ppid} 接收状态通知失败：{ex.Message}"); }
    }

    private void RunRecipe(Action action, string context = "")
    {
        if (_disposed) return;
        try { action(); }
        catch (Exception ex)
        {
            Model.RecipeStatus = $"失败：{context}{ex.Message}";
            AddLog("错误", Model.RecipeStatus);
        }
    }

    public void UseCurrentRecipePpid() => RunRecipe(() =>
    {
        var id = _currentHostPpid?.Invoke();
        if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("请先在宿主中手动切换当前配方。");
        ProcessProgramCommunication.ValidatePpid(id);
        Model.RecipePpid = id;
        Model.HostCurrentPpid = id;
        Model.RecipeStatus = "仅将宿主当前 PPID 填入测试输入，没有切换配方。";
    });

    public void ImportHostRecipes() => RunRecipe(() =>
    {
        Model.RecipeRequestSml = "";
        Model.RecipeJsonPreview = "";
        if (Model.IsConnected || _runtime.IsConnected) throw new InvalidOperationException("请先断开连接，再导入宿主配方到测试内存库。");
        if (_recipeIds is null || _readHostRecipe is null) throw new InvalidOperationException("宿主未提供配方数据源。");
        var snapshots = _recipeIds().Select(id =>
        {
            ProcessProgramCommunication.ValidatePpid(id);
            return (Id: id, Body: _readHostRecipe(id) ?? throw new InvalidOperationException($"配方 {id} 已不存在。"),
                Formatted: _readHostFormattedRecipe?.Invoke(id));
        }).ToArray(); // 两种表示全部转换和校验成功后才写入，避免编码失败留下半批数据。
        foreach (var snapshot in snapshots)
        {
            _runtime.StoreUnformattedProcessProgram(snapshot.Id, snapshot.Body);
            if (snapshot.Formatted != null)
            {
                _runtime.StoreFormattedProcessProgram(snapshot.Formatted);
                ReportFormattedRecipeReceived(snapshot.Id);
            }
        }
        RefreshProcessPrograms();
        Model.RecipeStatus = $"已导入 {snapshots.Length} 条宿主配方（UTF-8 JSON{(_readHostFormattedRecipe is null ? "" : " / 格式化正文")}），仅测试内存；未切换或应用配方。";
    });

    /// <summary>下载通知只更新配方库，绝不改变宿主的当前配方或已切换快照。</summary>
    private void SyncDownloadedRecipe(string ppid, byte[] body, bool publishFormatted = true) => RunRecipe(() =>
    {
        if (_updateHostRecipe is null)
        {
            Model.RecipeStatus = $"S7F3 PPID={ppid} 已接收原始字节，但尚未绑定宿主配方类型；绑定后补充刷新列表。";
            AddLog("提示", Model.RecipeStatus);
            return;
        }
        // 只同步确实入库且仍为最新的正文；迟到通知不能恢复已删除或覆盖的新版本。
        var stored = _runtime.GetUnformattedProcessProgram(ppid);
        if (stored is null || !stored.AsSpan().SequenceEqual(body)) return;
        _updateHostRecipe(ppid, body, publishFormatted); // 回填时不以旧二进制覆盖已接收的格式化正文。
        Model.RecipeRequestSml = "";
        Model.RecipeJsonPreview = "";
        Model.RecipeStatus = $"S7F3 PPID={ppid} 校验成功，配方管理列表已新增或更新；当前配方未切换。";
        AddLog("配方", Model.RecipeStatus);
    }, $"S7F3 PPID={ppid} 正文解析失败（已通知列表，不可切换）：");

    public void ReadRecipeToHost() => RunRecipe(() =>
    {
        Model.RecipeRequestSml = "";
        Model.RecipeJsonPreview = "";
        if (_updateHostRecipe is null) throw new InvalidOperationException("宿主未提供配方数据源。");
        var body = _runtime.GetUnformattedProcessProgram(Model.RecipePpid)
            ?? throw new InvalidOperationException("没有该 PPID 的二进制正文。");
        _updateHostRecipe(Model.RecipePpid, body, true);
        Model.RecipeJsonPreview = Encoding.UTF8.GetString(body);
        Model.RecipeStatus = "已读取并校验正文，更新宿主配方数据；未改变当前 PPID、未应用到设备。";
    });

    public void BuildRecipeRequest() => RunRecipe(() =>
    {
        Model.RecipeRequestSml = "";
        Model.RecipeJsonPreview = "";
        byte[]? body = null;
        if (Model.RecipeRequestIndex is 0 or 1)
            body = _readHostRecipe?.Invoke(Model.RecipePpid) ?? _runtime.GetUnformattedProcessProgram(Model.RecipePpid);
        using var message = ProcessProgramCommunication.CreateRequest(Model.RecipeRequestIndex, Model.RecipePpid, body, Model.RecipeFormattedSml);
        Model.RecipeRequestSml = message.ToSml();
        if (body != null) Model.RecipeJsonPreview = Encoding.UTF8.GetString(body);
        Model.RecipeStatus = Model.RecipeRequestIndex is 3 or 7
            ? "危险模板：外部 Host 发送后将删除内存配方；当前仅生成文本，没有发送或删除。"
            : "已生成 H→E 请求模板，预期 E→H 应答；请复制到外部 Host 工具发送。";
    });

    public void CopyRecipeRequest() => RunRecipe(() =>
    {
        if (string.IsNullOrWhiteSpace(Model.RecipeRequestSml)) return;
        System.Windows.Clipboard.SetText(Model.RecipeRequestSml);
        Model.RecipeStatus = "已复制 Host 请求模板，未发送。";
    });

    public void RefreshProcessPrograms()
    {
        if (_disposed) return;
        try
        {
            Model.HostCurrentPpid = _currentHostPpid?.Invoke() is { Length: > 0 } id ? id : "未选择";
            var selectedId = Model.SelectedProcessProgram?.PPID;
            var entries = _runtime.GetProcessPrograms();
            if (!Model.ProcessPrograms.SequenceEqual(entries))
            {
                Model.ProcessPrograms = entries;
                Model.SelectedProcessProgram = entries.FirstOrDefault(x => x.PPID == selectedId);
            }
            PreviewProcessProgram();
        }
        catch (Exception ex)
        {
            Model.ProcessPrograms = [];
            Model.SelectedProcessProgram = null;
            Model.ProcessProgramPreview = "配方目录读取失败。";
            AddLog("错误", $"读取 S7 目录: {ex.Message}");
        }
    }

    public void PreviewProcessProgram()
    {
        if (_disposed) return;
        try
        {
            var ppid = Model.SelectedProcessProgram?.PPID ?? Model.RecipePpid;
            Model.ProcessProgramPreview = string.IsNullOrEmpty(ppid)
                ? "请输入测试 PPID 查看只读 SML。"
                : _runtime.GetProcessProgramPreview(ppid);
        }
        catch (Exception ex)
        {
            Model.ProcessProgramPreview = $"读取配方失败: {ex.Message}";
            AddLog("错误", Model.ProcessProgramPreview);
        }
    }
}
