// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FakeSecsGemEventHandler。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、SecsGem.Core。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using SecsGem.Core;

namespace SecsGem.Tests.Fakes;

/// <summary>
/// FakeSecsGemEventHandler 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：ISecsGemEventHandler。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class FakeSecsGemEventHandler(byte result = 0) : ISecsGemEventHandler
{
    public SecsGemRemoteCommand? LastCommand { get; private set; }

    /// <summary>
    /// 验证 OnRemoteCommandAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
    /// </summary>
    public Task<byte> OnRemoteCommandAsync(SecsGemRemoteCommand command)
    {
        LastCommand = command;
        return Task.FromResult(result);
    }
}
