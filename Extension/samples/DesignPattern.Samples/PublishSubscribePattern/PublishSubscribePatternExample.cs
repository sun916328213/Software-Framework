using System;
using System.Threading.Tasks;
using Extension.Patterns.Messaging.PublishSubscribe;

namespace Extension.DesignPattern.PublishSubscribePattern.Examples
{
    /// <summary>
    /// 消息数据类
    /// </summary>
    public class MessageData
    {
        public string Topic { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public MessageData(string topic, string content)
        {
            Topic = topic;
            Content = content;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"Topic: {Topic}, Content: {Content}, Time: {Timestamp:HH:mm:ss.fff}";
        }
    }

    /// <summary>
    /// 邮件服务订阅者
    /// </summary>
    public class EmailServiceSubscriber : SubscriberBase<MessageData>
    {
        public EmailServiceSubscriber(string name) : base(name)
        {
        }

        public override void HandleMessage(MessageData message)
        {
            Console.WriteLine($"[邮件服务 - {Name}] 接收到消息: {message}");
        }
    }

    /// <summary>
    /// 短信服务订阅者
    /// </summary>
    public class SMSServiceSubscriber : SubscriberBase<MessageData>
    {
        public SMSServiceSubscriber(string name) : base(name)
        {
        }

        public override void HandleMessage(MessageData message)
        {
            Console.WriteLine($"[短信服务 - {Name}] 接收到消息: {message}");
        }
    }

    /// <summary>
    /// 日志服务订阅者
    /// </summary>
    public class LoggingServiceSubscriber : SubscriberBase<MessageData>
    {
        public LoggingServiceSubscriber(string name) : base(name)
        {
        }

        public override void HandleMessage(MessageData message)
        {
            Console.WriteLine($"[日志服务 - {Name}] 记录消息: {message}");
        }
    }

    /// <summary>
    /// 发布订阅模式使用示例
    /// </summary>
    public class PublishSubscribePatternExample
    {
        public static async Task RunBasicExample()
        {
            Console.WriteLine("=== 基础发布订阅模式示例 ===\n");

            // 1. 创建消息中心
            using (var messageBroker = new MessageBroker<MessageData>())
            {
                // 2. 创建不同类型的订阅者
                var emailSubscriber = new EmailServiceSubscriber("邮件服务1");
                var smsSubscriber = new SMSServiceSubscriber("短信服务1");
                var loggingSubscriber = new LoggingServiceSubscriber("日志服务1");

                // 3. 订阅消息
                Console.WriteLine("1. 订阅消息:");
                messageBroker.Subscribe(emailSubscriber);
                messageBroker.Subscribe(smsSubscriber);
                messageBroker.Subscribe(loggingSubscriber);

                Console.WriteLine($"当前订阅者数量: {messageBroker.SubscriberCount}\n");

                // 4. 发布消息
                Console.WriteLine("2. 发布消息:");
                var message1 = new MessageData("通知", "系统维护将在今晚进行");
                messageBroker.Publish(message1);

                // 5. 批量订阅更多订阅者
                Console.WriteLine("\n3. 批量订阅更多订阅者:");
                var batchSubscribers = new ISubscriber<MessageData>[]
                {
                    new EmailServiceSubscriber("邮件服务2"),
                    new SMSServiceSubscriber("短信服务2"),
                    new LoggingServiceSubscriber("日志服务2")
                };
                messageBroker.SubscribeRange(batchSubscribers);
                Console.WriteLine($"当前订阅者数量: {messageBroker.SubscriberCount}\n");

                // 6. 再次发布消息
                Console.WriteLine("4. 再次发布消息:");
                var message2 = new MessageData("更新", "系统已更新到最新版本");
                await messageBroker.PublishAsync(message2);

                // 7. 取消部分订阅
                Console.WriteLine("\n5. 取消部分订阅:");
                messageBroker.Unsubscribe(smsSubscriber);
                messageBroker.Unsubscribe(loggingSubscriber);
                Console.WriteLine($"当前订阅者数量: {messageBroker.SubscriberCount}\n");

                // 8. 发布第三条消息
                Console.WriteLine("6. 发布第三条消息:");
                var message3 = new MessageData("警报", "系统检测到异常活动");
                messageBroker.Publish(message3);

                // 9. 清空所有订阅者
                Console.WriteLine("\n7. 清空所有订阅者:");
                messageBroker.ClearSubscribers();
                Console.WriteLine($"当前订阅者数量: {messageBroker.SubscriberCount}");

                // 10. 发布最后一条消息（应该没有订阅者收到通知）
                Console.WriteLine("\n8. 发布最后一条消息（无订阅者）:");
                var message4 = new MessageData("最终通知", "这是最后一条通知消息");
                messageBroker.Publish(message4);
            }

            Console.WriteLine("\n=== 基础示例结束 ===\n");
        }

