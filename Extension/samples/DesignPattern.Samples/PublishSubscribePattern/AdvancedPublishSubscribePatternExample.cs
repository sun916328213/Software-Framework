using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Extension.DesignPattern.PublishSubscribePattern;
using Extension.DesignPattern.PublishSubscribePattern.Advanced;

namespace Extension.DesignPattern.PublishSubscribePattern.Examples
{
    /// <summary>
    /// 系统事件数据类
    /// </summary>
    public class SystemEventData
    {
        public string EventType { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }

        public SystemEventData(string eventType, string source, string message)
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
    /// 系统监控订阅者（处理PriorityMessage<SystemEventData>类型）
    /// </summary>
    public class PrioritySystemMonitorSubscriber : SubscriberBase<PriorityMessage<SystemEventData>>
    {
        public PrioritySystemMonitorSubscriber(string name) : base(name)
        {
        }

        public override void HandleMessage(PriorityMessage<SystemEventData> message)
        {
            var priorityStr = GetPriorityString(message.Priority);
            Console.WriteLine($"[系统监控 - {Name}] [{priorityStr}] 接收到事件: {message.Data}");
        }

        private string GetPriorityString(MessagePriority priority)
        {
            switch (priority)
            {
                case MessagePriority.Critical: return "CRITICAL";
                case MessagePriority.High: return "HIGH";
                case MessagePriority.Normal: return "NORMAL";
                case MessagePriority.Low: return "LOW";
                default: return "UNKNOWN";
            }
        }
    }

    /// <summary>
    /// 系统监控订阅者（处理SystemEventData类型）
    /// </summary>
    public class SystemMonitorSubscriber : SubscriberBase<SystemEventData>
    {
        public SystemMonitorSubscriber(string name) : base(name)
        {
        }

        public override void HandleMessage(SystemEventData message)
        {
            Console.WriteLine($"[系统监控 - {Name}] 接收到事件: {message}");
        }
    }

    /// <summary>
    /// 事件类型过滤器
    /// </summary>
    public class EventTypeFilter : IMessageFilter<PriorityMessage<SystemEventData>>
    {
        private readonly HashSet<string> _allowedEventTypes;

        public EventTypeFilter(IEnumerable<string> allowedEventTypes)
        {
            _allowedEventTypes = new HashSet<string>(allowedEventTypes ?? throw new ArgumentNullException(nameof(allowedEventTypes)));
        }

        public bool ShouldProcess(PriorityMessage<SystemEventData> message)
        {
            return _allowedEventTypes.Contains(message.Data.EventType);
        }
    }

    /// <summary>
    /// 高级发布订阅模式使用示例
    /// </summary>
    public class AdvancedPublishSubscribePatternExample
    {
        public static async Task RunPriorityExample()
        {
            Console.WriteLine("=== 优先级发布订阅模式示例 ===\n");

            // 1. 创建高级消息中心
            using (var messageBroker = new Advanced.AdvancedMessageBroker<SystemEventData>())
            {
                // 2. 创建不同优先级的订阅者
                var highPrioritySubscriber = new PrioritySystemMonitorSubscriber("高优先级监控");
                var normalPrioritySubscriber = new PrioritySystemMonitorSubscriber("普通优先级监控");
                var lowPrioritySubscriber = new PrioritySystemMonitorSubscriber("低优先级监控");

                // 3. 订阅消息
                Console.WriteLine("1. 订阅消息:");
                messageBroker.Subscribe(highPrioritySubscriber);
                messageBroker.Subscribe(normalPrioritySubscriber);
                messageBroker.Subscribe(lowPrioritySubscriber);

                Console.WriteLine($"当前订阅者数量: {messageBroker.SubscriberCount}\n");

                // 4. 发布不同优先级的消息
                Console.WriteLine("2. 发布不同优先级的消息:");
                
                var criticalMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("系统崩溃", "Kernel", "系统内核出现严重错误"),
                    MessagePriority.Critical,
                    "system"
                );
                messageBroker.Publish(criticalMessage);

                var highMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("警告", "Memory", "内存使用率超过90%"),
                    MessagePriority.High,
                    "system"
                );
                await messageBroker.PublishAsync(highMessage);

                var normalMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("信息", "Update", "系统更新检查完成"),
                    MessagePriority.Normal,
                    "system"
                );
                messageBroker.Publish(normalMessage);

