// ─────────────────────────────────────────────────────────────────────────────
// P6 —— DynamicDllInviNativeApi 迁移（来源：DPLibrary_Halcon_F48/Tools/AI/Invi/
// DynamicDllInviNativeApi.cs）。逐字迁移，仅改命名空间。
// ─────────────────────────────────────────────────────────────────────────────

using Extension.Platform.Native;
using System;

namespace Vision.AI.Invi.Native
{
    /// <summary>
    /// <see cref="IInviNativeApi"/> 的真实实现——用 <see cref="DynamicDllLoaderEx"/>
    /// 加载 INVI API.dll 并绑定 6 个导出函数。
    /// 构造期任何一步失败都抛带上下文的异常（DLL 名 / 阶段），并保留 InnerException。
    /// </summary>
    internal sealed class DynamicDllInviNativeApi : IInviNativeApi
    {
        /// <summary>厂商 native DLL 名（与现场部署一致，勿改）。</summary>
        public const string DllName = "INVI API.dll";

        private delegate IntPtr CreateInferManagerDelegate();

        private delegate bool DestroyInferManagerDelegate(IntPtr manager);

        private delegate bool CreateInferAPIDelegate(IntPtr manager, string modelPath, int type);

        private delegate IntPtr InferExeDelegate(IntPtr manager, string modelName, int type, int length,
            IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea);

        private delegate IntPtr InferIntPtrExeDelegate(IntPtr manager, string modelName, int type, int w, int h,
            int channel, IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea);

        private delegate bool DestroyInferDelegate(IntPtr manager, string modelPath);

        private readonly DynamicDllLoaderEx _loader;
        private readonly CreateInferManagerDelegate _createInferManager;
        private readonly DestroyInferManagerDelegate _destroyInferManager;
        private readonly CreateInferAPIDelegate _createInferApi;
        private readonly InferExeDelegate _inferExe;
        private readonly InferIntPtrExeDelegate _inferIntPtrExe;
        private readonly DestroyInferDelegate _destroyInfer;
        private bool _disposed;

        public DynamicDllInviNativeApi(string dllPath = DllName)
        {
            try
            {
                _loader = new DynamicDllLoaderEx(dllPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Invi native 初始化失败（阶段：加载 DLL \"{dllPath}\"）。", ex);
            }

            try
            {
                _createInferManager = _loader.GetFunction<CreateInferManagerDelegate>("CreateInferManager");
                _destroyInferManager = _loader.GetFunction<DestroyInferManagerDelegate>("DestroyInferManager");
                _createInferApi = _loader.GetFunction<CreateInferAPIDelegate>("CreateInferAPI");
                _inferExe = _loader.GetFunction<InferExeDelegate>("InferExe");
                _inferIntPtrExe = _loader.GetFunction<InferIntPtrExeDelegate>("InferExe_Intptr");
                _destroyInfer = _loader.GetFunction<DestroyInferDelegate>("DestoryInfer");
            }
            catch (Exception ex)
            {
                try { _loader.Dispose(); } catch { /* 绑定失败后 DLL 已不可用，释放失败无补救手段 */ }
                throw new InvalidOperationException(
                    $"Invi native 初始化失败（阶段：绑定 \"{dllPath}\" 的导出函数；InnerException 含缺失的函数名）。", ex);
            }
        }

        public IntPtr CreateManager() => _createInferManager();

        public bool DestroyManager(IntPtr manager) => _destroyInferManager(manager);

        public bool CreateModel(IntPtr manager, string modelPath, int type) =>
            _createInferApi(manager, modelPath, type);

        public bool DestroyModel(IntPtr manager, string modelPath) =>
            _destroyInfer(manager, modelPath);

        public IntPtr InferExe(IntPtr manager, string modelName, int type, int length, IntPtr imagePtr,
            int inputsize, double threshold, int batchSize, int mArea) =>
            _inferExe(manager, modelName, type, length, imagePtr, inputsize, threshold, batchSize, mArea);

        public IntPtr InferExeIntPtr(IntPtr manager, string modelName, int type, int w, int h, int channel,
            IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea) =>
            _inferIntPtrExe(manager, modelName, type, w, h, channel, imagePtr, inputsize, threshold, batchSize, mArea);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _loader.Dispose();
        }
    }
}
