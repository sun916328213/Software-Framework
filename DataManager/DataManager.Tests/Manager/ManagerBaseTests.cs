using DataManager;

namespace DataManager.Tests.Manager
{
    /// <summary>
    /// ManagerBase 通用行为测试（使用字符串模拟设备）
    /// </summary>
    public class ManagerBaseTests
    {
        private class FakeManager : ManagerBase<string, string>
        {
            public override void Config(List<string> param)
            {
                foreach (var item in param)
                    Create(item);
            }

            public override string Create(string param)
            {
                DataManagers.Add(param);
                return param;
            }

            protected override string GetName(string device) => device;
        }

        [Fact]
        public void Add_IncreasesCount()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.Equal(1, manager.Count);
        }

        [Fact]
        public void IndexerByName_ReturnsMatchedItem()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            Assert.Equal("B", manager["B"]);
        }

        [Fact]
        public void IndexerByName_ReturnsDefault_WhenNotFound()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.Null(manager["X"]);
        }

        [Fact]
        public void IndexerByInt_ReturnsItemAtIndex()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            Assert.Equal("A", manager[0]);
            Assert.Equal("B", manager[1]);
        }

        [Fact]
        public void Remove_DecreasesCount()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Remove("A");
            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void Clear_RemovesAll()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            manager.Clear();
            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void Config_CreatesAllItems()
        {
            var manager = new FakeManager();
            manager.Config(new List<string> { "A", "B", "C" });
            Assert.Equal(3, manager.Count);
        }

        [Fact]
        public void Add_DuplicateName_Throws()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.Throws<InvalidOperationException>(() => manager.Add("A"));
        }

        [Fact]
        public void TryGet_FoundAndNotFound()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.True(manager.TryGet("A", out var a));
            Assert.Equal("A", a);
            Assert.False(manager.TryGet("X", out _));
        }

        [Fact]
        public void Items_IsReadOnlySnapshot()
        {
            var manager = new FakeManager();
            manager.Add("A");
            manager.Add("B");
            Assert.Equal(2, manager.Items.Count);
            Assert.Contains("A", manager.Items);
        }

        [Fact]
        public void Remove_ReturnsTrueAndFalse()
        {
            var manager = new FakeManager();
            manager.Add("A");
            Assert.True(manager.Remove("A"));
            Assert.False(manager.Remove("A"));
        }

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
