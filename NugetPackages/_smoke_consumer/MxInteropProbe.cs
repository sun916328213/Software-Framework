using System;
using ActUtlTypeLib;
using ActUtlType64Lib;
using ActProgTypeLib;
using ActProgType64Lib;
using ActSupportMsgLib;
using ActSupportMsg64Lib;

namespace SmokeConsumer
{
    // 验证 Mitsubishi.MxComponent.Interop 包可被消费：能 using 全部 6 个 Interop 程序集的类型并编译通过
    public static class MxInteropProbe
    {
        public static void Probe()
        {
            // ActUtlType（数据通信，32/64）
            var utl32 = new ActUtlTypeClass();
            var utl64 = new ActUtlType64Class();
            Console.WriteLine("ActUtlType OK: {0} / {1}", utl32.GetType().FullName, utl64.GetType().FullName);

            // ActProg（程序监控，32/64）
            var prog32 = new ActProgTypeClass();
            var prog64 = new ActProgType64Class();
            Console.WriteLine("ActProg OK: {0} / {1}", prog32.GetType().FullName, prog64.GetType().FullName);

            // ActSupportMsg（支持消息，32/64）
            var msg32 = new ActSupportMsgClass();
            var msg64 = new ActSupportMsg64Class();
            Console.WriteLine("ActSupportMsg OK: {0} / {1}", msg32.GetType().FullName, msg64.GetType().FullName);
        }
    }
}
