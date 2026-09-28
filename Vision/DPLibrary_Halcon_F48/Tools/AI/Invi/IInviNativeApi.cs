using System;

namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    /// <summary>
    /// P10-3：Invi native 调用的最小适配面。
    /// 存在理由只有一个——让 <see cref="InviHelper"/> 的生命周期（管理器创建/销毁、模型加载/卸载、
    /// 失败回滚、Dispose 幂等）在没有 INVI API.dll、没有 GPU、没有模型文件的环境里也能被测试锁住。
    /// 刻意**不做**通用 native 抽象：只覆盖 <see cref="InviHelper"/> 当前真正用到的 6 个调用，
    /// 多一个都不加（避免为测试设计过度抽象）。
    /// </summary>
    internal interface IInviNativeApi : IDisposable
    {
        /// <summary>
        /// 创建推理管理器（导出函数 CreateInferManager）。
        /// 返回 <see cref="IntPtr.Zero"/> 表示 native 侧创建失败。
        /// </summary>
        IntPtr CreateManager();

        /// <summary>销毁推理管理器（导出函数 DestroyInferManager）。返回 false 表示 native 侧销毁失败。</summary>
        bool DestroyManager(IntPtr manager);

        /// <summary>加载模型（导出函数 CreateInferAPI）。返回 false 表示 native 侧加载失败。</summary>
        bool CreateModel(IntPtr manager, string modelPath, int type);

        /// <summary>
        /// 卸载模型（导出函数 DestoryInfer，拼写沿用厂商原始导出名，勿"修正"）。
        /// 返回 false 表示 native 侧卸载失败。
        /// </summary>
        bool DestroyModel(IntPtr manager, string modelPath);

        /// <summary>按内存长度推理（导出函数 InferExe）。</summary>
        IntPtr InferExe(IntPtr manager, string modelName, int type, int length, IntPtr imagePtr,
            int inputsize, double threshold, int batchSize, int mArea);

        /// <summary>按图像宽/高/通道推理（导出函数 InferExe_Intptr）。</summary>
        IntPtr InferExeIntPtr(IntPtr manager, string modelName, int type, int w, int h, int channel,
            IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea);
    }
}
