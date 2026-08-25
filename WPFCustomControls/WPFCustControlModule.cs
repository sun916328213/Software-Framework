using Extension.Core.ReflectionEx;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.ViewModels;
using WPFCustomControls.Window.Views;

namespace WPFCustomControls
{
    /// <summary>
    /// WPFCustomControls Prism 模块。
    /// 负责注册所有类型到 DI 容器、合并资源字典。
    /// </summary>
    public class WPFCustomControlsModule : IModule
    {
        /// <summary>
        /// 注册 WPFCustomControls 模块类型到 Prism 容器
        /// </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 批量注册 Controls 命名空间下的导航视图（单例 + (object, 导航名) 单例）
            containerRegistry.RegisterNavigationViewsFromNamespace(Assembly.GetExecutingAssembly(), "WPFCustomControls.Controls");

            containerRegistry.Register<FileLoading>();
            containerRegistry.Register<MessagePrompt>();
            containerRegistry.Register<ProgramLoading>();

            // ── PropertyGrid 服务层（IoC 管理生命周期） ──
            containerRegistry.RegisterSingleton<IPropertyGridBuilderService, PropertyGridBuilderService>();
            containerRegistry.RegisterSingleton<IPortDialogService, PortDialogService>();
            // 属性面板根 VM：瞬态，每个容器(窗口)实例拥有独立的属性面板状态
            containerRegistry.Register<PropertyGridHostViewModel>();
        }

        /// <summary>
        /// 模块初始化完成后调用
        /// </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            MergeResourceDictionaries();
        }

        /// <summary>
        /// 合并 WPFCustomControls 资源字典到应用程序资源中
        /// </summary>
        private static void MergeResourceDictionaries()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var resourceDict = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml")
                };
                app.Resources.MergedDictionaries.Add(resourceDict);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WPFCustomControls 资源合并失败: {ex.Message}");
            }
        }
    }
}
