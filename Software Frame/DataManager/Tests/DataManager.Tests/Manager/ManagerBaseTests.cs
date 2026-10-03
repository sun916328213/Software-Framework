// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ManagerBaseTests、FakeManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core。导入不代表所有类型都会在运行时加载。
using DataManager.Core;

namespace DataManager.Tests.Manager
{
    /// <summary>
    /// ManagerBase 通用行为测试（使用字符串模拟设备）
    /// </summary>
    public class ManagerBaseTests
    {
        /// <summary>
        /// FakeManager 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// 继承或实现的契约：ManagerBase&lt;string, string&gt;。具体约束按这些契约及各成员实现解释。
        /// </summary>
        private class FakeManager : ManagerBase<string, string>
        {
            /// <summary>
            /// 验证 Config 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
            /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
            /// </summary>
            public override void Config(List<string> param)
            {
                foreach (var item in param)
                    Create(item);
            }

            /// <summary>
            /// 验证 Create 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
            /// 正文中的关键协作调用包括 AddInternal；具体条件和先后次序见方法体。
            /// </summary>
            public override string Create(string param)
            {
                AddInternal(param);
                return param;
            }

            /// <summary>
            /// 验证 GetName 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
            /// </summary>
            protected override string GetName(string device) => device;
        }

        /// <summary>
        /// 验证 Add_IncreasesCount 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Add_IncreasesCount()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.Equal(1, manager.Count);
        }

        /// <summary>
        /// 验证 IndexerByName_ReturnsMatchedItem 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void IndexerByName_ReturnsMatchedItem()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            Assert.Equal("B", manager["B"]);
        }

        /// <summary>
        /// 验证 IndexerByName_ReturnsDefault_WhenNotFound 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void IndexerByName_ReturnsDefault_WhenNotFound()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.Null(manager["X"]);
        }

        /// <summary>
        /// 验证 IndexerByInt_ReturnsItemAtIndex 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void IndexerByInt_ReturnsItemAtIndex()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            Assert.Equal("A", manager[0]);
            Assert.Equal("B", manager[1]);
        }

        /// <summary>
        /// 验证 Remove_DecreasesCount 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Remove、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Remove_DecreasesCount()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Remove("A");
            Assert.Equal(0, manager.Count);
        }

        /// <summary>
        /// 验证 Clear_RemovesAll 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Clear、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Clear_RemovesAll()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            manager.Clear();
            Assert.Equal(0, manager.Count);
        }

        /// <summary>
        /// 验证 Config_CreatesAllItems 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Config、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Config_CreatesAllItems()
        {
            var manager = new FakeManager();
            manager.Config(new List<string> { "A", "B", "C" });
            Assert.Equal(3, manager.Count);
        }

        /// <summary>
        /// 验证 Add_DuplicateName_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Throws；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Add_DuplicateName_Throws()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.Throws<InvalidOperationException>(() => manager.Add("A"));
        }

        /// <summary>
        /// 验证 TryGet_FoundAndNotFound 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、True、TryGet、Equal、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void TryGet_FoundAndNotFound()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.True(manager.TryGet("A", out var a));
            Assert.Equal("A", a);
            Assert.False(manager.TryGet("X", out _));
        }

        /// <summary>
        /// 验证 Items_IsReadOnlySnapshot 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、Equal、Contains；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Items_IsReadOnlySnapshot()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            Assert.Equal(2, manager.Items.Count);
            Assert.Contains("A", manager.Items);
        }

        /// <summary>
        /// 验证 Remove_ReturnsTrueAndFalse 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Add、True、Remove、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Remove_ReturnsTrueAndFalse()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.True(manager.Remove("A"));
            Assert.False(manager.Remove("A"));
        }

        /// <summary>
        /// 验证 Add_ParallelUniqueNames_AllAdded 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 For、Add、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Add_ParallelUniqueNames_AllAdded()
        {
            var manager = new FakeManager();
            const int count = 100;
            Parallel.For(0, count, i => manager.Add($"P{i:000}"));
            Assert.Equal(count, manager.Count);
        }
    }
}
