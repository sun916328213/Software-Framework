using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WPFControl.CustomControl.Models
{
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

        public UserInfo() { }


        public UserInfo(string iName, string iPassword, EUserLevel iLevel)
        {
            Name = iName;
            Password = iPassword;
            Level = iLevel;
        }

    }
}
