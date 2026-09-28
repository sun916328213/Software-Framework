using System.Threading.Tasks;
using Secs;
using Secs.Order;
using Secs4Net;
using Xunit;

namespace Secs.Runtime.Tests
{
    /// <summary>
    /// 门面与回调接口相关测试
    /// </summary>
    public class SecsFacadeTests
    {
        private class TestOrder : SecsOrderBase
        {
            public override Item GetReplyItem(SecsMessage pSecsMessage) => Item.B(7);
        }

        [Fact]
        public void GetReplyItemAsync_Default_Wraps_Sync_Method()
        {
            var order = new TestOrder();
            var msg = new SecsMessage(1, 1, false);

            Task<Item> task = order.GetReplyItemAsync(msg);

            Assert.True(task.IsCompletedSuccessfully);
            Assert.Equal(SecsFormat.Binary, task.Result.Format);
        }

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
