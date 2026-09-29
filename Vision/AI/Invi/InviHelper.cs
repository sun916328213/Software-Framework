// ─────────────────────────────────────────────────────────────────────────────
// P6 —— InviHelper 生命周期迁移（来源：DPLibrary_Halcon_F48/Tools/AI/Invi/InviHelper.cs）。
//
// 迁移映射（行为不变，只换边界）：
//   · 保留：构造（manager 创建失败抛上下文异常 + 句柄清理）、CreateInferAPI
//     （重复加载幂等、native 成功后才提交 managed 状态）、ReleaseModel
//     （失败保留状态并上抛）、ResetModel（返回未释放路径）、Dispose（幂等不抛）、
//     InferExe / InferExe_IntPtr 透传、诊断用的 HasManager / IsDisposed /
//     LoadedModelCount / IsModelLoaded。
//   · 新增：InitModel（加载失败抛异常，供 Provider 会话打开使用）、
//     InferResults（图像指针 → 解析后的 DTO 列表，供 Session 结果转换使用）。
//   · 不迁移：AIBase 基类、Inference(HObject)（绑定 HALCON 图像类型，计划 §6 点名要解耦；
//     轮廓→XLD 的 HALCON 结果构造一并退场， Core 结果改由 InviResultConverter 从 DTO 直转）。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Vision.AI.Invi.Native;

namespace Vision.AI.Invi
{
    /// <summary>
    /// 业视 native 生命周期拥有者（manager + 已加载模型集合）。
    /// 与旧实现相同的 P10 收口语义：成功后提交、失败回滚、释放失败保留状态。
    /// </summary>
    internal sealed class InviHelper : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IInviNativeApi _native;
        private readonly ConcurrentDictionary<string, bool> _loadedModels = new();
        private readonly object _modelGate = new();

        private IntPtr _inferManager;
        private bool _disposed;

        /// <summary>生产路径：加载真实 INVI API.dll。</summary>
        public InviHelper() : this(new DynamicDllInviNativeApi())
        {
        }

