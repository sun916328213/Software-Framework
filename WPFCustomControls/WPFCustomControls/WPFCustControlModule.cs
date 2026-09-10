using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Windows;
using WPFCustomControls.Common.Composition;

namespace WPFCustomControls;

/// <summary>
/// WPFCustomControls composition root. Feature registration is delegated to focused registrars;
/// this module only orchestrates startup and shared resource initialization.
/// </summary>
public sealed class WPFCustomControlsModule : IModule
{
    private static readonly IDependencyRegistrar[] Registrars =
    {
        new ControlsDependencyRegistrar(),
        new WindowDependencyRegistrar(),
        new PropertyGridDependencyRegistrar(),
    };

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        ArgumentNullException.ThrowIfNull(containerRegistry);

        foreach (var registrar in Registrars)
            registrar.Register(containerRegistry);
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        MergeResourceDictionaries();
    }

    private static void MergeResourceDictionaries()
    {
        try
        {
            if (Application.Current is not { } application)
                return;

            // 幂等：同一字典只合一次，避免重复 StaticResource 绑定到旧实例。
            var uri = new Uri("pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml");
            foreach (var existing in application.Resources.MergedDictionaries)
            {
                if (existing.Source is not null && existing.Source.Equals(uri))
                    return;
            }

            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = uri
            });
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"WPFCustomControls 资源合并失败: {exception.Message}");
        }
    }
}
