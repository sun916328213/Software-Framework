// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SecsOrderBaseTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core.Order、SecsGem.Runtime.Order、Secs4Net、Xunit。导入不代表所有类型都会在运行时加载。
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using Secs4Net;
using Xunit;

namespace SecsGem.Tests
{
    /// <summary>
    /// SecsOrderBase 数据组包/解析纯逻辑测试（不依赖网络与容器）
    /// </summary>
    public class SecsOrderBaseTests
    {
        private readonly SecsOrderBase _order = new SecsOrderBase();

        // ── AddData 组包 ──

        [Fact]
        public void AddData_ASCII_ReturnsAsciiItem()
        {
            var item = _order.AddData("HELLO", SecsFormat.ASCII);
            Assert.Equal("HELLO", item.GetString());
        }

        /// <summary>
        /// 验证 AddData_ASCII_Null_ReturnsEmptyAscii 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 AddData、Equal、GetString；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AddData_ASCII_Null_ReturnsEmptyAscii()
        {
            var item = _order.AddData(null, SecsFormat.ASCII);
            Assert.Equal(string.Empty, item.GetString());
        }

        /// <summary>
        /// 验证 AddData_Unsigned_RoundTrip 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 AddData、Equal、ToString、SecsItemToList；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData(SecsFormat.U1, (byte)200)]
        [InlineData(SecsFormat.U2, (ushort)60000)]
        [InlineData(SecsFormat.U4, 4000000000u)]
        public void AddData_Unsigned_RoundTrip(SecsFormat format, object value)
        {
            var item = _order.AddData(value, format);
            Assert.Equal(format, item.Format);
            Assert.Equal(value.ToString(), _order.SecsItemToList(item)[0]);
        }

        /// <summary>
        /// 验证 AddData_Boolean_True 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 AddData、Equal、SecsItemToList；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AddData_Boolean_True()
        {
            var item = _order.AddData(true, SecsFormat.Boolean);
            Assert.Equal(SecsFormat.Boolean, item.Format);
            Assert.Equal("True", _order.SecsItemToList(item)[0]);
        }

        /// <summary>
        /// 验证 AddData_List_FromStringList_BuildsListOfAscii 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 AddData、Equal、GetString；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AddData_List_FromStringList_BuildsListOfAscii()
        {
            var data = new List<string> { "A", "B", "C" };
            var item = _order.AddData(data, SecsFormat.List);

            Assert.Equal(SecsFormat.List, item.Format);
            Assert.Equal(3, item.Count);
            Assert.Equal("A", item.Items[0].GetString());
            Assert.Equal("C", item.Items[2].GetString());
        }

        /// <summary>
        /// 验证 AddData_List_FromDictionary_BuildsListOfPairs 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 AddData、Equal、GetString；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AddData_List_FromDictionary_BuildsListOfPairs()
        {
            var data = new Dictionary<string, string>
            {
                ["K1"] = "V1",
                ["K2"] = "V2",
            };
            var item = _order.AddData(data, SecsFormat.List);

            Assert.Equal(SecsFormat.List, item.Format);
            Assert.Equal(2, item.Count);

            var pair = item.Items[0];
            Assert.Equal(SecsFormat.List, pair.Format);
            Assert.Equal("K1", pair.Items[0].GetString());
            Assert.Equal("V1", pair.Items[1].GetString());
        }

        /// <summary>
        /// 验证 AddData_List_Null_ReturnsEmptyList 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 AddData、Equal、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void AddData_List_Null_ReturnsEmptyList()
        {
            var item = _order.AddData(null, SecsFormat.List);
            Assert.Equal(SecsFormat.List, item.Format);
            Assert.Empty(item.Items);
        }

        // ── SecsItemToList 解析 ──

        [Fact]
        public void SecsItemToList_NonList_WrapsSingleValue()
        {
            var item = Item.U1(42);
            var result = _order.SecsItemToList(item);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("42", result[0]);
        }

        /// <summary>
        /// 验证 SecsItemToList_FlattensNestedList 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 L、A、U4、SecsItemToList、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SecsItemToList_FlattensNestedList()
        {
            var item = Item.L(
                Item.L(Item.A("X"), Item.A("Y")),
                Item.L(Item.U4(1))
            );
            var result = _order.SecsItemToList(item);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { "X", "Y", "1" }, result);
        }

        /// <summary>
        /// 验证 SecsItemToList_Null_ReturnsNull 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Null、SecsItemToList；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SecsItemToList_Null_ReturnsNull()
        {
            Assert.Null(_order.SecsItemToList(null));
        }

        /// <summary>
        /// 验证 SecsItemToList_EmptyList_ReturnsEmpty 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 SecsItemToList、L、NotNull、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SecsItemToList_EmptyList_ReturnsEmpty()
        {
            var result = _order.SecsItemToList(Item.L());
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ── GetValue<T> ──

        /// <summary>
        /// 验证 GetValue_ReturnsFirstElementAsString 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 U1、Equal、GetValue；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetValue_ReturnsFirstElementAsString()
        {
            var item = Item.U1(7);
            Assert.Equal("7", _order.GetValue<byte>(item));
        }

        /// <summary>
        /// 验证 GetValue_EmptyMemory_ReturnsNull 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 U1、Null、GetValue；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetValue_EmptyMemory_ReturnsNull()
        {
            var item = Item.U1(Memory<byte>.Empty);
            Assert.Null(_order.GetValue<byte>(item));
        }
    }
}
