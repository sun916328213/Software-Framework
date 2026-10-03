// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：DynamicDllInviNativeApi。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.AI.Invi、Extension.Platform.Native、System。导入不代表所有类型都会在运行时加载。
using Vision.AI.Invi;
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

        /// <summary>
        /// 初始化 DynamicDllInviNativeApi 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 GetFunction、Dispose；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateManager。
        /// 正文中的关键协作调用包括 _createInferManager；具体条件和先后次序见方法体。
        /// </summary>
        public IntPtr CreateManager() => _createInferManager();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DestroyManager。
        /// 正文中的关键协作调用包括 _destroyInferManager；具体条件和先后次序见方法体。
        /// </summary>
        public bool DestroyManager(IntPtr manager) => _destroyInferManager(manager);

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateModel。
        /// 正文中的关键协作调用包括 _createInferApi；具体条件和先后次序见方法体。
        /// </summary>
        public bool CreateModel(IntPtr manager, string modelPath, int type) =>
            _createInferApi(manager, modelPath, type);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DestroyModel。
        /// 正文中的关键协作调用包括 _destroyInfer；具体条件和先后次序见方法体。
        /// </summary>
        public bool DestroyModel(IntPtr manager, string modelPath) =>
            _destroyInfer(manager, modelPath);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 InferExe。
        /// 正文中的关键协作调用包括 _inferExe；具体条件和先后次序见方法体。
        /// </summary>
        public IntPtr InferExe(IntPtr manager, string modelName, int type, int length, IntPtr imagePtr,
            int inputsize, double threshold, int batchSize, int mArea) =>
            _inferExe(manager, modelName, type, length, imagePtr, inputsize, threshold, batchSize, mArea);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 InferExeIntPtr。
        /// 正文中的关键协作调用包括 _inferIntPtrExe；具体条件和先后次序见方法体。
        /// </summary>
        public IntPtr InferExeIntPtr(IntPtr manager, string modelName, int type, int w, int h, int channel,
            IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea) =>
            _inferIntPtrExe(manager, modelName, type, w, h, channel, imagePtr, inputsize, threshold, batchSize, mArea);

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _loader.Dispose();
        }
    }
}
