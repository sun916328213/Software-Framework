// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：UserInfoModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、System.Collections.Generic、System.Collections.ObjectModel、WPFControls.Controls.Models。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WPFControls.Controls.Models;

namespace WPFControls.ProgramControl.Models
{
  

    /// <summary>
    /// UserInfoModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class UserInfoModel : ObservableObject
    {

        ObservableCollection<UserInfo> userInfos=new ObservableCollection<UserInfo>();
        /// <summary>
        /// 用户信息集合
        /// </summary>
        public ObservableCollection<UserInfo> UserInfos
        {
            get => userInfos;
            set => SetProperty(ref userInfos, value);
        }


        ObservableCollection<string> iUserNames = new ObservableCollection<string>();
        /// <summary>
        /// 用户名称集合
        /// </summary>
        public ObservableCollection<string> UserNames
        {
            get => iUserNames;
            set => SetProperty(ref iUserNames, value);
        }

        ObservableCollection<string> iUserLevels=new ObservableCollection<string>();   
        /// <summary>
        /// 用户等级集合
        /// </summary>
        public ObservableCollection<string> UserLevels
        {
            get => iUserLevels;
            set => SetProperty(ref iUserLevels, value);
        }


        UserInfo sUserInfo = new UserInfo();
        /// <summary>
        /// 当前用户
        /// </summary>
        public UserInfo CurUserInfo
        {
            get => sUserInfo;
            set => SetProperty(ref sUserInfo, value);
        }

        int selectItem;
        /// <summary>
        /// Tab功能项
        /// </summary>
        public int TabSelectItem
        {
            get => selectItem;
            set => SetProperty(ref selectItem, value);
        }



        string userName;
        /// <summary>
        /// 用户名称
        /// </summary>
        public string UserName
        {
            get => userName;
            set => SetProperty(ref userName, value);
        }

        string password;
        /// <summary>
        /// 当前用户密码
        /// </summary>
        public string Password
        {
            get => password;
            set => SetProperty(ref password, value);
        }

        string newpassword;
        /// <summary>
        /// 当前用户新密码 
        /// </summary>
        public string NewPassword
        {
            get => newpassword;
            set => SetProperty(ref newpassword, value);
        }

        string confirmpassword;
        /// <summary>
        /// 当前用户确认密码
        /// </summary>
        public string ConfirmPassword
        {
            get => confirmpassword;
            set => SetProperty(ref confirmpassword, value);
        }

        EUserLevel userlevel;
        /// <summary>
        /// 当前用户等级
        /// </summary>
        public EUserLevel UserLevel
        {
            get => userlevel;
            set => SetProperty(ref userlevel, value);
        }


        private string errorMessage;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage
        {
            get => errorMessage;
            set => SetProperty(ref errorMessage, value);
        }

        /// <summary>
        /// 初始化 UserInfoModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 GetName、Remove、ToString；具体条件和先后次序见方法体。
        /// </summary>
        public UserInfoModel()
        {
            iUserLevels = new ObservableCollection<string>(Extension.Core.Enums.EnumEx.GetName<EUserLevel>());
            iUserLevels.Remove(EUserLevel.SuperAdmin.ToString()); //排除管理员
            iUserLevels.Remove(EUserLevel.Guest.ToString()); //排除管理员
        }
    }
}
