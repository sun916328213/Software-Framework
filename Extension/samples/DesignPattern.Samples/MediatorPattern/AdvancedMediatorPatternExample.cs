using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Extension.Patterns.Coordination.Mediator;
using Extension.Patterns.Coordination.Mediator.Advanced;

namespace Extension.DesignPattern.MediatorPattern.Examples
{
    /// <summary>
    /// 系统事件数据类
    /// </summary>
    public class SystemEvent
    {
        public string EventType { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }

        public SystemEvent(string eventType, string source, string message)
        {
            EventType = eventType;
            Source = source;
            Message = message;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"Event: {EventType}, Source: {Source}, Message: {Message}, Time: {Timestamp:HH:mm:ss.fff}";
        }
    }

    /// <summary>
    /// 系统监控同事实现
    /// </summary>
    public class SystemMonitorColleague : ColleagueBase
    {
        public SystemMonitorColleague(string id) : base(id)
        {
        }

        public override void Receive(string from, object message)
        {
            var priorityStr = GetPriorityString(message);
            Console.WriteLine($"[系统监控 - {Id}] [{priorityStr}] 从 {from} 接收到: {message}");

            if (message is PriorityMessage priorityMessage)
            {
                if (priorityMessage.Data is SystemEvent systemEvent)
                {
                    Console.WriteLine($"  处理系统事件: {systemEvent}");
                }
            }
        }

        private string GetPriorityString(object message)
        {
            if (message is PriorityMessage priorityMessage)
            {
                switch (priorityMessage.Priority)
                {
                    case MessagePriority.Critical: return "CRITICAL";
                    case MessagePriority.High: return "HIGH";
                    case MessagePriority.Normal: return "NORMAL";
                    case MessagePriority.Low: return "LOW";
                    default: return "UNKNOWN";
                }
            }
            return "NORMAL";
        }
    }

    /// <summary>
    /// 事件类型过滤器
    /// </summary>
    public class EventTypeFilter : IMessageFilter
    {
        private readonly HashSet<string> _allowedEventTypes;

        public EventTypeFilter(IEnumerable<string> allowedEventTypes)
        {
            _allowedEventTypes = new HashSet<string>(allowedEventTypes ?? throw new ArgumentNullException(nameof(allowedEventTypes)));
        }

        public bool ShouldProcess(PriorityMessage message)
        {
            if (message.Data is SystemEvent systemEvent)
            {
                return _allowedEventTypes.Contains(systemEvent.EventType);
            }
            return false;
        }
    }

    /// <summary>
    /// 高级中介者模式使用示例
    /// </summary>
    public class AdvancedMediatorPatternExample
    {
        public static async Task RunPriorityExample()
        {
            Console.WriteLine("=== 优先级中介者模式示例 ===\n");

            // 1. 创建高级中介者
            using (var mediator = new AdvancedMediator())
            {
                // 2. 创建不同类型的同事对象
                var systemMonitor = new SystemMonitorColleague("SystemMonitor");
                var securityMonitor = new SystemMonitorColleague("SecurityMonitor");
                var networkMonitor = new SystemMonitorColleague("NetworkMonitor");

                // 3. 注册同事对象
                Console.WriteLine("1. 注册同事对象:");
                mediator.Register(systemMonitor);
                mediator.Register(securityMonitor);
                mediator.Register(networkMonitor);

                Console.WriteLine($"当前同事数量: {mediator.ColleagueCount}\n");

                // 4. 发送不同优先级的消息
                Console.WriteLine("2. 发送不同优先级的消息:");

                var criticalEvent = new SystemEvent("系统崩溃", "Kernel", "系统内核出现严重错误");
                mediator.SendWithPriority("Sender", "SystemMonitor", criticalEvent, MessagePriority.Critical);

                var highEvent = new SystemEvent("警告", "Memory", "内存使用率超过90%");
                await mediator.SendWithPriorityAsync("Sender", "SecurityMonitor", highEvent, MessagePriority.High);

                var normalEvent = new SystemEvent("信息", "Update", "系统更新检查完成");
                mediator.SendWithPriority("Sender", "NetworkMonitor", normalEvent, MessagePriority.Normal);

                var lowEvent = new SystemEvent("调试", "Cache", "缓存清理完成");
                mediator.SendWithPriority("Sender", "SystemMonitor", lowEvent, MessagePriority.Low);
            }

            Console.WriteLine("\n=== 优先级示例结束 ===\n");
        }

