using System;
using System.Collections.Generic;

namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    /// <summary>
    /// P10-4：<see cref="IInviNativeApi"/> 的可编程 fake。
    /// 只做「记录调用 + 按预设返回」，不模拟任何 native 真实行为——测试锁定的是
    /// <see cref="InviHelper"/> 的状态机（何时提交 managed 状态、何时回滚、何时幂等）。
    /// </summary>
    internal sealed class FakeInviNativeApi : IInviNativeApi
    {
        /// <summary>CreateManager 抛异常（模拟 native 初始化异常）。</summary>
        public bool ManagerCreationThrows { get; set; }

        /// <summary>CreateManager 返回 <see cref="IntPtr.Zero"/>（模拟创建失败）。</summary>
        public bool ManagerHandleIsZero { get; set; }

        /// <summary>CreateModel 抛异常。</summary>
        public bool ModelCreationThrows { get; set; }

        /// <summary>DestroyManager 的返回值。</summary>
        public bool DestroyManagerResult { get; set; } = true;

        /// <summary>这些路径的 CreateModel 返回 false（模拟加载失败）。</summary>
        public HashSet<string> CreateModelFailures { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>这些路径的 DestroyModel 返回 false（模拟卸载失败）。</summary>
        public HashSet<string> DestroyModelFailures { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>这些路径的 DestroyModel 抛异常。</summary>
        public HashSet<string> DestroyModelThrows { get; } = new HashSet<string>(StringComparer.Ordinal);

        public int CreateManagerCalls { get; private set; }

        public int DestroyManagerCalls { get; private set; }

        public List<string> CreatedModels { get; } = new List<string>();

        public List<string> DestroyedModels { get; } = new List<string>();

        public int DisposeCalls { get; private set; }

        public bool IsDisposed { get; private set; }

        public IntPtr CreateManager()
        {
            CreateManagerCalls++;
            if (ManagerCreationThrows)
                throw new InvalidOperationException("fake: CreateInferManager 抛出异常");
            return ManagerHandleIsZero ? IntPtr.Zero : new IntPtr(0x1234);
        }

        public bool DestroyManager(IntPtr manager)
        {
            DestroyManagerCalls++;
            return DestroyManagerResult;
        }

        public bool CreateModel(IntPtr manager, string modelPath, int type)
        {
            CreatedModels.Add(modelPath);
            if (ModelCreationThrows)
                throw new InvalidOperationException("fake: CreateInferAPI 抛出异常");
            return !CreateModelFailures.Contains(modelPath);
        }

        public bool DestroyModel(IntPtr manager, string modelPath)
        {
            DestroyedModels.Add(modelPath);
            if (DestroyModelThrows.Contains(modelPath))
                throw new InvalidOperationException("fake: DestoryInfer 抛出异常");
            return !DestroyModelFailures.Contains(modelPath);
        }

        public IntPtr InferExe(IntPtr manager, string modelName, int type, int length, IntPtr imagePtr,
            int inputsize, double threshold, int batchSize, int mArea) => IntPtr.Zero;

        public IntPtr InferExeIntPtr(IntPtr manager, string modelName, int type, int w, int h, int channel,
            IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea) => IntPtr.Zero;

        public void Dispose()
        {
            DisposeCalls++;
            IsDisposed = true;
        }
    }
}
