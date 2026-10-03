// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：UserManagerService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Presentation.User、WPFControls.ProgramControl.Services、WPFControls、Extension.Core.Conversion、Extension.Core.Enums、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Presentation.User;
using WPFControls.ProgramControl.Services;
using WPFControls;
using Extension.Core.Conversion;
using Extension.Core.Enums;
using Extension.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WPFControls.Controls.Models;



namespace WPFControls.ProgramControl.Services.User
{

    /// <summary>
    /// UserManagerService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    internal class UserManagerService
    {
        /// <summary>
        /// 用户登入时间
        /// </summary>
        private DateTime _lastActivityTime;
        private DispatcherTimer _logoutTimer;

        /// <summary>
        /// 登出时间
        /// </summary>
        public TimeSpan LogoutTime = TimeSpan.FromMinutes(30);
        /// <summary>
        /// 用户信息文件路径
        /// </summary>

        public string FilePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + "//Config//";

        /// <summary>
        /// 用户信息文件路径
        /// </summary>
        public string FileName { get; set; } = "UserInfos.json";

        /// <summary>
        /// 根据用户权限获取用户信息
        /// </summary>
        public List<UserInfo> GetUserInfos => UserInfos.Where(x => x.Level <= CurUserInfo.Level).ToList();

        public List<string> GetLoginUserNames => UserInfos
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .Select(x => x.Name)
            .ToList();


        /// <summary>
        /// 用户状态变化事件
        /// </summary>
        public event Action<UserInfo> UserStatusChanged;

        private ObservableCollection<UserInfo> UserInfos = new ObservableCollection<UserInfo>();

        internal UserInfo CurUserInfo = new UserInfo();


        /// <summary>
        /// 初始化 UserManagerService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Load_UserInfo、GetDescription、Any、Add、Invoke；具体条件和先后次序见方法体。
        /// </summary>
        public UserManagerService()
        {
            UserInfos = Load_UserInfo();

            UserInfo iuser1 = new UserInfo(EUserLevel.Guest.GetDescription(), "", EUserLevel.Guest);
            if (!UserInfos.Any(s => s.Name == iuser1.Name))
            {
                UserInfos.Add(iuser1);
            }
            UserInfo iuser2 = new UserInfo("zjq", "083330", EUserLevel.SuperAdmin);
            if (!UserInfos.Any(s => s.Name == iuser2.Name))
            {
                UserInfos.Add(iuser2);
            }
            UserInfo iuser3 = new UserInfo("SL", "18352566831", EUserLevel.SuperAdmin);
            if (!UserInfos.Any(s => s.Name == iuser3.Name))
            {
                UserInfos.Add(iuser3);
            }
            // DispatcherTimer 和 InputManager 必须在 UI 线程初始化，
            // 否则 DispatcherTimer 会绑定到错误的调度器而永不触发，导致 Login 后无法循环计时
            global::System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _logoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                _logoutTimer.Tick += LogoutTimer_Tick;
                InputManager.Current.PreProcessInput += InputManager_PreProcessInput;
            });
            Logout();
        }
 
        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        internal void Add_UserInfo(UserInfo userInfo)
        {
            if (userInfo.Name == null) throw new Exception(UserMessage.用户信息不能为空.GetMessage());
            if (UserInfos.Any(s => s.Name == userInfo.Name)) throw new Exception(UserMessage.用户信息已经存在.GetMessage());

            UserInfos.Add(userInfo);
            Save_UserInfo();
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="userName">用户名称</param>
        /// <param name="password">密码</param>
        /// <param name="confirmPassword">确认密码</param>
        /// <param name="pUserlevel">用户等级</param>
        /// <returns></returns>
        public void Add_UserInfo(string userName, string password, string confirmPassword,  EUserLevel pUserlevel = EUserLevel.Guest)
        {
            if (userName == null) throw new Exception(UserMessage.用户信息不能为空.GetMessage());
            if (password != confirmPassword) throw new Exception(UserMessage.密码不一致.GetMessage());
            if (UserInfos.Any(s => s.Name == userName)) throw new Exception(UserMessage.用户信息已经存在.GetMessage());

            var userinfo = new UserInfo(userName, password, pUserlevel);
            UserInfos.Add(userinfo);
            Save_UserInfo();
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="name"></param>
        /// <param name="passwd"></param>
        public void Login(string name, string passwd)
        {
            UserInfo userInfo = CheckLoginPassword(name, passwd);
            
            CurUserInfo = userInfo;
            // 触发用户状态变化事件
            UserStatusChanged?.Invoke(CurUserInfo);

            _lastActivityTime = DateTime.Now;
           
                _logoutTimer.Start();
           
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 InputManager_PreProcessInput。
        /// </summary>
        private void InputManager_PreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!_logoutTimer.IsEnabled)
            {
                return;
            }

            InputEventArgs input = e.StagingItem.Input;
            if (input is KeyEventArgs ||
                input is MouseEventArgs ||
                input is TouchEventArgs ||
                input is StylusEventArgs)
            {
                _lastActivityTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 LogoutTimer_Tick。
        /// 正文中的关键协作调用包括 Logout；具体条件和先后次序见方法体。
        /// </summary>
        private void LogoutTimer_Tick(object? sender, EventArgs e)
        {
            if (DateTime.Now - _lastActivityTime >= LogoutTime)
            {
                Logout();
            }
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        /// <param name="name"></param>
        /// <param name="passwd"></param>
        /// <returns></returns>
        private UserInfo CheckPassword(string name, string passwd)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new Exception(UserMessage.用户信息不能为空.GetMessage());

            var userInfo = UserInfos.FirstOrDefault(s => s.Name == name);
            if (passwd == null) passwd = "";
            if (userInfo == null || userInfo.Password != passwd)
                throw new Exception(UserMessage.密码错误.GetMessage());

            return userInfo;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CheckLoginPassword。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、GetMessage、FirstOrDefault；具体条件和先后次序见方法体。
        /// </summary>
        private UserInfo CheckLoginPassword(string name, string passwd)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new Exception(UserMessage.账户名或密码错误.GetMessage());

            UserInfo? userInfo = UserInfos.FirstOrDefault(s => s.Name == name);
            if (userInfo == null || userInfo.Password != (passwd ?? ""))
                throw new Exception(UserMessage.账户名或密码错误.GetMessage());

            return userInfo;
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetLoginUserInfo。
        /// 正文中的关键协作调用包括 FirstOrDefault；具体条件和先后次序见方法体。
        /// </summary>
        public UserInfo? GetLoginUserInfo(string? userName)
        {
            return UserInfos.FirstOrDefault(s => s.Name == userName);
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="confirmPassword"></param>
        /// <returns></returns>
        public void Delet_UserInfo(string userName, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(userName)) throw new Exception(UserMessage.用户信息不能为空.GetMessage());
            if (password != confirmPassword) throw new Exception(UserMessage.密码错误.GetMessage());
            if (CurUserInfo.Name == userName) throw new Exception(UserMessage.当前用户登录中.GetMessage());

            var result = CheckPassword(userName, password);
            var userInfo = UserInfos.First(s => s.Name == userName);
            UserInfos.Remove(userInfo);
            Save_UserInfo();
        }

        /// <summary>
        /// 修改用户信息
        /// </summary>
        /// <param name="pUserName"></param>
        /// <param name="pPassword"></param>
        /// <param name="pNewPassword"></param>
        /// <param name="pConfirmPassword"></param>
        /// <param name="pUserlevel"></param>
        /// <returns></returns>
        public void Modify_UserInfo(string pUserName, string pPassword, string pNewPassword, string pConfirmPassword,
            EUserLevel pUserlevel = EUserLevel.Guest)
        {
            if (string.IsNullOrEmpty(pUserName)) throw new Exception(UserMessage.用户信息不能为空.GetMessage());
            if (pNewPassword != pConfirmPassword) throw new Exception(UserMessage.密码不一致.GetMessage());
            if (CurUserInfo.Name == pUserName) throw new Exception(UserMessage.当前用户登录中.GetMessage());

            var reult = CheckPassword(pUserName, pPassword);
            var userInfo = UserInfos.First(s => s.Name == pUserName);

            userInfo.Password = pNewPassword;
            userInfo.Level = pUserlevel;

            Save_UserInfo();
        }

        /// <summary>
        /// 保存用户信息到文件
        /// </summary>
        /// <returns></returns>
        public void Save_UserInfo()
        {
            string json = UserInfos.ToJson();
            if (!Directory.Exists(FilePath))
            {
                Directory.CreateDirectory(FilePath);
            }
            File.WriteAllText(Path.Combine(FilePath, FileName), json);
        }

        /// <summary>
        /// 加载用户信息
        /// </summary>
        /// <returns></returns>
        private ObservableCollection<UserInfo> Load_UserInfo()
        {
            string path = Path.Combine(FilePath, FileName);
            if (!File.Exists(path)) return new ObservableCollection<UserInfo>();
            var data = File.ReadAllText(path).FromJson<ObservableCollection<UserInfo>>();
            if(data==null) 
                data= new ObservableCollection<UserInfo>();
            return data;
        }

        /// <summary>
        /// 注销用户
        /// </summary>
        /// <returns></returns>
        public void Logout()
        {
            _logoutTimer.Stop();
            CurUserInfo = UserInfos.First(s => s.Level == EUserLevel.Guest);
            // 触发用户状态变化事件
            UserStatusChanged?.Invoke(CurUserInfo);
        }
 
    }
}

