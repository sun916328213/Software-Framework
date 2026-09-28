using DPLibrary_Halcon_F48.Tools.AI.Definition;
using Extension.Core.Conversion;
using Extension.Core.Enums;
using Extension.Imaging.Halcon;
using Extension.Imaging.OpenCv;
using Extension.Platform.Diagnostics;
using Extension.Platform.Native;
using Extension.Platform.Memory;
using Extension.Platform.Processes;
using HalconDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using OpenCvSharp;

namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    /// <summary>
    /// 业视（Invi）AI 推理助手——DPLibrary_Halcon_F48 的**唯一正式 Invi 实现**
    /// （<see cref="AITool.Config"/> 只创建它；旧的 InvIHelpr1 / InvIClassTool / InvISegmentationTool /
    /// Vision.InvI.InvIToolMgr 已在 P10-2 退场，不再维护 InvI.dll 旧协议）。
    ///
    /// P10-3 生命周期收口：
    /// ① native 调用改走可注入的 <see cref="IInviNativeApi"/>（无 DLL 也能测状态机）；
    /// ② 模型注册改为 **native 成功后**才提交 managed 状态（旧实现先 TryAdd 再调 native，
    ///    失败会留下假加载状态，导致同路径重试永远返回 false）；
    /// ③ 新增 <see cref="ReleaseModel"/> / <see cref="ResetModel"/>，卸载成功才移除 managed 状态；
    /// ④ 实现 <see cref="IDisposable"/>：释放模型 + 销毁推理管理器，**幂等且不抛**；
    /// ⑤ 初始化失败不再空 catch——异常带 DLL 名 / 阶段并保留 InnerException。
    /// </summary>
    internal class InviHelper : AIBase, IDisposable
    {
        private readonly IInviNativeApi _native;

        /// <summary>
        /// 已加载模型集合（managed 状态）。语义：**存在即 native 侧已成功加载**。
        /// 只在 native 返回成功后写入，只在 native 卸载成功后移除。
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _loadedModels = new ConcurrentDictionary<string, bool>();

        /// <summary>保护「读 managed 状态 → 调 native → 提交 managed 状态」这段 check-then-act。</summary>
        private readonly object _modelGate = new object();

        private IntPtr _InferManager;
        private bool _disposed;

        private string _modelPath;
        private ToolType _type;
        int Decimal = 3;

        /// <summary>生产路径：加载真实 INVI API.dll。</summary>
        public InviHelper() : this(new DynamicDllInviNativeApi())
        {
        }

        /// <summary>
        /// P10-3：注入 native 适配层。测试传 fake，即可在没有 INVI API.dll / GPU / 模型文件的环境
        /// 验证创建失败、失败回滚、重复加载、释放失败、Dispose 幂等等全部生命周期分支。
        /// 管理器创建失败时抛带上下文的异常，并释放已加载的 native 句柄，不留半初始化对象。
        /// </summary>
        internal InviHelper(IInviNativeApi native)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));

            try
            {
                _InferManager = _native.CreateManager();
            }
            catch (Exception ex)
            {
                SafeDisposeNative();
                throw new InvalidOperationException(
                    $"Invi 推理管理器创建失败（阶段：CreateInferManager，DLL: {DynamicDllInviNativeApi.DllName}）。", ex);
            }

            if (_InferManager == IntPtr.Zero)
            {
                SafeDisposeNative();
                throw new InvalidOperationException(
                    $"Invi 推理管理器创建失败：CreateInferManager 返回空句柄（DLL: {DynamicDllInviNativeApi.DllName}）。");
            }
        }

        /// <summary>推理管理器句柄是否有效（诊断/测试用）。</summary>
        internal bool HasManager => _InferManager != IntPtr.Zero;

        /// <summary>是否已释放（诊断/测试用）。</summary>
        internal bool IsDisposed => _disposed;

        /// <summary>当前已加载模型数（诊断/测试用；不把集合本身暴露出去）。</summary>
        internal int LoadedModelCount => _loadedModels.Count;

        /// <summary>指定路径是否处于「已加载」managed 状态（诊断/测试用）。</summary>
        internal bool IsModelLoaded(string modelPath) =>
            !string.IsNullOrEmpty(modelPath) && _loadedModels.ContainsKey(modelPath);

        public override InferParam InferParam { get; set; }

        public override List<InferResultInfo> Inference(HObject image)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();

            List<InferResultInfo> inferResultInfos = new List<InferResultInfo>();
            List<InviResult> result;

            Stopwatch stopwatch = new Stopwatch();
            image.ToIntPtr(out IntPtr imageHandle, out int imageWidth, out int imageHeight, out int imageChannels);
            stopwatch.Start();
            var resultHandle = InferExe_IntPtr(_modelPath, (int)_type, imageWidth, imageHeight, imageChannels, imageHandle);
            stopwatch.Stop();
            var d111 = Marshal.PtrToStringAnsi(resultHandle);
            //数据转换
            result = JsonEx.FromJson<List<InviResult>>(d111);

            for (int i = 0; i < result.Count; i++)
            {
                var resultInfo = result[i];

                // 获取轮廓点集合
                HTuple rows = new HTuple(), cols = new HTuple();
                for (var vv = 0; vv < resultInfo.contour_points.Count; vv++)
                {
                    cols.Append(resultInfo.contour_points.ElementAt(vv).x);
                    rows.Append(resultInfo.contour_points.ElementAt(vv).y);
                }

                InferResult cInferResult = new InferResult(resultInfo.area, resultInfo.confidence, resultInfo.class_name, cols, rows);

                var d1 = JudgeParam(cInferResult, InferParam);
                if (d1 != null)
                {
                    InferResultInfo Contour = new InferResultInfo()
                    {
                        ContourInfo = d1,
                        DefectInfo = new DefectInfo
                        {
                            DefectName = resultInfo.class_name,
                            Area = Math.Round(cInferResult.area, Decimal),
                            Confidence = resultInfo.confidence,
                        }
                    };
                    inferResultInfos.Add(Contour);
                }
            }
            return inferResultInfos;
        }

        public override void InitModel(string path, int type)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();

            if (!CreateInferAPI(path, EnumEx.GetEnum<ToolType>(type.ToString())))
            {
                throw new InvalidOperationException($"加载 Invi 模型失败：CreateInferAPI 返回 false（模型: {path}）。");
            }

            _modelPath = path;
            _type = (ToolType)type;
        }

        /// <summary>
        /// 确保指定模型已加载。
        /// **P10 明确定义的语义**：同一路径重复调用是**幂等成功**——已加载时直接返回 true，
        /// 不再重复调用 native；native 创建失败时**不写入** managed 状态（旧实现先 TryAdd 再调 native，
        /// 失败会留下假加载状态，导致同路径重试永远返回 false）。
        /// </summary>
        public bool CreateInferAPI(string modelPath, ToolType type)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("modelPath 不能为空。", nameof(modelPath));

            lock (_modelGate)
            {
                if (_loadedModels.ContainsKey(modelPath))
                    return true;

                if (!_native.CreateModel(_InferManager, modelPath, (int)type))
                    return false;

                // native 成功后提交 managed 状态
                _loadedModels[modelPath] = true;
                return true;
            }
        }

        /// <summary>
        /// P10-3：卸载单个模型。native 成功后才从 managed 状态移除；
        /// native 返回 false 或抛异常时**保留** managed 状态（不伪装成已释放），异常向上抛并保留根因。
        /// 未处于已加载状态的路径直接返回 false，不触碰 native。
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
                    destroyed = _native.DestroyModel(_InferManager, modelPath);
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
        /// P10-3：释放全部已加载模型。
        /// 返回**未能释放**的模型路径（空表示全部成功）——失败路径保留在 managed 状态里供诊断与重试，
        /// 不会被伪装成已释放；单个路径的失败原因可再调 <see cref="ReleaseModel"/> 取回异常。
        /// </summary>
        public IReadOnlyList<string> ResetModel()
        {
            ThrowIfDisposed();
            return ResetModelCore();
        }

        public IntPtr InferExe(string modelName, int type, int length, IntPtr imagePtr, int inputsize = 512, int mARea = 100, double threshold = 0.5, int batch_size = 1)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();
            return _native.InferExe(_InferManager, modelName, type, length, imagePtr, inputsize, threshold, batch_size, mARea);
        }

        public IntPtr InferExe_IntPtr(string modelName, int type, int w, int h, int channel, IntPtr imagePtr, int inputsize = 512, int mARea = 100, double threshold = 0.5, int batch_size = 1)
        {
            ThrowIfDisposed();
            EnsureManagerUsable();
            return _native.InferExeIntPtr(_InferManager, modelName, type, w, h, channel, imagePtr, inputsize, threshold, batch_size, mARea);
        }

        /// <summary>
        /// P10-3：释放已加载模型并销毁 native 推理管理器。
        /// 策略：先逐个 best-effort 卸载模型（失败仅保留在 managed 状态里，不抛），再调用一次
        /// DestroyInferManager；**重复调用安全且不抛**（幂等）——Dispose 出现在 finally / 进程退出等
        /// 清理路径上，抛异常只会掩盖真正的错误。销毁失败同样不抛（对象状态一律清空）。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try { ResetModelCore(); }
            catch { /* ResetModelCore 只做 best-effort 卸载，异常已在其内部按路径吞并记录，此处兜底 */ }

            try
            {
                if (_InferManager != IntPtr.Zero)
                    _native.DestroyManager(_InferManager);
            }
            catch { /* Dispose 契约不抛：销毁失败无补救手段，状态清空优先 */ }
            finally
            {
                _InferManager = IntPtr.Zero;
                _loadedModels.Clear();
                SafeDisposeNative();
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>best-effort 卸载全部已加载模型，返回未能释放的路径。不检查 _disposed（供 Dispose 复用）。</summary>
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

        /// <summary>ReleaseModel 的无 _disposed 检查版本（Dispose 内部使用）。</summary>
        private bool ReleaseModelCore(string modelPath)
        {
            lock (_modelGate)
            {
                if (!_loadedModels.ContainsKey(modelPath))
                    return false;

                bool destroyed;
                try
                {
                    destroyed = _native.DestroyModel(_InferManager, modelPath);
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
            if (_InferManager == IntPtr.Zero)
                throw new InvalidOperationException("Invi 推理管理器不可用（未成功创建或已被销毁）。");
        }

        /// <summary>
        /// 释放 native 句柄（卸载 DLL）。仅在「构造失败」与「Dispose」两条清理路径上调用：
        /// 此处若抛异常会掩盖调用方正在处理的原始错误，故吞掉——native 卸载失败无补救手段。
        /// </summary>
        private void SafeDisposeNative()
        {
            try { _native.Dispose(); }
            catch { /* 清理路径：不覆盖原始异常 */ }
        }
    }
}
