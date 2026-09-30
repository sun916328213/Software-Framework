using Extension.Patterns.Creation.Factory.Advanced;
using Secs.Order;

namespace SemiTool.Tool;

/// <summary>Compatibility adapter for the old factory API.</summary>
public class SecsOrderAdapter : IAdvancedProduct
{
    private readonly SecsOrderBase _secsOrder;

    public string Id { get; }
    public string Name { get; }
    public string Type => "Secs命令";

    public SecsOrderAdapter(SecsOrderBase secsOrder, string productId)
    {
        _secsOrder = secsOrder ?? throw new ArgumentNullException(nameof(secsOrder));
        Id = productId ?? throw new ArgumentNullException(nameof(productId));
        Name = secsOrder.GetType().Name;
    }

    public void Execute() => Console.WriteLine($"Secs命令 {Name} 已创建");

    public SecsOrderBase GetSecsOrder() => _secsOrder;

    public void Dispose() { }
}
