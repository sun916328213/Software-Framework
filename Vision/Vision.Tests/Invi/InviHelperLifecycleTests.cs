using DPLibrary_Halcon_F48.Tools.AI.Definition;
using DPLibrary_Halcon_F48.Tools.AI.Invi;
using System;
using Xunit;

namespace Vision.Tests.Invi
{
    /// <summary>
    /// P10-4：InviHelper 生命周期矩阵（不需要 INVI API.dll / GPU / 模型文件）。
    /// 对应计划 §7 测试矩阵：manager 创建成功/失败、模型创建成功/失败、失败后同路径重试、
    /// 重复加载、释放模型成功/失败、Dispose 幂等、销毁失败。
    /// </summary>
    public class InviHelperLifecycleTests
    {
        private const string ModelA = "C:/models/a.invi";
        private const string ModelB = "C:/models/b.invi";

        // ---------- Manager 创建 ----------

        /// <summary>矩阵：Manager 创建成功 → helper 可用 → InitModel 可继续。</summary>
        [Fact]
        public void ManagerCreated_HelperUsable_InitModelSucceeds()
        {
            var native = new FakeInviNativeApi();
            using var helper = new InviHelper(native);

            Assert.True(helper.HasManager);
            Assert.Equal(1, native.CreateManagerCalls);

            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            Assert.True(helper.IsModelLoaded(ModelA));
            Assert.Equal(1, helper.LoadedModelCount);
            Assert.Single(native.CreatedModels);
        }

        /// <summary>矩阵：Manager 创建失败（native 返回空句柄）→ 无已加载模型 + 明确异常，且不留半初始化对象。</summary>
        [Fact]
        public void ManagerCreationReturnsZero_ThrowsWithContext_AndReleasesNative()
        {
            var native = new FakeInviNativeApi { ManagerHandleIsZero = true };

            var ex = Assert.Throws<InvalidOperationException>(() => new InviHelper(native));

            Assert.Contains("CreateInferManager", ex.Message);
            Assert.Contains(DynamicDllInviNativeApi.DllName, ex.Message);
            Assert.Equal(1, native.DisposeCalls);
        }

        /// <summary>矩阵：Manager 创建抛异常 → 明确异常并保留根因（InnerException）。</summary>
        [Fact]
        public void ManagerCreationThrows_PreservesRootCause()
        {
            var native = new FakeInviNativeApi { ManagerCreationThrows = true };

            var ex = Assert.Throws<InvalidOperationException>(() => new InviHelper(native));

            Assert.Contains("CreateInferManager", ex.Message);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("fake:", ex.InnerException!.Message);
            Assert.Equal(1, native.DisposeCalls);
        }

        // ---------- 模型创建 ----------

        /// <summary>矩阵：模型创建成功 → managed 状态含该路径。</summary>
        [Fact]
        public void ModelCreationSucceeds_RegistersManagedState()
        {
            var native = new FakeInviNativeApi();
            using var helper = new InviHelper(native);

            Assert.True(helper.CreateInferAPI(ModelA, ToolType.Segmentation));

            Assert.True(helper.IsModelLoaded(ModelA));
            Assert.Equal(1, helper.LoadedModelCount);
        }

        /// <summary>
        /// 矩阵：模型创建失败 → managed 状态**不含**该路径，抛失败；修正后同路径可再次重试。
        /// 这一条正是旧实现的缺陷：先 TryAdd 再调 native，失败后同路径永远返回 false。
        /// </summary>
        [Fact]
        public void ModelCreationFails_LeavesNoManagedState_AndSamePathCanRetry()
        {
            var native = new FakeInviNativeApi();
            native.CreateModelFailures.Add(ModelA);
            using var helper = new InviHelper(native);

            Assert.Throws<InvalidOperationException>(() => helper.InitModel(ModelA, (int)ToolType.Segmentation));

            Assert.False(helper.IsModelLoaded(ModelA));
            Assert.Equal(0, helper.LoadedModelCount);

            // 修正参数（fake 放行）后同路径重试必须成功
            native.CreateModelFailures.Clear();
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            Assert.True(helper.IsModelLoaded(ModelA));
            Assert.Equal(2, native.CreatedModels.Count);
        }

        /// <summary>模型创建抛异常时同样不留 managed 状态。</summary>
        [Fact]
        public void ModelCreationThrows_LeavesNoManagedState()
        {
            var native = new FakeInviNativeApi { ModelCreationThrows = true };
            using var helper = new InviHelper(native);

            Assert.Throws<InvalidOperationException>(() => helper.InitModel(ModelA, (int)ToolType.Segmentation));

            Assert.Equal(0, helper.LoadedModelCount);
        }

        /// <summary>矩阵：重复加载同路径 → managed 状态仅一份，且不重复调用 native Create。</summary>
        [Fact]
        public void DuplicateLoad_SingleManagedEntry_WithoutExtraNativeCall()
        {
            var native = new FakeInviNativeApi();
            using var helper = new InviHelper(native);

            helper.InitModel(ModelA, (int)ToolType.Segmentation);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            Assert.Equal(1, helper.LoadedModelCount);
            Assert.Single(native.CreatedModels);
        }

        // ---------- 模型释放 ----------

        /// <summary>矩阵：释放模型成功 → 移除路径 → 之后允许重新加载。</summary>
        [Fact]
        public void ReleaseModelSucceeds_RemovesPath_AndAllowsReload()
        {
            var native = new FakeInviNativeApi();
            using var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            Assert.True(helper.ReleaseModel(ModelA));

            Assert.False(helper.IsModelLoaded(ModelA));
            Assert.Equal(0, helper.LoadedModelCount);

            helper.InitModel(ModelA, (int)ToolType.Segmentation);
            Assert.True(helper.IsModelLoaded(ModelA));
            Assert.Equal(2, native.CreatedModels.Count);
        }

