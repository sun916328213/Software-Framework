// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FakeInviNativeApi。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Tests.Tools.AI、System.Runtime.InteropServices、Vision.AI.Invi.Native。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
using Vision.Tests.Tools.AI;
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

    /// <summary>
    /// 验证 CreateManager 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    public IntPtr CreateManager()
    {
        CreateManagerCalls++;
        if (ManagerCreationThrows)
            throw new InvalidOperationException("fake: CreateInferManager 抛出异常");
        return ManagerHandleIsZero ? IntPtr.Zero : new IntPtr(0x1234);
    }

    /// <summary>
    /// 验证 DestroyManager 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    public bool DestroyManager(IntPtr manager)
    {
        DestroyManagerCalls++;
        return true;
    }

    /// <summary>
    /// 验证 CreateModel 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Contains；具体条件和先后次序见方法体。
    /// </summary>
    public bool CreateModel(IntPtr manager, string modelPath, int type)
    {
        CreatedModels.Add(modelPath);
        if (ModelCreationThrows)
            throw new InvalidOperationException("fake: CreateInferAPI 抛出异常");
        return !CreateModelFailures.Contains(modelPath);
    }

    /// <summary>
    /// 验证 DestroyModel 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Contains；具体条件和先后次序见方法体。
    /// </summary>
    public bool DestroyModel(IntPtr manager, string modelPath)
    {
        DestroyedModels.Add(modelPath);
        if (DestroyModelThrows.Contains(modelPath))
            throw new InvalidOperationException("fake: DestoryInfer 抛出异常");
        return !DestroyModelFailures.Contains(modelPath);
    }

    /// <summary>
    /// 验证 InferExe 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AllocJson；具体条件和先后次序见方法体。
    /// </summary>
    public IntPtr InferExe(IntPtr manager, string modelName, int type, int length, IntPtr imagePtr,
        int inputsize, double threshold, int batchSize, int mArea) => AllocJson();

    /// <summary>
    /// 验证 InferExeIntPtr 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AllocJson；具体条件和先后次序见方法体。
    /// </summary>
    public IntPtr InferExeIntPtr(IntPtr manager, string modelName, int type, int w, int h, int channel,
        IntPtr imagePtr, int inputsize, double threshold, int batchSize, int mArea)
    {
        LastWidth = w;
        LastHeight = h;
        LastThreshold = threshold;
        return AllocJson();
    }

    /// <summary>
    /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    public void Dispose() => DisposeCalls++;

    /// <summary>
    /// 验证 FreeJson 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FreeHGlobal、Clear；具体条件和先后次序见方法体。
    /// </summary>
    public void FreeJson()
    {
        foreach (var ptr in AllocatedJsonPtrs)
            Marshal.FreeHGlobal(ptr);
        AllocatedJsonPtrs.Clear();
    }

    /// <summary>
    /// 验证 AllocJson 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StringToHGlobalAnsi、Add；具体条件和先后次序见方法体。
    /// </summary>
    private IntPtr AllocJson()
    {
        if (InferJson == null)
            return IntPtr.Zero;
        var ptr = Marshal.StringToHGlobalAnsi(InferJson);
        AllocatedJsonPtrs.Add(ptr);
        return ptr;
    }
}
