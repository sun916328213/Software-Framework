using System;
using System.Threading.Tasks;
using Extension.DesignPattern.MediatorPattern;

namespace Extension.DesignPattern.MediatorPattern.Examples
{
    /// <summary>
    /// 用户数据类
    /// </summary>
    public class UserData
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }

        public UserData(string userId, string userName, string email)
        {
            UserId = userId;
            UserName = userName;
            Email = email;
        }

        public override string ToString()
        {
            return $"User: {UserName} ({UserId}), Email: {Email}";
        }
    }

    /// <summary>
    /// 聊天消息类
    /// </summary>
    public class ChatMessage
    {
        public string Sender { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public ChatMessage(string sender, string content)
        {
            Sender = sender;
            Content = content;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
        }
    }

    /// <summary>
    /// 用户管理同事实现
    /// </summary>
    public class UserManagerColleague : ColleagueBase
    {
        public UserManagerColleague(string id) : base(id)
        {
        }

        public override void Receive(string from, object message)
        {
            Console.WriteLine($"[用户管理] 从 {from} 接收到消息: {message}");

            if (message is UserData userData)
            {
                Console.WriteLine($"  处理用户数据: {userData}");
            }
            else if (message is string command)
            {
                switch (command)
                {
                    case "GetAllUsers":
                        Console.WriteLine("  执行获取所有用户命令");
                        // 模拟发送用户列表
                        var userList = new[]
                        {
                            new UserData("001", "张三", "zhangsan@example.com"),
                            new UserData("002", "李四", "lisi@example.com")
                        };
                        Send("ChatRoom", userList);
                        break;
                    default:
                        Console.WriteLine($"  未知命令: {command}");
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 聊天室同事实现
    /// </summary>
    public class ChatRoomColleague : ColleagueBase
    {
        public ChatRoomColleague(string id) : base(id)
        {
        }

        public override void Receive(string from, object message)
        {
            Console.WriteLine($"[聊天室] 从 {from} 接收到消息: {message}");

            if (message is ChatMessage chatMessage)
            {
                Console.WriteLine($"  广播聊天消息: {chatMessage}");
                Broadcast(chatMessage);
            }
            else if (message is UserData[] users)
            {
                Console.WriteLine("  接收到用户列表:");
                foreach (var user in users)
                {
                    Console.WriteLine($"    {user}");
                }
            }
        }
    }

    /// <summary>
    /// 通知服务同事实现
    /// </summary>
    public class NotificationColleague : ColleagueBase
    {
        public NotificationColleague(string id) : base(id)
        {
        }

        public override void Receive(string from, object message)
        {
            Console.WriteLine($"[通知服务] 从 {from} 接收到消息: {message}");

            if (message is ChatMessage chatMessage)
            {
                Console.WriteLine($"  发送通知: {chatMessage.Sender} 发送了新消息");
            }
            else if (message is UserData userData)
            {
                Console.WriteLine($"  发送用户通知: 新用户 {userData.UserName} 已加入");
            }
        }
    }

    /// <summary>
    /// 日志服务同事实现
    /// </summary>
    public class LoggingColleague : ColleagueBase
    {
        public LoggingColleague(string id) : base(id)
        {
        }

        public override void Receive(string from, object message)
        {
            Console.WriteLine($"[日志服务] 从 {from} 接收到消息: {message}");

            // 记录所有消息到日志
            Console.WriteLine($"  记录日志: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {from} -> {message}");
        }
    }

    /// <summary>
    /// 中介者模式使用示例
    /// </summary>
    public class MediatorPatternExample
    {
        public static async Task RunBasicExample()
        {
            Console.WriteLine("=== 基础中介者模式示例 ===\n");

            // 1. 创建中介者
            using (var mediator = new Mediator())
            {
                // 2. 创建不同类型的同事对象
                var userManager = new UserManagerColleague("UserManager");
                var chatRoom = new ChatRoomColleague("ChatRoom");
                var notificationService = new NotificationColleague("Notification");
                var loggingService = new LoggingColleague("Logging");

                // 3. 注册同事对象
                Console.WriteLine("1. 注册同事对象:");
                mediator.Register(userManager);
                mediator.Register(chatRoom);
                mediator.Register(notificationService);
                mediator.Register(loggingService);

                Console.WriteLine($"当前同事数量: {mediator.ColleagueCount}\n");

                // 4. 发送用户数据消息
                Console.WriteLine("2. 发送用户数据消息:");
                var newUser = new UserData("003", "王五", "wangwu@example.com");
                userManager.Send("ChatRoom", newUser);

                // 5. 发送聊天消息
                Console.WriteLine("\n3. 发送聊天消息:");
                var chatMessage = new ChatMessage("张三", "大家好！");
                chatRoom.Send("Notification", chatMessage);

                // 6. 广播消息
                Console.WriteLine("\n4. 广播消息:");
                var broadcastMessage = new ChatMessage("系统", "欢迎新用户加入");
                chatRoom.Broadcast(broadcastMessage);

                // 7. 发送命令消息
                Console.WriteLine("\n5. 发送命令消息:");
                userManager.Send("UserManager", "GetAllUsers");

                // 8. 异步发送消息
                Console.WriteLine("\n6. 异步发送消息:");
                var asyncMessage = new ChatMessage("李四", "异步消息测试");
                await chatRoom.SendAsync("Notification", asyncMessage);

                // 9. 异步广播消息
                Console.WriteLine("\n7. 异步广播消息:");
                var asyncBroadcastMessage = new ChatMessage("系统", "异步广播测试");
                await chatRoom.BroadcastAsync(asyncBroadcastMessage);

                // 10. 取消部分同事注册
                Console.WriteLine("\n8. 取消部分同事注册:");
                mediator.Unregister(notificationService);
                mediator.Unregister(loggingService);
                Console.WriteLine($"当前同事数量: {mediator.ColleagueCount}");

                // 11. 再次发送消息（部分同事已注销）
                Console.WriteLine("\n9. 再次发送消息（部分同事已注销）:");
                var finalMessage = new ChatMessage("系统", "这是最后一条消息");
                chatRoom.Broadcast(finalMessage);

                // 12. 清空所有同事
                Console.WriteLine("\n10. 清空所有同事:");
                mediator.ClearColleagues();
                Console.WriteLine($"当前同事数量: {mediator.ColleagueCount}");
            }

            Console.WriteLine("\n=== 基础示例结束 ===\n");
        }

        public static async Task RunDelegateExample()
        {
            Console.WriteLine("=== 委托中介者模式示例 ===\n");

            // 1. 创建中介者
            using (var mediator = new Mediator())
            {
                // 2. 创建基于委托的同事对象
                var delegateColleague1 = new DelegateColleague(
                    "Delegate1",
                    (from, message) => Console.WriteLine($"[委托同事1] 从 {from} 接收到: {message}"),
                    async (from, message) =>
                    {
                        Console.WriteLine($"[委托同事1] 异步处理来自 {from} 的消息: {message}");
                        await Task.Delay(100); // 模拟异步处理
                    }
                );

                var delegateColleague2 = new DelegateColleague(
                    "Delegate2",
                    (from, message) => Console.WriteLine($"[委托同事2] 从 {from} 接收到: {message}")
                    // 不提供异步处理委托，将使用同步方法
                );

                var delegateColleague3 = new DelegateColleague(
                    "Delegate3",
                    (from, message) => Console.WriteLine($"[委托同事3] 从 {from} 接收到: {message}"),
                    async (from, message) =>
                    {
                        Console.WriteLine($"[委托同事3] 异步处理来自 {from} 的消息: {message}");
                        await Task.Delay(50); // 模拟异步处理
                    }
                );

                // 3. 注册同事对象
                Console.WriteLine("1. 注册委托同事对象:");
                mediator.Register(delegateColleague1);
                mediator.Register(delegateColleague2);
                mediator.Register(delegateColleague3);

                Console.WriteLine($"当前同事数量: {mediator.ColleagueCount}\n");

                // 4. 发送消息
                Console.WriteLine("2. 发送消息:");
                delegateColleague1.Send("Delegate2", "Hello from Delegate1!");

                // 5. 广播消息
                Console.WriteLine("\n3. 广播消息:");
                delegateColleague2.Broadcast("Hello everyone!");

                // 6. 异步发送消息
                Console.WriteLine("\n4. 异步发送消息:");
                await delegateColleague3.SendAsync("Delegate1", "Async message from Delegate3!");

                // 7. 异步广播消息
                Console.WriteLine("\n5. 异步广播消息:");
                await delegateColleague1.BroadcastAsync("Async broadcast from Delegate1!");
            }

            Console.WriteLine("\n=== 委托示例结束 ===\n");
        }

        public static async Task RunExample()
        {
            await RunBasicExample();
            await RunDelegateExample();
        }
    }
}