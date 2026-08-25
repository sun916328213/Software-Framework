using Communication.Buffers;
using Communication.Communication.Socket;
 
using Communication.Protocols.Mitsubishi;
using Prism.DryIoc;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CommunicationDemo
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<CommPortManager>();
            containerRegistry.RegisterSingleton<ICommunicationBuffer, CommunicationBuffer>();

            containerRegistry.Register<MC3EHelper>("test_MC3E");
        }
    }
}