        public static async Task RunAdvancedExample()
        {
            Console.WriteLine("=== 高级发布订阅模式示例 ===\n");

            // 1. 创建高级消息中心
            using (var advancedMessageBroker = new AdvancedMessageBroker<MessageData>())
            {
                // 2. 创建不同类型的订阅者
                var emailSubscriber = new EmailServiceSubscriber("邮件服务");
                var smsSubscriber = new SMSServiceSubscriber("短信服务");
                var loggingSubscriber = new LoggingServiceSubscriber("日志服务");

                // 3. 订阅所有消息
                Console.WriteLine("1. 订阅所有消息:");
                advancedMessageBroker.Subscribe(emailSubscriber);
                advancedMessageBroker.Subscribe(loggingSubscriber);

                // 4. 订阅特定主题
                Console.WriteLine("\n2. 订阅特定主题:");
                advancedMessageBroker.SubscribeToTopic("紧急通知", smsSubscriber);
                advancedMessageBroker.SubscribeToTopic("系统更新", smsSubscriber);

                Console.WriteLine($"当前订阅者数量: {advancedMessageBroker.SubscriberCount}\n");

                // 5. 发布通用消息（所有订阅者都会收到）
                Console.WriteLine("3. 发布通用消息:");
                var generalMessage = new MessageData("通用", "这是一条通用消息");
                advancedMessageBroker.Publish(generalMessage);

                // 6. 发布特定主题消息
                Console.WriteLine("\n4. 发布特定主题消息:");
                var urgentMessage = new MessageData("紧急通知", "系统出现紧急情况");
                advancedMessageBroker.PublishToTopic("紧急通知", urgentMessage);

                var updateMessage = new MessageData("系统更新", "系统已更新到v2.0");
                await advancedMessageBroker.PublishToTopicAsync("系统更新", updateMessage);

                // 7. 取消订阅特定主题
                Console.WriteLine("\n5. 取消订阅特定主题:");
                advancedMessageBroker.UnsubscribeFromTopic("系统更新", smsSubscriber);

                // 8. 再次发布系统更新消息（SMS订阅者不会再收到）
                Console.WriteLine("\n6. 再次发布系统更新消息:");
                var updateMessage2 = new MessageData("系统更新", "系统更新已完成");
                advancedMessageBroker.PublishToTopic("系统更新", updateMessage2);

                // 9. 取消所有订阅
                Console.WriteLine("\n7. 取消所有订阅:");
                advancedMessageBroker.Unsubscribe(emailSubscriber);
                advancedMessageBroker.Unsubscribe(smsSubscriber);
                advancedMessageBroker.Unsubscribe(loggingSubscriber);
                Console.WriteLine($"当前订阅者数量: {advancedMessageBroker.SubscriberCount}");
            }

            Console.WriteLine("\n=== 高级示例结束 ===\n");
        }

        public static async Task RunExample()
        {
            await RunBasicExample();
            await RunAdvancedExample();
        }
    }
}