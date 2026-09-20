using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Extension.Core.Collections;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「集合」专项：<c>Extension.Core.Collections</c>（<see cref="ListEx"/>、
    /// <see cref="ConcurrentList{T}"/>、<see cref="RangeObservableCollection{T}"/>）。
    /// 全部零依赖、无 IO，但此前**零用例覆盖**。
    /// </summary>
    public class CollectionsTests
    {
        // ══════════════════════════════════════════════════════════════════
        // ListEx.Equal —— 忽略顺序的比较
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ListEx_Equal_忽略顺序()
        {
            Assert.True(new List<int> { 1, 2, 3 }.Equal(new List<int> { 3, 2, 1 }));
            Assert.True(new List<int> { 1, 2, 3 }.Equal(new List<int> { 1, 2, 3 }));
        }

        [Fact]
        public void ListEx_Equal_重复元素按出现次数比较()
        {
            Assert.True(new List<int> { 1, 1, 2 }.Equal(new List<int> { 2, 1, 1 }));
            // 元素集合相同但计数不同 ⇒ 不相等（这正是它比 HashSet 比较强的地方）
            Assert.False(new List<int> { 1, 1, 2 }.Equal(new List<int> { 1, 2, 2 }));
            Assert.False(new List<int> { 1, 1, 2 }.Equal(new List<int> { 1, 2 }));
        }

        [Fact]
        public void ListEx_Equal_长度不同直接false()
        {
            Assert.False(new List<int> { 1, 2 }.Equal(new List<int> { 1, 2, 3 }));
            Assert.False(new List<int> { 1, 2, 3 }.Equal(new List<int> { 1, 2 }));
        }

        [Fact]
        public void ListEx_Equal_空列表()
        {
            Assert.True(new List<int>().Equal(new List<int>()));
            Assert.False(new List<int>().Equal(new List<int> { 1 }));
        }

        [Fact]
        public void ListEx_Equal_字符串走默认比较器_区分大小写()
        {
            Assert.True(new List<string> { "a", "b" }.Equal(new List<string> { "b", "a" }));
            Assert.False(new List<string> { "a" }.Equal(new List<string> { "A" }));
        }

        [Fact]
        public void ListEx_Equal_null元素会抛异常_是已知限制()
        {
            // ⚠️ 实现是 GroupBy(x => x).ToDictionary(g => g.Key, ...)，而 Dictionary 不允许 null 键
            //    ⇒ 列表里含 null 元素时抛 ArgumentNullException（具体类型随 BCL 实现，故用 ThrowsAny）。
            var ex = Record.Exception(() => new List<string> { null }.Equal(new List<string> { null }));
            Assert.NotNull(ex);
        }

        [Fact]
        public void ListEx_Equal_null参数是NullReferenceException()
        {
            // 没有做参数校验：第一行就是 list1.Count
            Assert.Throws<NullReferenceException>(() => ((List<int>)null).Equal(new List<int>()));
            Assert.Throws<NullReferenceException>(() => new List<int>().Equal(null));
        }

        // ══════════════════════════════════════════════════════════════════
        // ConcurrentList<T> —— 基本语义
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void ConcurrentList_增删改查()
        {
            var list = new ConcurrentList<int>();
            Assert.Empty(list);
            Assert.False(list.IsReadOnly);

            list.Add(1);
            list.Add(2);
            Assert.Equal(2, list.Count);
            Assert.Contains(1, list);
            Assert.Equal(1, list.IndexOf(2));
            Assert.Equal(1, list[0]);

            list[0] = 9;
            Assert.Equal(9, list[0]);

            list.Insert(0, 5);
            Assert.Equal(new[] { 5, 9, 2 }, list.ToArray());

            Assert.True(list.Remove(9));
            Assert.False(list.Remove(12345));       // 不存在 ⇒ false，不抛
            Assert.Equal(new[] { 5, 2 }, list.ToArray());

            list.RemoveAt(0);
            Assert.Equal(new[] { 2 }, list.ToArray());

            list.Clear();
            Assert.Empty(list);
        }

        [Fact]
        public void ConcurrentList_构造可带初始集合_null等价于空()
        {
            Assert.Equal(new[] { 1, 2, 3 }, new ConcurrentList<int>(new[] { 1, 2, 3 }).ToArray());
            Assert.Empty(new ConcurrentList<int>(null));
        }

        [Fact]
        public void ConcurrentList_AddRange_与null校验()
        {
            var list = new ConcurrentList<int>();
            list.AddRange(new[] { 1, 2, 3 });
            Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());

            Assert.Throws<ArgumentNullException>(() => list.AddRange(null));
        }

        [Fact]
        public void ConcurrentList_索引越界_读返回默认值_写静默忽略()
        {
            // ⚠️ 与 List<T> 语义不同：List<T> 越界会抛 ArgumentOutOfRangeException，
            //    这里读越界返回 default(T)、写越界**静默丢弃**。
            //    这会让"写错下标"的 bug 变成静默的数据丢失，用测试钉住现行为。
            var list = new ConcurrentList<int>(new[] { 1, 2 });

            Assert.Equal(0, list[-1]);          // default(int)
            Assert.Equal(0, list[99]);

            list[99] = 123;                     // 静默忽略
            Assert.Equal(2, list.Count);
            Assert.Equal(new[] { 1, 2 }, list.ToArray());

            // 引用类型同理返回 null
            var strs = new ConcurrentList<string>(new[] { "a" });
            Assert.Null(strs[42]);
        }

        [Fact]
        public void ConcurrentList_Insert与RemoveAt越界会抛()
        {
            var list = new ConcurrentList<int>(new[] { 1, 2 });
            Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(3, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(-1, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(-1));

            list.Insert(2, 9);                  // 末尾插入是合法的
            Assert.Equal(new[] { 1, 2, 9 }, list.ToArray());
        }

        [Fact]
        public void ConcurrentList_CopyTo()
        {
            var list = new ConcurrentList<int>(new[] { 1, 2, 3 });
            var target = new int[5];
            list.CopyTo(target, 1);
            Assert.Equal(new[] { 0, 1, 2, 3, 0 }, target);
        }

        [Fact]
        public void ConcurrentList_枚举是快照_枚举过程中新增不影响本次枚举()
        {
            var list = new ConcurrentList<int>();
            list.Add(1);
            list.Add(2);

            var seen = new List<int>();
            foreach (var value in list)
            {
                seen.Add(value);
                if (seen.Count == 1) list.Add(3);   // 枚举中修改（快照语义下不会 InvalidOperationException）
            }

            Assert.Equal(new[] { 1, 2 }, seen);
            Assert.Equal(3, list.Count);
        }

        [Fact]
        public void ConcurrentList_多线程并发Add不丢数据()
        {
            var list = new ConcurrentList<int>();
            const int perThread = 250;

            Parallel.For(0, 4, _ =>
            {
                for (int i = 0; i < perThread; i++)
                    list.Add(i);
            });

            Assert.Equal(4 * perThread, list.Count);
        }

        [Fact]
        public void ConcurrentList_Dispose后所有成员抛ObjectDisposedException()
        {
            var list = new ConcurrentList<int>(new[] { 1 });
            list.Dispose();

            Assert.Throws<ObjectDisposedException>(() => list.Add(2));
            Assert.Throws<ObjectDisposedException>(() => list.Clear());
            Assert.Throws<ObjectDisposedException>(() => list.Contains(1));
            Assert.Throws<ObjectDisposedException>(() => list.IndexOf(1));
            Assert.Throws<ObjectDisposedException>(() => list.Insert(0, 1));
            Assert.Throws<ObjectDisposedException>(() => list.RemoveAt(0));
            Assert.Throws<ObjectDisposedException>(() => list.AddRange(new[] { 1 }));
            Assert.Throws<ObjectDisposedException>(() => list.CopyTo(new int[1], 0));
            Assert.Throws<ObjectDisposedException>(() => list[0]);
            Assert.Throws<ObjectDisposedException>(() => list.Count);
        }

        [Fact]
        public void ConcurrentList_GetEnumerator是迭代器方法_Dispose后取枚举器不抛_枚举时才抛()
        {
            // ⚠️ 细节：GetEnumerator 含 yield return ⇒ 整个方法体（含 _disposed 检查）**延迟到
            //    第一次 MoveNext 才执行**。所以"取枚举器"这一步不抛，foreach 才抛。
            var list = new ConcurrentList<int>(new[] { 1 });
            list.Dispose();

            var enumerator = list.GetEnumerator();   // 不抛
            Assert.NotNull(enumerator);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                foreach (var _ in list) { }
            });
        }

        [Fact]
        public void ConcurrentList_Remove依赖IndexOf_先查再删()
        {
            // 实现是 IndexOf + RemoveAt（两次加锁），不是原子操作。
            // 单线程语义正确；这里只钉住"删的是第一个匹配项"。
            var list = new ConcurrentList<string>(new[] { "a", "b", "a" });
            Assert.True(list.Remove("a"));
            Assert.Equal(new[] { "b", "a" }, list.ToArray());
        }

        // ══════════════════════════════════════════════════════════════════
        // RangeObservableCollection<T> —— 批量操作只触发一次 Reset
        // ══════════════════════════════════════════════════════════════════

        private static (RangeObservableCollection<int> Col, Counter Counter) NewObserved(params int[] seed)
        {
            var col = seed.Length == 0
                ? new RangeObservableCollection<int>()
                : new RangeObservableCollection<int>(seed);

            var counter = new Counter();
            col.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Reset) counter.Resets++;
                else counter.Items++;
            };
            // ObservableCollection<T> 是**显式**实现 INotifyPropertyChanged.PropertyChanged 的，
            // 必须转接口才能订阅。
            ((INotifyPropertyChanged)col).PropertyChanged += (_, e) => counter.PropertyNames.Add(e.PropertyName);
            return (col, counter);
        }

        private sealed class Counter
        {
            public int Resets;
            public int Items;
            public List<string> PropertyNames { get; } = new List<string>();
        }

        [Fact]
        public void AddRange_只触发一次Reset_逐项Add被抑制()
        {
            var (col, counter) = NewObserved();

            col.AddRange(new[] { 1, 2, 3 });

            Assert.Equal(3, col.Count);
            Assert.Equal(1, counter.Resets);       // 一次 Reset
            Assert.Equal(0, counter.Items);        // 3 次 Add 通知全被抑制
        }

        [Fact]
        public void AddRange_空集合不触发任何通知()
        {
            var (col, counter) = NewObserved();

            col.AddRange(new int[0]);

            Assert.Equal(0, counter.Resets);
            Assert.Equal(0, counter.Items);
        }

        [Fact]
        public void AddRange_null抛ArgumentNullException()
        {
            var (col, _) = NewObserved();
            Assert.Throws<ArgumentNullException>(() => col.AddRange(null));
        }

        [Fact]
        public void AddRange_结束后补发Count与Item的PropertyChanged()
        {
            var (col, counter) = NewObserved();

            col.AddRange(new[] { 1, 2 });

            Assert.Contains(nameof(col.Count), counter.PropertyNames);
            Assert.Contains("Item[]", counter.PropertyNames);
        }

        [Fact]
        public void RemoveRange_只触发一次Reset()
        {
            var (col, counter) = NewObserved(1, 2, 3);

            col.RemoveRange(new[] { 1, 3 });

            Assert.Equal(new[] { 2 }, col);
            Assert.Equal(1, counter.Resets);
            Assert.Equal(0, counter.Items);
        }

        [Fact]
        public void ReplaceAll_先清空再填充_只触发一次Reset()
        {
            var (col, counter) = NewObserved(1, 2);

            col.ReplaceAll(new[] { 7, 8, 9 });

            Assert.Equal(new[] { 7, 8, 9 }, col);
            Assert.Equal(1, counter.Resets);
        }

        [Fact]
        public void 嵌套抑制_只有最外层结束才触发一次Reset()
        {
            var (col, counter) = NewObserved();

            using (col.SuppressNotifications())
            {
                col.Add(1);
                Assert.True(col.IsSuppressing);

                using (col.SuppressNotifications())
                {
                    col.Add(2);
                }

                Assert.True(col.IsSuppressing);    // 内层结束不该解除
                Assert.Equal(0, counter.Resets);
            }

            Assert.False(col.IsSuppressing);
            Assert.Equal(1, counter.Resets);       // 全程只发一次
            Assert.Equal(2, col.Count);
        }

        [Fact]
        public void 抑制期间即使只Add一个元素_结束后也只发Reset()
        {
            var (col, counter) = NewObserved();

            using (col.SuppressNotifications())
            {
                col.Add(1);
            }

            Assert.Equal(1, counter.Resets);
            Assert.Equal(0, counter.Items);
        }

        [Fact]
        public void 未抑制时走基类行为_逐项通知()
        {
            var (col, counter) = NewObserved();

            col.Add(1);
            col.Add(2);

            Assert.Equal(0, counter.Resets);
            Assert.Equal(2, counter.Items);        // 基类逐项 Add 通知
        }

        [Fact]
        public void 抑制期间清空_结束时不因_hasChanges为false而漏发()
        {
            // Clear() 本身在抑制下也会把 _hasChanges 置 true
            var (col, counter) = NewObserved(1, 2);

            using (col.SuppressNotifications())
            {
                col.Clear();
            }

            Assert.Empty(col);
            Assert.Equal(1, counter.Resets);
        }
    }
}
