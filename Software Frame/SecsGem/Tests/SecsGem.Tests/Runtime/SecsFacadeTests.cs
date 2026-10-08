// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SecsFacadeTests、TestOrder。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Controls、System.Threading.Tasks、SecsGem.Core、SecsGem.Runtime、SecsGem.Core.Order、SecsGem.Runtime.Order。导入不代表所有类型都会在运行时加载。
using SecsGem.Controls;
using System.Threading.Tasks;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using Secs4Net;
using Xunit;

namespace SecsGem.Tests
{
    /// <summary>
    /// 门面与回调接口相关测试
    /// </summary>
    public class SecsFacadeTests
    {
        /// <summary>
        /// TestOrder 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// 继承或实现的契约：SecsOrderBase。具体约束按这些契约及各成员实现解释。
        /// </summary>
        private class TestOrder : SecsOrderBase
        {
            /// <summary>
            /// 测试相关入口 GetReplyItem；准备条件、协作调用与结果检查以方法正文为准。
            /// 正文中的关键协作调用包括 B；具体条件和先后次序见方法体。
            /// </summary>
            public override Item GetReplyItem(SecsMessage pSecsMessage) => Item.B(7);
        }

        /// <summary>
        /// 测试相关入口 GetReplyItemAsync_Default_Wraps_Sync_Method；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetReplyItemAsync、True、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetReplyItemAsync_Default_Wraps_Sync_Method()
        {
            var order = new TestOrder();
            var msg = new SecsMessage(1, 1, false);

            Task<Item> task = order.GetReplyItemAsync(msg);

            Assert.True(task.IsCompletedSuccessfully);
            Assert.Equal(SecsFormat.Binary, task.Result.Format);
        }

        /// <summary>
        /// 测试相关入口 RemoteCommandArgs_Default_Return_Is_Accepted；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、Empty；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void RemoteCommandArgs_Default_Return_Is_Accepted()
        {
            // HCACK=0 表示命令接受，业务未显式设置时不应误回拒绝码
            var args = new RemoteCommandArgs();
            Assert.Equal(0, args.Return);
            Assert.Empty(args.Params);
        }
    }
}
