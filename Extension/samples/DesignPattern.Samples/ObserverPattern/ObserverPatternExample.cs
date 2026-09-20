using System;
using System.Threading.Tasks;
using Extension.Patterns.Messaging.Observer;

namespace Extension.DesignPattern.ObserverPattern.Examples
{
    /// <summary>
    /// 新闻数据类
    /// </summary>
    public class NewsData
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime PublishTime { get; set; }

        public NewsData(string title, string content)
        {
            Title = title;
            Content = content;
            PublishTime = DateTime.Now;
        }

        public override string ToString()
        {
            return $"标题: {Title}, 内容: {Content}, 发布时间: {PublishTime}";
        }
    }

    /// <summary>
    /// 新闻发布者类
    /// </summary>
    public class NewsPublisher : Observable<NewsData>
    {
        public string PublisherName { get; set; }

        public NewsPublisher(string publisherName)
        {
            PublisherName = publisherName;
        }

        public void PublishNews(string title, string content)
        {
            var news = new NewsData(title, content);
            Console.WriteLine($"\n{PublisherName} 发布新闻: {news.Title}");
            Data = news; // 这会自动通知所有观察者
        }
    }

    /// <summary>
    /// 邮件订阅者类
    /// </summary>
    public class EmailSubscriber : Observer<NewsData>
    {
        public string EmailAddress { get; set; }

        public EmailSubscriber(string emailAddress)
        {
            EmailAddress = emailAddress;
        }

        public override void HandleUpdate(IObservable subject, NewsData data)
        {
            if (data != null)
            {
                Console.WriteLine($"[邮件通知] 发送到 {EmailAddress}: 新闻 '{data.Title}' 已发布");
            }
        }
    }

    /// <summary>
    /// SMS订阅者类
    /// </summary>
    public class SMSSubscriber : Observer<NewsData>
    {
        public string PhoneNumber { get; set; }

        public SMSSubscriber(string phoneNumber)
        {
            PhoneNumber = phoneNumber;
        }

        public override void HandleUpdate(IObservable subject, NewsData data)
        {
            if (data != null)
            {
                Console.WriteLine($"[短信通知] 发送到 {PhoneNumber}: 新闻 '{data.Title}' 已发布");
            }
        }
    }

    /// <summary>
    /// 控制台订阅者类
    /// </summary>
    public class ConsoleSubscriber : IObserver
    {
        public string Name { get; set; }

        public ConsoleSubscriber(string name)
        {
            Name = name;
        }

        public void Update(IObservable subject)
        {
            if (subject is NewsPublisher publisher && publisher.Data != null)
            {
                Console.WriteLine($"[控制台] {Name} 收到通知: {publisher.Data}");
            }
        }
    }

    /// <summary>
    /// 观察者模式使用示例
    /// </summary>
    public class ObserverPatternExample
    {
        public static async Task RunExample()
        {
            Console.WriteLine("=== 观察者模式使用示例 ===\n");

            // 1. 创建新闻发布者
            var newsPublisher = new NewsPublisher("科技新闻网");

            // 2. 创建不同类型的订阅者
            var emailSubscriber1 = new EmailSubscriber("user1@example.com");
            var emailSubscriber2 = new EmailSubscriber("user2@example.com");
            var smsSubscriber = new SMSSubscriber("13800138000");
            var consoleSubscriber = new ConsoleSubscriber("控制台观察者");

            // 3. 注册观察者
            Console.WriteLine("1. 注册观察者:");
            newsPublisher.Attach(emailSubscriber1);
            newsPublisher.Attach(emailSubscriber2);
            newsPublisher.Attach(smsSubscriber);
            newsPublisher.Attach(consoleSubscriber);

            Console.WriteLine($"当前观察者数量: {newsPublisher.ObserverCount}\n");

            // 4. 发布新闻
            Console.WriteLine("2. 发布新闻:");
            newsPublisher.PublishNews("新技术突破", "科学家发现了一种新的能源技术...");

            // 5. 批量注册观察者
            Console.WriteLine("\n3. 批量注册观察者:");
            var batchSubscribers = new IObserver[]
            {
                new EmailSubscriber("batch1@example.com"),
                new EmailSubscriber("batch2@example.com"),
                new SMSSubscriber("13900139000")
            };
            newsPublisher.AttachRange(batchSubscribers);
            Console.WriteLine($"当前观察者数量: {newsPublisher.ObserverCount}\n");

            // 6. 再次发布新闻
            Console.WriteLine("4. 再次发布新闻:");
            newsPublisher.PublishNews("市场动态", "股市今日大幅上涨...");

            // 7. 移除部分观察者
            Console.WriteLine("\n5. 移除部分观察者:");
            newsPublisher.Detach(emailSubscriber2);
            newsPublisher.Detach(smsSubscriber);
            Console.WriteLine($"当前观察者数量: {newsPublisher.ObserverCount}\n");

            // 8. 发布第三条新闻
            Console.WriteLine("6. 发布第三条新闻:");
            newsPublisher.PublishNews("国际新闻", "两国签署重要合作协议...");

            // 9. 异步更新数据
            Console.WriteLine("\n7. 异步更新数据:");
            var result = await newsPublisher.UpdateDataAsync(
                new NewsData("异步新闻", "这是一条通过异步方式发布的新闻...")
            );
            Console.WriteLine($"异步更新结果: {(result ? "成功通知观察者" : "未通知观察者(数据未变化)")}");

            // 10. 清空所有观察者
            Console.WriteLine("\n8. 清空所有观察者:");
            newsPublisher.ClearObservers();
            Console.WriteLine($"当前观察者数量: {newsPublisher.ObserverCount}");

            // 11. 发布最后一条新闻（应该没有观察者收到通知）
            Console.WriteLine("\n9. 发布最后一条新闻（无观察者）:");
            newsPublisher.PublishNews("最终新闻", "这是最后一条新闻...");

            Console.WriteLine("\n=== 示例结束 ===");
        }
    }
}