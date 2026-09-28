using SecsGem.Core;

namespace SecsGem.Runtime.Tests.Fakes;

internal sealed class FakeEquipmentDataProvider(IReadOnlyDictionary<string, object?> values) : IEquipmentDataProvider
{
    public IReadOnlyDictionary<string, object?> GetVariableValues() => values;
}
