using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;
using Xunit;

namespace CommunicationNew.Tests.Core;

public sealed class DataPointDefinitionTests
{
    [Fact]
    public void Zero_length_uses_type_default_for_both_materialization_paths()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = "HR1",
            DataType = DataPointType.Int32,
            LengthBytes = 0,
        };

        var standalone = definition.ToDataPoint();
        var collection = new DataPointCollection();
        var added = collection.AddDefinition(definition);

        Assert.Equal(4, standalone.Length);
        Assert.Equal(4, added.Length);
    }

    [Fact]
    public void Negative_length_is_rejected_when_definition_is_materialized()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = "HR1",
            DataType = DataPointType.Int32,
            LengthBytes = -1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ToDataPoint());
    }

    [Fact]
    public void Unsupported_data_type_is_rejected_even_when_length_is_configured()
    {
        var definition = new DataPointDefinition
        {
            Name = "Unknown",
            Address = "HR1",
            DataType = (DataPointType)int.MaxValue,
            LengthBytes = 8,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ToDataPoint());
    }

    [Fact]
    public void Fixed_width_data_type_rejects_length_shorter_than_its_value()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = "HR1",
            DataType = DataPointType.Int32,
            LengthBytes = 2,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ToDataPoint());
    }

    [Fact]
    public void Blank_name_is_rejected_when_definition_is_materialized()
    {
        var definition = new DataPointDefinition
        {
            Name = " ",
            Address = "HR1",
        };

        Assert.Throws<ArgumentException>(() => definition.ToDataPoint());
    }

    [Fact]
    public void Blank_address_is_rejected_when_definition_is_materialized()
    {
        var definition = new DataPointDefinition
        {
            Name = "Counter",
            Address = " ",
        };

        Assert.Throws<ArgumentException>(() => definition.ToDataPoint());
    }
}
