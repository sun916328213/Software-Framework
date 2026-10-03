// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：UserInfoService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Extension.Core.Conversion、Microsoft.EntityFrameworkCore.Metadata.Internal、System、System.Collections.Generic、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Extension.Core.Conversion;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WPFControls.Controls.Models;
using WPFControls.Controls.Services;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// UserInfoService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class UserInfoService
    {
        private readonly TryCatchHelper _errors;

        /// <summary>
        /// 用户信息模型
        /// </summary>
        internal UserInfoModel Model { get; set; } = new UserInfoModel();

        /// <summary>
        /// 用户信息管理类
        /// </summary>
        internal UserManagerService _UserService = new UserManagerService();

        public event EventHandler<UserInfoModel> UserInfoChanged;

        /// <summary>
        /// 初始化 UserInfoService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Update；具体条件和先后次序见方法体。
        /// </summary>
        public UserInfoService(TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

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
            _errors.TryExecute(() =>
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
            return _errors.TryExecute(() =>
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
            return _errors.TryExecute<string>(() =>
            {
                Model.UserName = _UserService.CurUserInfo.Name;
                return Model.UserName;
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


        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SwitchTabItem。
        /// 正文中的关键协作调用包括 TryExecute、Reset；具体条件和先后次序见方法体。
        /// </summary>
        public void SwitchTabItem(int index)
        {
            _errors.TryExecute(() =>
            {
                Model.TabSelectItem = index;
                Reset();
            });
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Reset。
        /// 正文中的关键协作调用包括 TryExecute；具体条件和先后次序见方法体。
        /// </summary>
        private void Reset()
        {
            _errors.TryExecute(() =>
            {
                Model.ErrorMessage = "";
                Model.UserName = "";
                Model.Password = "";
                Model.NewPassword = "";
                Model.ConfirmPassword = "";
                Model.UserLevel = EUserLevel.Guest;
            });
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 LoginCommand。
        /// 正文中的关键协作调用包括 TryExecuteWithResult、Login、Update、Reset；具体条件和先后次序见方法体。
        /// </summary>
        public bool LoginCommand()
        {
            return _errors.TryExecuteWithResult(() =>
              {
                  _UserService.Login(Model.UserName, Model.Password);

                  Update();
                  Reset();
              },
              (ex) => Model.ErrorMessage = ex.Message
              );
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 LogoutCommand。
        /// 正文中的关键协作调用包括 TryExecute、Logout、Update、Reset；具体条件和先后次序见方法体。
        /// </summary>
        public void LogoutCommand()
        {
            _errors.TryExecute(() =>
            {
                _UserService.Logout();
                Update();
                Reset();
            },
            (ex) => Model.ErrorMessage = ex.Message
            );
        }


        /// <summary>
        /// 执行添加操作；对应入口为 AddCommand。
        /// 正文中的关键协作调用包括 TryExecute、Add_UserInfo、Update、Reset；具体条件和先后次序见方法体。
        /// </summary>
        public void AddCommand()
        {
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
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
            _errors.TryExecute(() =>
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

