using DataManager.Log;
using Secs;
using SecsGem.Runtime.Tests.Fakes;
using Secs.Variable;
using Secs.Variable.Data;

namespace SecsGem.Runtime.Tests.Infrastructure;

internal static class RuntimeTestFixture
{
    internal static SecsGemRuntime Create(InMemorySecsTransport transport)
    {
        var container = new TestPrismContainer();
        container.RegisterSingleton(typeof(LogManager), () => new LogManager());
        foreach (var type in new[]
        {
            typeof(ReportID), typeof(CEID), typeof(DVID), typeof(SVID), typeof(ECID),
            typeof(Alarm), typeof(ActiveReceive)
        })
        {
            container.RegisterSingleton(type, type);
        }
        new SecsGemModule().RegisterTypes(container);
        var runtime = new SecsGemRuntime(container, transport);
        container.RegisterInstance(typeof(SecsGemRuntime), runtime);
        return runtime;
    }
}
