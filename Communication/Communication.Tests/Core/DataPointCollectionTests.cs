using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using Xunit;

namespace CommunicationNew.Tests.Core;

public sealed class DataPointCollectionTests
{
    [Fact]
    public void AddRange_failure_does_not_partially_change_the_collection()
    {
        var collection = new DataPointCollection();
        collection.Add(new DataPoint("Existing", DataPointType.Int16, "HR1"));
        var points = new IDataPoint[]
        {
            new DataPoint("New", DataPointType.Int16, "HR2"),
            new DataPoint("Existing", DataPointType.Int16, "HR3"),
        };

        Assert.Throws<ArgumentException>(() => collection.AddRange(points));

        Assert.Equal(1, collection.Count);
        Assert.False(collection.Contains("New"));
    }

    [Fact]
    public void AddDefinitions_failure_does_not_partially_change_the_collection()
    {
        var collection = new DataPointCollection();
        collection.Add(new DataPoint("Existing", DataPointType.Int16, "HR1"));
        var definitions = new[]
        {
            new DataPointDefinition { Name = "New", Address = "HR2" },
            new DataPointDefinition { Name = "Existing", Address = "HR3" },
        };

        Assert.Throws<ArgumentException>(() => collection.AddDefinitions(definitions));

        Assert.Equal(1, collection.Count);
        Assert.False(collection.Contains("New"));
    }

    [Fact]
    public void All_does_not_allow_bypassing_collection_invariants()
    {
        var collection = new DataPointCollection();
        collection.Add(new DataPoint("Existing", DataPointType.Int16, "HR1"));
        var exposed = Assert.IsAssignableFrom<ICollection<IDataPoint>>(collection.All);

        Assert.Throws<NotSupportedException>(() =>
            exposed.Add(new DataPoint("Injected", DataPointType.Int16, "HR2")));

        Assert.Equal(1, collection.Count);
        Assert.False(collection.Contains("Injected"));
    }
}
