// ─────────────────────────────────────────────────────────────────────────────
// P6 测试 fake —— IInviNativeApi 的可编程实现（延续 P10 FakeInviNativeApi 的思路：
// 只记录调用 + 按预设返回，不模拟 native 真实行为；推理返回测试构造的 JSON）。
// ─────────────────────────────────────────────────────────────────────────────

using System.Runtime.InteropServices;
using Vision.AI.Invi.Native;

namespace Vision.Tests.Tools.AI.Invi;

/// <summary>Invi native 可编程 fake（含 JSON 推理应答）。</summary>
internal sealed class FakeInviNativeApi : IInviNativeApi
{
    public bool ManagerCreationThrows { get; set; }

    public bool ManagerHandleIsZero { get; set; }

    public bool ModelCreationThrows { get; set; }

    public HashSet<string> CreateModelFailures { get; } = new(StringComparer.Ordinal);

    public HashSet<string> DestroyModelFailures { get; } = new(StringComparer.Ordinal);

    public HashSet<string> DestroyModelThrows { get; } = new(StringComparer.Ordinal);

    /// <summary>推理应答 JSON；null 表示返回空指针（零检出）。</summary>
    public string? InferJson { get; set; }

    public int CreateManagerCalls { get; private set; }

    public int DestroyManagerCalls { get; private set; }

    public List<string> CreatedModels { get; } = [];

    public List<string> DestroyedModels { get; } = [];

    public List<IntPtr> AllocatedJsonPtrs { get; } = [];

    public double LastThreshold { get; private set; } = double.NaN;

    public int LastWidth { get; private set; }

    public int LastHeight { get; private set; }

    public int DisposeCalls { get; private set; }

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
        return true;
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
        int inputsize, double threshold, int batchSize, int mArea) => AllocJson();

    public IntPtr InferExeIntPtr(IntPtr manager, string modelName, int type, int w, int h, int channel,
        IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea)
    {
        LastWidth = w;
        LastHeight = h;
        LastThreshold = threshold;
        return AllocJson();
    }

    public void Dispose() => DisposeCalls++;

    public void FreeJson()
    {
        foreach (var ptr in AllocatedJsonPtrs)
            Marshal.FreeHGlobal(ptr);
        AllocatedJsonPtrs.Clear();
    }

    private IntPtr AllocJson()
    {
        if (InferJson == null)
            return IntPtr.Zero;
        var ptr = Marshal.StringToHGlobalAnsi(InferJson);
        AllocatedJsonPtrs.Add(ptr);
        return ptr;
    }
}