        /// <summary>
        /// 注入 native 适配层（测试传 fake）。管理器创建失败时抛带上下文的异常，
        /// 并释放已加载的 native 句柄，不留半初始化对象。
        /// </summary>
        internal InviHelper(IInviNativeApi native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));

            try
            {
                _inferManager = _native.CreateManager();
            }
            catch (Exception ex)
            {
                SafeDisposeNative();
                throw new InvalidOperationException(
                    $"Invi 推理管理器创建失败（阶段：CreateInferManager，DLL: {DynamicDllInviNativeApi.DllName}）。", ex);
            }

            if (_inferManager == IntPtr.Zero)
            {
                SafeDisposeNative();
                throw new InvalidOperationException(
                    $"Invi 推理管理器创建失败：CreateInferManager 返回空句柄（DLL: {DynamicDllInviNativeApi.DllName}）。");
            }
        }

        /// <summary>推理管理器句柄是否有效（诊断/测试用）。</summary>
        internal bool HasManager => _inferManager != IntPtr.Zero;

        /// <summary>是否已释放（诊断/测试用）。</summary>
        internal bool IsDisposed => _disposed;

        /// <summary>当前已加载模型数（诊断/测试用）。</summary>
        internal int LoadedModelCount => _loadedModels.Count;

        /// <summary>指定路径是否处于「已加载」managed 状态（诊断/测试用）。</summary>
        internal bool IsModelLoaded(string modelPath) =>
            !string.IsNullOrEmpty(modelPath) && _loadedModels.ContainsKey(modelPath);

        /// <summary>
        /// 初始化模型（加载失败抛异常；供 Provider 会话打开使用）。
        /// </summary>
        public void InitModel(string path, int type)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();

            if (!CreateInferAPI(path, type))
                throw new InvalidOperationException($"加载 Invi 模型失败：CreateInferAPI 返回 false（模型: {path}）。");
        }

        /// <summary>
        /// 确保指定模型已加载。同一路径重复调用幂等成功；native 失败不写 managed 状态。
        /// </summary>
        public bool CreateInferAPI(string modelPath, int type)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("modelPath 不能为空。", nameof(modelPath));

            lock (_modelGate)
            {
                if (_loadedModels.ContainsKey(modelPath))
                    return true;

                if (!_native.CreateModel(_inferManager, modelPath, type))
                    return false;

                _loadedModels[modelPath] = true;
                return true;
            }
        }

        /// <summary>
        /// 卸载单个模型。native 成功后才移除 managed 状态；失败保留状态并上抛。
        /// 未加载的路径直接返回 false，不触碰 native。
        /// </summary>
        public bool ReleaseModel(string modelPath)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("modelPath 不能为空。", nameof(modelPath));

            lock (_modelGate)
            {
                if (!_loadedModels.ContainsKey(modelPath))
                    return false;

                bool destroyed;
                try
                {
                    destroyed = _native.DestroyModel(_inferManager, modelPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"卸载 Invi 模型时 native 抛出异常（阶段：DestoryInfer，模型: {modelPath}）；managed 状态已保留。", ex);
                }

                if (!destroyed)
                {
                    throw new InvalidOperationException(
                        $"卸载 Invi 模型失败：DestoryInfer 返回 false（模型: {modelPath}）；managed 状态已保留，可重试或诊断。");
                }

                _loadedModels.TryRemove(modelPath, out _);
                return true;
            }
        }

        /// <summary>
        /// 释放全部已加载模型。返回未能释放的路径（空表示全部成功）。
        /// </summary>
        public IReadOnlyList<string> ResetModel()
        {
            ThrowIfDisposed();
            return ResetModelCore();
        }

        /// <summary>
        /// 推理并解析结果（图像指针由调用方 pin 住并保证在调用期间有效）。
        /// native 返回空指针/空串视为零检出（旧实现此处空引用崩溃，P6 改为返回空列表）。
        /// </summary>
        public List<InviResult> InferResults(string modelPath, int type, int width, int height, int channels,
            IntPtr imagePtr, double threshold, int inputSize = 512, int minArea = 100, int batchSize = 1)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();

            var resultHandle = _native.InferExeIntPtr(_inferManager, modelPath, type, width, height, channels,
                imagePtr, inputSize, threshold, batchSize, minArea);
            if (resultHandle == IntPtr.Zero)
                return new List<InviResult>();

            var json = Marshal.PtrToStringAnsi(resultHandle);
            if (string.IsNullOrWhiteSpace(json))
                return new List<InviResult>();

            return JsonSerializer.Deserialize<List<InviResult>>(json, JsonOptions) ?? new List<InviResult>();
        }

        public IntPtr InferExe(string modelName, int type, int length, IntPtr imagePtr, int inputsize = 512,
            int mArea = 100, double threshold = 0.5, int batchSize = 1)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();
            return _native.InferExe(_inferManager, modelName, type, length, imagePtr, inputsize, threshold, batchSize, mArea);
        }

        public IntPtr InferExeIntPtr(string modelName, int type, int w, int h, int channel, IntPtr imagePtr,
            int inputsize = 512, int mArea = 100, double threshold = 0.5, int batchSize = 1)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();
            return _native.InferExeIntPtr(_inferManager, modelName, type, w, h, channel, imagePtr, inputsize, threshold, batchSize, mArea);
        }

        /// <summary>
        /// 释放已加载模型并销毁 manager。best-effort、幂等、不抛。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try { ResetModelCore(); }
            catch { /* best-effort 卸载，异常已在内部按路径处理，此处兜底 */ }

            try
            {
                if (_inferManager != IntPtr.Zero)
                    _native.DestroyManager(_inferManager);
            }
            catch { /* Dispose 契约不抛 */ }
            finally
            {
                _inferManager = IntPtr.Zero;
                _loadedModels.Clear();
                SafeDisposeNative();
            }

            GC.SuppressFinalize(this);
        }

        private IReadOnlyList<string> ResetModelCore()
        {
            var failed = new List<string>();
            string[] paths = _loadedModels.Keys.ToArray();

            foreach (var path in paths)
            {
                try { ReleaseModelCore(path); }
                catch (Exception) { failed.Add(path); }
            }

            return failed;
        }

        private bool ReleaseModelCore(string modelPath)
        {
            lock (_modelGate)
            {
                if (!_loadedModels.ContainsKey(modelPath))
                    return false;

                bool destroyed;
                try
                {
                    destroyed = _native.DestroyModel(_inferManager, modelPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"卸载 Invi 模型时 native 抛出异常（阶段：DestoryInfer，模型: {modelPath}）；managed 状态已保留。", ex);
                }

                if (!destroyed)
                {
                    throw new InvalidOperationException(
                        $"卸载 Invi 模型失败：DestoryInfer 返回 false（模型: {modelPath}）；managed 状态已保留，可重试或诊断。");
                }

                _loadedModels.TryRemove(modelPath, out _);
                return true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InviHelper));
        }

        private void EnsureManagerUsable()
        {
            if (_inferManager == IntPtr.Zero)
                throw new InvalidOperationException("Invi 推理管理器不可用（未成功创建或已被销毁）。");
        }

        private void SafeDisposeNative()
        {
            try { _native.Dispose(); }
            catch { /* 清理路径：不覆盖原始异常 */ }
        }
    }
}
