// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：EUserLevel、UserInfo。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.ComponentModel、System.Text.Json.Serialization、CommunityToolkit.Mvvm、CommunityToolkit.Mvvm.ComponentModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControls.Controls.Models
{
    /// <summary>
    /// EUserLevel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EUserLevel
    {
        [Description("游客")] Guest = 0,
        [Description("操作员")] Operator,
        [Description("工程师")] Engineer,
        [Description("管理员")] Admin,
        [Description("超级管理员")][Browsable(false)] SuperAdmin,
    }

    /// <summary>
    /// 通用基础本信息结
    /// </summary>
    public class UserInfo: ObservableObject
    {
        private string name,password;
        private EUserLevel level= EUserLevel.Guest;

        /// <summary>
        /// 用户名称
        /// </summary>
        public string Name
        {
            get => name;
            set=>SetProperty(ref name, value);
        }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password
        {
            get => password;
            set => SetProperty(ref password, value);
        }

        /// <summary>
        /// 用户等级
        /// </summary>
        public EUserLevel Level
        {
            get => level;
            set => SetProperty(ref level, value);
        }

        /// <summary>
        /// 初始化 UserInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public UserInfo() { }


        /// <summary>
        /// 初始化 UserInfo 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public UserInfo(string iName, string iPassword, EUserLevel iLevel)
        {
            Name = iName;
            Password = iPassword;
            Level = iLevel;
        }

    }
}
