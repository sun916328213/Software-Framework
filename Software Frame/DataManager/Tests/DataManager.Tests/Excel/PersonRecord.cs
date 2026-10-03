// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PersonRecord。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
namespace DataManager.Tests.Excel
{
    /// <summary>
    /// Excel 测试实体
    /// </summary>
    public class PersonRecord
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public DateTime CreateTime { get; set; } = new(2026, 1, 1);
    }
}
