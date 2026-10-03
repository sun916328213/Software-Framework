// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：UserMessage。成员说明可结合下方具体实现阅读。
namespace WPFControls.ProgramControl.Presentation.User
{
    /// <summary>
    /// UserMessage 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public static class UserMessage
    {
        public static readonly string[] 用户信息不能为空 = { "用户信息不能为空", "User information cannot be empty" };
        public static readonly string[] 用户信息已经存在 = { "用户信息已经存在", "User information already exists" };
        public static readonly string[] 密码不一致 = { "密码不一致" ,"Passwords do not match"  };
        public static readonly string[] 密码错误 = { "密码错误", "Incorrect password" };
        public static readonly string[] 账户名或密码错误 = { "账户名或密码错误", "Incorrect user name or password" };
        public static readonly string[] 登录失败 = { "登录失败", "Login failed" };
        public static readonly string[] 请先登录 = { "请先登录", "Please log in first" };
        public static readonly string[] 当前用户登录中 = { "当前用户登录中", "The current user is logged in" };
    }

}
