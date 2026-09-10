using System.Windows;
using System.Windows.Threading;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

namespace FlowEditorTest;

/// <summary>让所有需要 Application 资源的 UI 测试复用同一个 STA Dispatcher。</summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Host> SharedHost = new(() => new Host());

    public static void Run(Action action) => SharedHost.Value.Dispatcher.Invoke(action);

    private sealed class Host
    {
        public Dispatcher Dispatcher { get; }

        public Host()
        {
            using var ready = new ManualResetEventSlim();
            Dispatcher? dispatcher = null;
            var thread = new Thread(() =>
            {
                var application = Application.Current ?? new Application();
                EnsureResources(application);
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "FlowEditor WPF test host"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            Dispatcher = dispatcher!;
        }
    }

    private static void EnsureResources(Application application)
    {
        application.Resources.MergedDictionaries.Add(new BundledTheme
        {
            BaseTheme = BaseTheme.Light,
            PrimaryColor = PrimaryColor.Teal,
            SecondaryColor = SecondaryColor.Amber
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/FlowEditor;component/Resources/Resources.xaml")
        });
    }
}
