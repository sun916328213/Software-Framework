using DPLibrary_Halcon_F48.Tools.AI;
using DPLibrary_Halcon_F48.Tools.AI.Definition;
using DPLibrary_Halcon_F48.Tools.AI.Invi;
using HalconDotNet;
using System;
using System.Collections.Generic;
using Xunit;

namespace Vision.Tests.Invi
{
    /// <summary>
    /// P10-4：AITool 配置入口——重复 Config 必须释放旧 helper；Invi 品牌恒用 <see cref="InviHelper"/>。
    /// 通过覆写 <see cref="AITool.CreateAIHelper"/> 注入 fake，所以不需要 INVI API.dll。
    /// </summary>
    public class AIToolConfigTests
    {
        /// <summary>矩阵：重复 Config → 旧 helper 已 Dispose，只保留一个在用。</summary>
        [Fact]
        public void RepeatedConfig_DisposesPreviousHelper_KeepsOnlyOne()
        {
            var tool = new RecordingAITool();
            tool.ModelPath = "model_a";

            tool.Config();
            var first = Assert.Single(tool.Helpers);
            Assert.Equal(new[] { "model_a" }, first.InitModelCalls);
            Assert.Same(first, tool.CurrentAIHelper);

            tool.ModelPath = "model_b";
            tool.Config();

            Assert.Equal(2, tool.Helpers.Count);
            Assert.Equal(1, first.DisposeCalls);              // 旧 helper 被释放
            Assert.Equal(0, tool.Helpers[1].DisposeCalls);    // 新的仍在用
            Assert.Equal(new[] { "model_b" }, tool.Helpers[1].InitModelCalls);
            Assert.Same(tool.Helpers[1], tool.CurrentAIHelper);
        }

        /// <summary>Config 初始化失败 → 失败的 helper 被回滚释放，对外状态回到「未初始化」。</summary>
        [Fact]
        public void Config_InitFailure_RollsBackFailedHelper_AndLeavesNoHelper()
        {
            var tool = new RecordingAITool { FailInitModel = true };
            tool.ModelPath = "model_a";

            Assert.Throws<InvalidOperationException>(() => tool.Config());

            var failed = Assert.Single(tool.Helpers);
            Assert.Equal(1, failed.DisposeCalls);   // 不留残留 native manager
            Assert.Null(tool.CurrentAIHelper);
        }

        /// <summary>Invi 品牌的正式入口是 InviHelper（不是任何旧实现类）。</summary>
        [Fact]
        public void InviBrand_CreatesInviHelper()
        {
            var tool = new RecordingAITool { UseRealFactory = true };

            IAI helper;
            try
            {
                helper = tool.CreateAIHelper();
            }
            catch (Exception ex)
            {
                // 本机没有 INVI API.dll：构造失败本身就证明走的是真实 InviHelper
                // （旧的 InvIClassTool / InvISegmentationTool 完全不需要这个 DLL）
                Assert.Contains(DynamicDllInviNativeApi.DllName, ex.Message);
                return;
            }

            Assert.IsType<InviHelper>(helper);
            ((IDisposable)helper).Dispose();
        }
    }

    /// <summary>把 <see cref="AITool.CreateAIHelper"/> 换成可记录的 fake。</summary>
    internal sealed class RecordingAITool : AITool
    {
        public List<RecordingHelper> Helpers { get; } = new List<RecordingHelper>();

        /// <summary>true = 走生产路径（真实 InviHelper）。</summary>
        public bool UseRealFactory { get; set; }

        /// <summary>true = 新建的 fake helper 在 InitModel 时抛异常。</summary>
        public bool FailInitModel { get; set; }

        internal override IAI CreateAIHelper()
        {
            if (UseRealFactory)
                return base.CreateAIHelper();

            var helper = new RecordingHelper { InitModelThrows = FailInitModel };
            Helpers.Add(helper);
            return helper;
        }
    }

    /// <summary>记录 InitModel 调用与 Dispose 次数的 IAI fake。</summary>
    internal sealed class RecordingHelper : IAI, IDisposable
    {
        public InferParam InferParam { get; set; } = new InferParam();

        public bool InitModelThrows { get; set; }

        public List<string> InitModelCalls { get; } = new List<string>();

        public int DisposeCalls { get; private set; }

        public List<InferResultInfo> Inference(HObject image) => new List<InferResultInfo>();

        public void InitModel(string path, int type)
        {
            InitModelCalls.Add(path);
            if (InitModelThrows)
                throw new InvalidOperationException("fake helper: InitModel 失败");
        }

        public void Dispose() => DisposeCalls++;
    }
}