                var lowMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("调试", "Cache", "缓存清理完成"),
                    MessagePriority.Low,
                    "system"
                );
                messageBroker.Publish(lowMessage);
            }

            Console.WriteLine("\n=== 优先级示例结束 ===\n");
        }

        public static async Task RunFilteredExample()
        {
            Console.WriteLine("=== 过滤器发布订阅模式示例 ===\n");

            // 1. 创建高级消息中心
            using (var messageBroker = new Advanced.AdvancedMessageBroker<SystemEventData>())
            {
                // 2. 创建基础订阅者
                var systemSubscriber = new PrioritySystemMonitorSubscriber("系统监控");
                var securitySubscriber = new PrioritySystemMonitorSubscriber("安全监控");
                var networkSubscriber = new PrioritySystemMonitorSubscriber("网络监控");

                // 3. 创建带过滤器的订阅者
                var errorFilter = new EventTypeFilter(new[] { "错误", "警告", "系统崩溃" });
                var securityFilter = new EventTypeFilter(new[] { "安全警报", "入侵检测", "认证失败" });

                var filteredSystemSubscriber = new FilteredSubscriber<PriorityMessage<SystemEventData>>(
                    systemSubscriber, errorFilter);
                var filteredSecuritySubscriber = new FilteredSubscriber<PriorityMessage<SystemEventData>>(
                    securitySubscriber, securityFilter);

                // 4. 订阅消息
                Console.WriteLine("1. 订阅带过滤器的消息:");
                messageBroker.Subscribe(networkSubscriber); // 接收所有消息
                messageBroker.Subscribe(filteredSystemSubscriber); // 只接收错误相关消息
                messageBroker.Subscribe(filteredSecuritySubscriber); // 只接收安全相关消息

                Console.WriteLine($"当前订阅者数量: {messageBroker.SubscriberCount}\n");

                // 5. 发布不同类型的消息
                Console.WriteLine("2. 发布不同类型的消息:");

                // 安全相关消息
                var securityMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("安全警报", "Firewall", "检测到可疑网络活动"),
                    MessagePriority.High,
                    "security"
                );
                messageBroker.Publish(securityMessage);

                // 错误相关消息
                var errorMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("错误", "Database", "数据库连接失败"),
                    MessagePriority.High,
                    "system"
                );
                messageBroker.Publish(errorMessage);

                // 普通信息消息
                var infoMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("信息", "Backup", "数据备份完成"),
                    MessagePriority.Normal,
                    "system"
                );
                messageBroker.Publish(infoMessage);

                // 网络相关消息（只有networkSubscriber会收到）
                var networkMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("网络状态", "Router", "网络连接恢复正常"),
                    MessagePriority.Normal,
                    "network"
                );
                await messageBroker.PublishAsync(networkMessage);
            }

            Console.WriteLine("\n=== 过滤器示例结束 ===\n");
        }

        public static async Task RunTopicExample()
        {
            Console.WriteLine("=== 主题发布订阅模式示例 ===\n");

            // 1. 创建高级消息中心
            using (var messageBroker = new Advanced.AdvancedMessageBroker<SystemEventData>())
            {
                // 2. 创建不同主题的订阅者
                var systemSubscriber = new PrioritySystemMonitorSubscriber("系统监控");
                var securitySubscriber = new PrioritySystemMonitorSubscriber("安全监控");
                var networkSubscriber = new PrioritySystemMonitorSubscriber("网络监控");

                // 3. 订阅特定主题
                Console.WriteLine("1. 订阅特定主题:");
                messageBroker.SubscribeToTopic("system", systemSubscriber);
                messageBroker.SubscribeToTopic("security", securitySubscriber);
                messageBroker.SubscribeToTopic("network", networkSubscriber);

                Console.WriteLine($"当前订阅者数量: {messageBroker.SubscriberCount}\n");

                // 4. 发布到特定主题
                Console.WriteLine("2. 发布到特定主题:");

                var systemMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("系统更新", "OS", "操作系统更新完成"),
                    MessagePriority.Normal,
                    "system"
                );
                messageBroker.PublishToTopic("system", systemMessage);

                var securityMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("认证成功", "Auth", "用户认证成功"),
                    MessagePriority.Normal,
                    "security"
                );
                await messageBroker.PublishToTopicAsync("security", securityMessage);

                var networkMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("带宽使用", "Monitor", "当前带宽使用率30%"),
                    MessagePriority.Low,
                    "network"
                );
                messageBroker.PublishToTopic("network", networkMessage);

                // 5. 发布到不存在的主题
                Console.WriteLine("\n3. 发布到不存在的主题:");
                var unknownMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("测试", "Test", "测试消息"),
                    MessagePriority.Low,
                    "unknown"
                );
                messageBroker.PublishToTopic("unknown", unknownMessage);
            }

            Console.WriteLine("\n=== 主题示例结束 ===\n");
        }

        public static async Task RunRoutingExample()
        {
            Console.WriteLine("=== 消息路由发布订阅模式示例 ===\n");

            // 1. 创建多个消息中心作为不同通道
            using (var systemBroker = new Advanced.AdvancedMessageBroker<SystemEventData>())
            using (var securityBroker = new Advanced.AdvancedMessageBroker<SystemEventData>())
            using (var networkBroker = new Advanced.AdvancedMessageBroker<SystemEventData>())
            {
                // 2. 创建消息路由器
                var router = new MessageRouter<SystemEventData>(message =>
                {
                    // 根据事件类型路由到不同通道
                    switch (message.Data.EventType)
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
                });

                // 3. 注册通道
                router.RegisterChannel("system", systemBroker);
                router.RegisterChannel("security", securityBroker);
                router.RegisterChannel("network", networkBroker);

                // 4. 为每个通道创建订阅者
                var systemSubscriber = new PrioritySystemMonitorSubscriber("系统监控");
                var securitySubscriber = new PrioritySystemMonitorSubscriber("安全监控");
                var networkSubscriber = new PrioritySystemMonitorSubscriber("网络监控");

                systemBroker.Subscribe(systemSubscriber);
                securityBroker.Subscribe(securitySubscriber);
                networkBroker.Subscribe(networkSubscriber);

                // 5. 通过路由器发布消息
                Console.WriteLine("1. 通过路由器发布消息:");

                var updateMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("系统更新", "Updater", "系统已更新到最新版本"),
                    MessagePriority.Normal,
                    "system"
                );
                router.RouteAndPublish(updateMessage);

                var securityMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("安全警报", "Firewall", "检测到潜在威胁"),
                    MessagePriority.High,
                    "security"
                );
                await router.RouteAndPublishAsync(securityMessage);

                var networkMessage = new PriorityMessage<SystemEventData>(
                    new SystemEventData("网络状态", "Router", "网络连接稳定"),
                    MessagePriority.Low,
                    "network"
                );
                router.RouteAndPublish(networkMessage);
            }

            Console.WriteLine("\n=== 消息路由示例结束 ===\n");
        }

        public static async Task RunExample()
        {
            await RunPriorityExample();
            await RunFilteredExample();
            await RunTopicExample();
            await RunRoutingExample();
        }
    }
}