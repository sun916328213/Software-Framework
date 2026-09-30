using SecsGem.Core;

namespace SecsGem.Tests.Fakes;

internal sealed class FakeEquipmentDataProvider(IReadOnlyDictionary<string, object?> values) : IEquipmentDataProvider
{
    public IReadOnlyDictionary<string, object?> GetVariableValues() => values;
}
