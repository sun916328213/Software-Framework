using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using Xunit;

namespace CommunicationNew.Tests.Core;

public sealed class DeviceValueTests
{
    [Fact]
    public void Failed_conversion_is_reported_instead_of_returning_default()
    {
        var value = new DeviceValue("not-a-number", DataPointType.String);

        var exception = Assert.Throws<InvalidCastException>(() => value.GetValue<int>());

        Assert.Contains(typeof(string).FullName!, exception.Message);
        Assert.Contains(typeof(int).FullName!, exception.Message);
        Assert.NotNull(exception.InnerException);
    }
}
