// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FakeEquipmentDataProvider。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、SecsGem.Core。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using SecsGem.Core;

namespace SecsGem.Tests.Fakes;

/// <summary>
/// FakeEquipmentDataProvider 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：IEquipmentDataProvider。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class FakeEquipmentDataProvider(IReadOnlyDictionary<string, object?> values) : IEquipmentDataProvider
{
    /// <summary>
    /// 测试相关入口 GetVariableValues；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetVariableValues() => values;
}
