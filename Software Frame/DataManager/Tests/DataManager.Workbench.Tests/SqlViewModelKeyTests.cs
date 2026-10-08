// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SqlViewModelKeyTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Workbench.Models、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、DataManager.Controls.Services。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Workbench.Models;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using DataManager.Controls.Services;

namespace DataManager.Workbench.Tests
{
    /// <summary>
    /// SqlViewModel 连接指纹回归测试：密码纠错后必须重建实例。
    /// 背景：键曾只含“工具+库类型”，密码改对后仍复用旧实例，一直报密码错误。
    /// 指纹按 DataManager 分字段口径计算（工作台档案标准串先转分字段再进指纹）。
    /// </summary>
    public class SqlViewModelKeyTests
    {
        /// <summary>
        /// 测试相关入口 BaseParam；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        private static SQLParam BaseParam() => new()
        {
            Name = "workbench",
            ServerIP = "127.0.0.1",
            Port = "5432",
            UserName = "postgres",
            Password = "wrong",
            DBName = "dm_integration",
            DBType = ESQLType.PostgreSQL,
            ToolType = ESQLToolType.Dapper,
        };

        /// <summary>
        /// 测试相关入口 BuildDbKey_PasswordChange_ProducesDifferentKey；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BaseParam、NotEqual、BuildDbKey；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildDbKey_PasswordChange_ProducesDifferentKey()
        {
            var before = BaseParam();
            var after = BaseParam();
            after.Password = "123";

            Assert.NotEqual(SqlWorkspace<TestEntity>.BuildDbKey(before), SqlWorkspace<TestEntity>.BuildDbKey(after));
        }

        /// <summary>
        /// 测试相关入口 BuildDbKey_SameParams_ProducesSameKey；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、BuildDbKey、BaseParam；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildDbKey_SameParams_ProducesSameKey()
        {
            Assert.Equal(SqlWorkspace<TestEntity>.BuildDbKey(BaseParam()), SqlWorkspace<TestEntity>.BuildDbKey(BaseParam()));
        }

        /// <summary>
        /// 测试相关入口 BuildDbKey_AnyFieldChange_ProducesDifferentKey；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BaseParam、SetValue、GetProperty、NotEqual、BuildDbKey；具体条件和先后次序见方法体。
        /// </summary>
        [Theory]
        [InlineData("ServerIP", "192.168.1.10")]
        [InlineData("Port", "5433")]
        [InlineData("UserName", "admin")]
        [InlineData("DBName", "other")]
        [InlineData("FilePath", "other.db")]
        [InlineData("ConnectionString", "Server=x;")]
        public void BuildDbKey_AnyFieldChange_ProducesDifferentKey(string field, string value)
        {
            var before = BaseParam();
            var after = BaseParam();
            typeof(SQLParam).GetProperty(field)!.SetValue(after, value);

            Assert.NotEqual(SqlWorkspace<TestEntity>.BuildDbKey(before), SqlWorkspace<TestEntity>.BuildDbKey(after));
        }

        /// <summary>
        /// 测试相关入口 BuildDbKey_TrustServerCertificateChange_ProducesDifferentKey；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 BaseParam、NotEqual、BuildDbKey；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void BuildDbKey_TrustServerCertificateChange_ProducesDifferentKey()
        {
            var before = BaseParam();
            var after = BaseParam();
            after.TrustServerCertificate = !before.TrustServerCertificate;

            Assert.NotEqual(SqlWorkspace<TestEntity>.BuildDbKey(before), SqlWorkspace<TestEntity>.BuildDbKey(after));
        }
    }
}
