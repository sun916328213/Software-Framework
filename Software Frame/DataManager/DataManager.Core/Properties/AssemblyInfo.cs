// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 直接导入的协作命名空间包括：System.Runtime.CompilerServices。导入不代表所有类型都会在运行时加载。
using System.Runtime.CompilerServices;

// P12：允许测试工程白盒覆盖内部实现（谓词翻译器/SQL 构造器/分表路由）。
[assembly: InternalsVisibleTo("DataManager.Tests")]
