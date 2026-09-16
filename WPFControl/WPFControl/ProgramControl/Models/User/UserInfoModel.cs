using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Models;

namespace WPFControl.ProgramControl.Models
{
  

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

        public UserInfoModel()
        {
            iUserLevels = new ObservableCollection<string>(Extension.Core.EnumEx.EnumEx.GetName<EUserLevel>());
            iUserLevels.Remove(EUserLevel.SuperAdmin.ToString()); //排除管理员
            iUserLevels.Remove(EUserLevel.Guest.ToString()); //排除管理员
        }
    }
}
