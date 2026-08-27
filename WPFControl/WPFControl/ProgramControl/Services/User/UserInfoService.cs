using Extension.Core.EnumEx;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Services;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class UserInfoService
    {
        /// <summary>
        /// 用户信息模型
        /// </summary>
        internal UserInfoModel Model { get; set; } = new UserInfoModel();

        /// <summary>
        /// 用户信息管理类
        /// </summary>
        internal UserManagerService _UserService = new UserManagerService();

        public event EventHandler<UserInfoModel> UserInfoChanged;

        public UserInfoService()
        {
            Update();
            // 订阅用户状态变化事件
            _UserService.UserStatusChanged += OnUserStatusChanged;
        }

        /// <summary>
        /// 处理用户状态变化事件
        /// </summary>
        /// <param name="userInfo">当前用户信息</param>
        private void OnUserStatusChanged(UserInfo userInfo)
        {
            Update();
            // 用户状态变化（含自动登出）统一在此通知订阅方，确保菜单等随等级刷新
            UserInfoChanged?.Invoke(this, Model);
        }

        /// <summary>
        /// 更新用户信息
        /// </summary>
        private void Update()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.UserNames = new ObservableCollection<string>(_UserService.GetUserInfos.Select(s => s.Name));
                Model.CurUserInfo = _UserService.CurUserInfo;
                Model.UserInfos = new ObservableCollection<UserInfo>(_UserService.GetUserInfos);
            });
        }



        /// <summary>
        /// 获取当前用户权限等级
        /// </summary>
        /// <returns></returns>
        public EUserLevel GetUserLevel()
        {
            return TryCatchHelper.TryExecute(() =>
            {
                return Model.UserLevel = _UserService.CurUserInfo.Level;
            });
        }

        /// <summary>
        /// 获取当前用户名称
        /// </summary>
        /// <returns></returns>
        public string GetUserName()
        {
            return TryCatchHelper.TryExecute(() =>
            {
                return Model.UserName = _UserService.CurUserInfo.Name;
            });
        }



        /// <summary>
        /// 切换添加用户界面
        /// </summary>
        public void SwitchAdd() => SwitchTabItem(1);

        /// <summary>
        /// 切换修改用户界面
        /// </summary>
        public void SwitchModify() => SwitchTabItem(2);

        /// <summary>
        /// 切换删除用户界面
        /// </summary>
        public void SwitchDelet() => SwitchTabItem(3);

        /// <summary>
        /// 切换用户主界面
        /// </summary>
        public void SwitchMain() => SwitchTabItem(0);


        public void SwitchTabItem(int index)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.TabSelectItem = index;
                Reset();
            });
        }

        private void Reset()
        {
            TryCatchHelper.TryExecute(() =>
            {
                Model.ErrorMessage = "";
                Model.UserName = "";
                Model.Password = "";
                Model.NewPassword = "";
                Model.ConfirmPassword = "";
                Model.UserLevel = EUserLevel.Guest;
            });
        }

        public void LoginCommand()
        {
            TryCatchHelper.TryExecute(() =>
            {
                _UserService.Login(Model.UserName, Model.Password);

                Update();
                Reset();
            },
            (ex) => Model.ErrorMessage = ex.Message
            );
        }

        public void LogoutCommand()
        {
            TryCatchHelper.TryExecute(() =>
            {
                _UserService.Logout();
                Update();
                Reset();
            },
            (ex) => Model.ErrorMessage = ex.Message
            );
        }


        public void AddCommand()
        {
            TryCatchHelper.TryExecute(() =>
            {
                _UserService.Add_UserInfo(Model.UserName, Model.Password, Model.ConfirmPassword, Model.UserLevel);
                Update();
                Reset();
            },
            (ex) => Model.ErrorMessage = ex.Message
            );
        }

        /// <summary>
        /// 删除
        /// </summary>
        public void DeletCommand()
        {
            TryCatchHelper.TryExecute(() =>
            {
                _UserService.Delet_UserInfo(Model.UserName, Model.Password, Model.ConfirmPassword);
                Update();
                Reset();
            },
            (ex) => Model.ErrorMessage = ex.Message
            );
        }

        /// <summary>
        /// 修改
        /// </summary>
        public void ModifyCommand()
        {
            TryCatchHelper.TryExecute(() =>
            {
                _UserService.Modify_UserInfo(Model.UserName, Model.Password, Model.NewPassword,
                    Model.ConfirmPassword, Model.UserLevel);
                Update();
                Reset();
            },
            (ex) => Model.ErrorMessage = ex.Message
            );
        }
    }
}
