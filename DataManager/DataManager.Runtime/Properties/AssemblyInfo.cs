using System.Runtime.CompilerServices;

// P12：允许测试工程白盒覆盖内部实现（谓词翻译器/SQL 构造器/分表路由）。
[assembly: InternalsVisibleTo("DataManager.Tests")]