        /// <summary>矩阵：释放模型失败（native 返回 false）→ 错误上抛，路径保持，不伪装为已释放。</summary>
        [Fact]
        public void ReleaseModelFails_KeepsManagedState_AndDoesNotPretendReleased()
        {
            var native = new FakeInviNativeApi();
            native.DestroyModelFailures.Add(ModelA);
            using var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            var ex = Assert.Throws<InvalidOperationException>(() => helper.ReleaseModel(ModelA));

            Assert.Contains("DestoryInfer", ex.Message);
            Assert.True(helper.IsModelLoaded(ModelA));
            Assert.Equal(1, helper.LoadedModelCount);
        }

        /// <summary>矩阵：释放模型抛异常 → 错误上抛并保留根因，路径保持。</summary>
        [Fact]
        public void ReleaseModelThrows_PreservesRootCause_AndKeepsManagedState()
        {
            var native = new FakeInviNativeApi();
            native.DestroyModelThrows.Add(ModelA);
            using var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            var ex = Assert.Throws<InvalidOperationException>(() => helper.ReleaseModel(ModelA));

            Assert.NotNull(ex.InnerException);
            Assert.True(helper.IsModelLoaded(ModelA));
        }

        /// <summary>边界：释放从未加载的路径直接返回 false，不触碰 native。</summary>
        [Fact]
        public void ReleaseModel_UnknownPath_ReturnsFalse_WithoutTouchingNative()
        {
            var native = new FakeInviNativeApi();
            using var helper = new InviHelper(native);

            Assert.False(helper.ReleaseModel(ModelA));
            Assert.Empty(native.DestroyedModels);
        }

        // ---------- ResetModel ----------

        /// <summary>ResetModel 全成功 → 返回空失败列表，managed 状态清空。</summary>
        [Fact]
        public void ResetModel_ReleasesAll_AndReportsNoFailure()
        {
            var native = new FakeInviNativeApi();
            using var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);
            helper.InitModel(ModelB, (int)ToolType.Classification);

            var failed = helper.ResetModel();

            Assert.Empty(failed);
            Assert.Equal(0, helper.LoadedModelCount);
            Assert.Equal(2, native.DestroyedModels.Count);
        }

        /// <summary>ResetModel 部分失败 → 失败路径保留在 managed 状态并出现在返回值里。</summary>
        [Fact]
        public void ResetModel_PartialFailure_KeepsFailedPathAndReportsIt()
        {
            var native = new FakeInviNativeApi();
            native.DestroyModelFailures.Add(ModelB);
            using var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);
            helper.InitModel(ModelB, (int)ToolType.Classification);

            var failed = helper.ResetModel();

            Assert.Equal(new[] { ModelB }, failed);
            Assert.False(helper.IsModelLoaded(ModelA));
            Assert.True(helper.IsModelLoaded(ModelB));
        }

        // ---------- Dispose ----------

        /// <summary>矩阵：Dispose 两次 → 幂等、不抛，DestroyManager 只调用一次，状态清空。</summary>
        [Fact]
        public void DisposeTwice_Idempotent_DestroysManagerOnce_AndClearsState()
        {
            var native = new FakeInviNativeApi();
            var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            helper.Dispose();
            helper.Dispose();

            Assert.Equal(1, native.DestroyManagerCalls);
            Assert.Equal(1, native.DisposeCalls);
            Assert.False(helper.HasManager);
            Assert.Equal(0, helper.LoadedModelCount);
            Assert.True(helper.IsDisposed);
        }

        /// <summary>矩阵：销毁失败 → Dispose 仍不抛，状态一律清空。</summary>
        [Fact]
        public void Dispose_WithDestroyManagerFailure_DoesNotThrow_AndClearsState()
        {
            var native = new FakeInviNativeApi { DestroyManagerResult = false };
            var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            helper.Dispose();

            Assert.False(helper.HasManager);
            Assert.Equal(0, helper.LoadedModelCount);
        }

        /// <summary>Dispose 时模型卸载失败 → best-effort：不抛，但管理器仍被销毁、状态仍被清空。</summary>
        [Fact]
        public void Dispose_WithModelReleaseFailure_DoesNotThrow_AndStillDestroysManager()
        {
            var native = new FakeInviNativeApi();
            native.DestroyModelFailures.Add(ModelA);
            var helper = new InviHelper(native);
            helper.InitModel(ModelA, (int)ToolType.Segmentation);

            helper.Dispose();

            Assert.Equal(1, native.DestroyManagerCalls);
            Assert.False(helper.HasManager);
            Assert.Equal(0, helper.LoadedModelCount);
        }

        /// <summary>Dispose 之后一切操作都抛 ObjectDisposedException（不会静默动到已销毁的 native）。</summary>
        [Fact]
        public void AfterDispose_OperationsThrowObjectDisposed()
        {
            var native = new FakeInviNativeApi();
            var helper = new InviHelper(native);
            helper.Dispose();

            Assert.Throws<ObjectDisposedException>(() => helper.InitModel(ModelA, (int)ToolType.Segmentation));
            Assert.Throws<ObjectDisposedException>(() => { helper.CreateInferAPI(ModelA, ToolType.Segmentation); });
            Assert.Throws<ObjectDisposedException>(() => { helper.ReleaseModel(ModelA); });
            Assert.Throws<ObjectDisposedException>(() => { helper.ResetModel(); });
        }
    }
}
