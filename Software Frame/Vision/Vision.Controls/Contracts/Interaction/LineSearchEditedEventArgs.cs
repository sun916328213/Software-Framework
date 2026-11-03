using Vision.Core.Geometry;

namespace Vision.Controls.View;

public sealed class LineSearchEditedEventArgs(ILineSearchRegion parameters, bool finished) : EventArgs
{
    public ILineSearchRegion Parameters { get; } = parameters;
    public bool Finished { get; } = finished;
}
