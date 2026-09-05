using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace ScrollBinding;

[PluginName("Scroll Binding Filter")]
public class ScrollBindingFilter : IPositionedPipelineElement<IDeviceReport>
{
    public PipelinePosition Position => PipelinePosition.PreTransform;

    public event Action<IDeviceReport> Emit;

    public event EventHandler<IAbsolutePositionReport> PositionChanged;

    public void Consume(IDeviceReport report)
    {
        if (report is IAbsolutePositionReport positionReport)
            PositionChanged?.Invoke(this, positionReport);

        Emit?.Invoke(report);
    }
}