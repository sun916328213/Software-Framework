// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：UserData、UserServiceEventArgs、EmailServiceObserver、LoggingServiceObserver、NotificationServiceObserver、UserService、AdvancedObserverPatternExample。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Threading.Tasks、Extension.Patterns.Messaging.Observer.Advanced。导入不代表所有类型都会在运行时加载。
using System;
using System.Threading.Tasks;
using Extension.Patterns.Messaging.Observer.Advanced;

namespace DesignPattern.Samples.DesignPattern.ObserverPattern.Examples
{
    /// <summary>
    /// 用户数据类
    /// </summary>
    public class UserData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }

        /// <summary>
        /// 初始化 UserData 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public UserData(int id, string name, string email)
        {
            Id = id;
            Name = name;
            Email = email;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ToString。
        /// </summary>
        public override string ToString()
        {
            return $"用户ID: {Id}, 姓名: {Name}, 邮箱: {Email}";
        }
    }

    /// <summary>
    /// 用户服务事件数据类
    /// </summary>
    public class UserServiceEventArgs
    {
        public string Operation { get; set; }
        public UserData User { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// 初始化 UserServiceEventArgs 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public UserServiceEventArgs(string operation, UserData user, string message)
        {
            Operation = operation;
            User = user;
            Message = message;
        }
    }

    /// <summary>
    /// 邮件服务观察者
    /// </summary>
    public class EmailServiceObserver : AdvancedObserverBase<UserServiceEventArgs>
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 HandleEvent。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public override void HandleEvent(object sender, EventArgs<UserServiceEventArgs> e)
        {
            if (e.Data.User != null)
            {
                Console.WriteLine($"[邮件服务] 发送邮件到 {e.Data.User.Email}: {e.Data.Message}");
            }
        }
    }

    /// <summary>
    /// 日志服务观察者
    /// </summary>
    public class LoggingServiceObserver : AdvancedObserverBase<UserServiceEventArgs>
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 HandleEvent。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public override void HandleEvent(object sender, EventArgs<UserServiceEventArgs> e)
        {
            Console.WriteLine($"[日志服务] 记录操作: {e.Data.Operation} - {e.Data.Message}");
        }
    }

    /// <summary>
    /// 通知服务观察者
    /// </summary>
    public class NotificationServiceObserver : AdvancedObserverBase<UserServiceEventArgs>
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 HandleEvent。
        /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
        /// </summary>
        public override void HandleEvent(object sender, EventArgs<UserServiceEventArgs> e)
        {
            Console.WriteLine($"[通知服务] 发送系统通知: {e.Data.Message}");
        }
    }

    /// <summary>
    /// 用户服务类
    /// 演示高级观察者模式的使用
    /// </summary>
    public class UserService
    {
        private readonly AdvancedObservable<UserServiceEventArgs> _eventPublisher;
        private int _userIdCounter = 1;

        /// <summary>
        /// 初始化 UserService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public UserService()
        {
            _eventPublisher = new AdvancedObservable<UserServiceEventArgs>();
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe(IAdvancedObserver<UserServiceEventArgs> observer)
        {
            _eventPublisher.Subscribe(observer);
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        public void Unsubscribe(IAdvancedObserver<UserServiceEventArgs> observer)
        {
            _eventPublisher.Unsubscribe(observer);
        }

        /// <summary>
        /// 创建用户
        /// </summary>
        public async Task<UserData> CreateUserAsync(string name, string email)
        {
            var user = new UserData(_userIdCounter++, name, email);
            
            Console.WriteLine($"\n创建用户: {user}");
            
            // 发布用户创建事件
            await _eventPublisher.PublishAsync(new UserServiceEventArgs(
                "CreateUser", 
                user, 
                $"用户 {name} 已成功创建"
            ));
            
            return user;
        }

        /// <summary>
        /// 更新用户
        /// </summary>
        public async Task UpdateUserAsync(UserData user, string newName, string newEmail)
        {
            user.Name = newName;
            user.Email = newEmail;
            
            Console.WriteLine($"\n更新用户: {user}");
            
            // 发布用户更新事件
            await _eventPublisher.PublishAsync(new UserServiceEventArgs(
                "UpdateUser", 
                user, 
                $"用户 {user.Id} 的信息已更新"
            ));
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        public async Task DeleteUserAsync(UserData user)
        {
            Console.WriteLine($"\n删除用户: {user}");
            
            // 发布用户删除事件
            await _eventPublisher.PublishAsync(new UserServiceEventArgs(
                "DeleteUser", 
                user, 
                $"用户 {user.Name} 已被删除"
            ));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _eventPublisher?.Dispose();
        }
    }

    /// <summary>
    /// 高级观察者模式使用示例
    /// </summary>
    public class AdvancedObserverPatternExample
    {
        /// <summary>
        /// 运行当前处理流程；对应入口为 RunExample。
        /// 正文中的关键协作调用包括 WriteLine、Subscribe、CreateUserAsync、UpdateUserAsync、DeleteUserAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public static async Task RunExample()
        {
            Console.WriteLine("=== 高级观察者模式使用示例 ===\n");

            // 1. 创建用户服务
            var userService = new UserService();

            // 2. 创建不同类型的观察者
            var emailObserver = new EmailServiceObserver();
            var loggingObserver = new LoggingServiceObserver();
            var notificationObserver = new NotificationServiceObserver();

            // 3. 使用委托创建观察者
            var delegateObserver = new DelegateObserver<UserServiceEventArgs>(
                "委托观察者",
                (sender, e) => Console.WriteLine($"[委托观察者] 接收到事件: {e.Data.Message}")
            );

            // 4. 订阅事件
            Console.WriteLine("1. 订阅事件:");
            userService.Subscribe(emailObserver);
            userService.Subscribe(loggingObserver);
            userService.Subscribe(notificationObserver);
            userService.Subscribe(delegateObserver);

            // 5. 创建用户
            Console.WriteLine("\n2. 创建用户:");
            var user1 = await userService.CreateUserAsync("张三", "zhangsan@example.com");
            var user2 = await userService.CreateUserAsync("李四", "lisi@example.com");

            // 6. 更新用户
            Console.WriteLine("\n3. 更新用户:");
            await userService.UpdateUserAsync(user1, "张三丰", "zhangsanfeng@example.com");

            // 7. 删除用户
            Console.WriteLine("\n4. 删除用户:");
            await userService.DeleteUserAsync(user2);

            // 8. 取消部分订阅
            Console.WriteLine("\n5. 取消部分订阅:");
            userService.Unsubscribe(notificationObserver);
            userService.Unsubscribe(delegateObserver);

            // 9. 再次执行操作，观察订阅者变化
            Console.WriteLine("\n6. 再次创建用户（部分订阅者已取消订阅）:");
            var user3 = await userService.CreateUserAsync("王五", "wangwu@example.com");

            // 10. 清理资源
            Console.WriteLine("\n7. 清理资源:");
            userService.Dispose();

            Console.WriteLine("\n=== 高级观察者模式示例结束 ===");
        }
    }
}