        public static async Task RunFilteredExample()
        {
            Console.WriteLine("=== 过滤器中介者模式示例 ===\n");

            // 1. 创建高级中介者
            using (var mediator = new AdvancedMediator())
            {
                // 2. 创建基础同事对象
                var systemMonitor = new SystemMonitorColleague("SystemMonitor");
                var securityMonitor = new SystemMonitorColleague("SecurityMonitor");
                var networkMonitor = new SystemMonitorColleague("NetworkMonitor");

                // 3. 创建带过滤器的同事对象
                var errorFilter = new EventTypeFilter(new[] { "错误", "警告", "系统崩溃" });
                var securityFilter = new EventTypeFilter(new[] { "安全警报", "入侵检测", "认证失败" });

                // 4. 注册带过滤器的同事对象
                Console.WriteLine("1. 注册带过滤器的同事对象:");
                mediator.Register(systemMonitor); // 接收所有消息
                mediator.RegisterWithFilter(securityMonitor, errorFilter); // 只接收错误相关消息
                mediator.RegisterWithFilter(networkMonitor, securityFilter); // 只接收安全相关消息

                Console.WriteLine($"当前同事数量: {mediator.ColleagueCount}\n");

                // 5. 发送不同类型的消息
                Console.WriteLine("2. 发送不同类型的消息:");

                // 安全相关消息
                var securityEvent = new SystemEvent("安全警报", "Firewall", "检测到可疑网络活动");
                mediator.Send("Sender", "NetworkMonitor", new PriorityMessage(securityEvent, MessagePriority.High, "security"));

                // 错误相关消息
                var errorEvent = new SystemEvent("错误", "Database", "数据库连接失败");
                mediator.Send("Sender", "SecurityMonitor", new PriorityMessage(errorEvent, MessagePriority.High, "system"));

                // 普通信息消息
                var infoEvent = new SystemEvent("信息", "Backup", "数据备份完成");
                mediator.Send("Sender", "SystemMonitor", new PriorityMessage(infoEvent, MessagePriority.Normal, "system"));

                // 网络相关消息（只有networkMonitor会收到）
                var networkEvent = new SystemEvent("网络状态", "Router", "网络连接恢复正常");
                await mediator.SendAsync("Sender", "NetworkMonitor", new PriorityMessage(networkEvent, MessagePriority.Low, "network"));
            }

            Console.WriteLine("\n=== 过滤器示例结束 ===\n");
        }

        public static async Task RunBroadcastExample()
        {
            Console.WriteLine("=== 广播中介者模式示例 ===\n");

            // 1. 创建高级中介者
            using (var mediator = new AdvancedMediator())
            {
                // 2. 创建不同类型的同事对象
                var systemMonitor = new SystemMonitorColleague("SystemMonitor");
                var securityMonitor = new SystemMonitorColleague("SecurityMonitor");
                var networkMonitor = new SystemMonitorColleague("NetworkMonitor");
                var logMonitor = new SystemMonitorColleague("LogMonitor");

                // 3. 注册同事对象
                Console.WriteLine("1. 注册同事对象:");
                mediator.Register(systemMonitor);
                mediator.Register(securityMonitor);
                mediator.Register(networkMonitor);
                mediator.Register(logMonitor);

                Console.WriteLine($"当前同事数量: {mediator.ColleagueCount}\n");

                // 4. 广播不同优先级的消息
                Console.WriteLine("2. 广播不同优先级的消息:");

                var criticalUpdate = new SystemEvent("系统更新", "Updater", "关键安全更新已安装");
                mediator.BroadcastWithPriority("Admin", criticalUpdate, MessagePriority.Critical);

                var securityAlert = new SystemEvent("安全警报", "Firewall", "检测到潜在威胁");
                await mediator.BroadcastWithPriorityAsync("Security", securityAlert, MessagePriority.High);

                var normalInfo = new SystemEvent("信息", "Monitor", "系统运行正常");
                mediator.BroadcastWithPriority("Monitor", normalInfo, MessagePriority.Normal);

                var debugInfo = new SystemEvent("调试", "Debugger", "调试信息输出");
                mediator.BroadcastWithPriority("Debugger", debugInfo, MessagePriority.Low);
            }

            Console.WriteLine("\n=== 广播示例结束 ===\n");
        }

        public static async Task RunRoutingExample()
        {
            Console.WriteLine("=== 路由中介者模式示例 ===\n");

            // 1. 创建多个中介者作为不同通道
            using (var systemMediator = new AdvancedMediator())
            using (var securityMediator = new AdvancedMediator())
            using (var networkMediator = new AdvancedMediator())
            {
                // 2. 创建消息路由器
                var router = new MessageRouter(message =>
                {
                    // 根据事件类型路由到不同通道
                    if (message.Data is SystemEvent systemEvent)
                    {
                        switch (systemEvent.EventType)
                        {
                            case "系统更新":
                            case "错误":
                            case "警告":
                                return "system";
                            case "安全警报":
                            case "认证失败":
                            case "入侵检测":
                                return "security";
                            case "网络状态":
                            case "带宽使用":
                                return "network";
                            default:
                                return "system"; // 默认路由到系统通道
                        }
                    }
                    return "system"; // 默认路由到系统通道
                });

                // 3. 注册通道
                router.RegisterChannel("system", systemMediator);
                router.RegisterChannel("security", securityMediator);
                router.RegisterChannel("network", networkMediator);

                // 4. 为每个通道创建同事对象
                var systemMonitor = new SystemMonitorColleague("SystemMonitor");
                var securityMonitor = new SystemMonitorColleague("SecurityMonitor");
                var networkMonitor = new SystemMonitorColleague("NetworkMonitor");

                systemMediator.Register(systemMonitor);
                securityMediator.Register(securityMonitor);
                networkMediator.Register(networkMonitor);

                // 5. 通过路由器发送消息
                Console.WriteLine("1. 通过路由器发送消息:");

                var updateEvent = new SystemEvent("系统更新", "Updater", "系统已更新到最新版本");
                router.RouteAndSend("Admin", "SystemMonitor", new PriorityMessage(updateEvent, MessagePriority.Normal));

                var securityEvent = new SystemEvent("安全警报", "Firewall", "检测到潜在威胁");
                await router.RouteAndSendAsync("Security", "SecurityMonitor", new PriorityMessage(securityEvent, MessagePriority.High));

                var networkEvent = new SystemEvent("网络状态", "Router", "网络连接稳定");
                router.RouteAndBroadcast("Network", new PriorityMessage(networkEvent, MessagePriority.Low));
            }

            Console.WriteLine("\n=== 路由示例结束 ===\n");
        }

        public static async Task RunExample()
        {
            await RunPriorityExample();
            await RunFilteredExample();
            await RunBroadcastExample();
            await RunRoutingExample();
        }
    }
}