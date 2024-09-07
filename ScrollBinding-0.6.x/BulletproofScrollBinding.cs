using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using ScrollBinding.Lib.Enums;

namespace ScrollBinding;

[PluginName("Scroll Binding")]
public class BulletproofScrollBinding : ScrollBinding, IStateBinding
{
    private string _property = string.Empty;

    #region Properties

    [Property("Property")]
    public override string Property
    {
        get => _property;
        set
        {
            if (_scrollDirections.ContainsKey(value))
            {
                _property = value;
                _scrollDirection = _scrollDirections[value];
            }
        }
    }

    [TabletReference]
    public TabletReference Tablet { get; set; }

    public static IEnumerable<string> ValidButtons => _scrollDirections.Keys;

    #endregion

    #region Methods

    public void Press(TabletReference tablet, IDeviceReport report) => Scroll();

    public void Release(TabletReference tablet, IDeviceReport report) {}

    public override void Initialize()
    {
        var settings = ScrollBindingSettings.Instances.FirstOrDefault(ins => ins.Tablet == Tablet) ?? new ScrollBindingSettings();

        _scrollAmount = _scrollDirection switch
        {
            ScrollDirection.Forward => settings.ForwardScroll,
            ScrollDirection.Backward => settings.BackwardScroll,
            ScrollDirection.Left => settings.LeftScroll,
            ScrollDirection.Right => settings.RightScroll,
            _ => 20
        };
    }

    #endregion
}