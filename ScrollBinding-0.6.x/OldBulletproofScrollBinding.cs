using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Timers;
using ScrollBinding.Lib.Enums;
using ScrollBinding.Lib.Interfaces;
using ScrollBinding.Logging;

namespace ScrollBinding;

[PluginName("Lagacy Scroll Bindings")]
public class BulletproofScrollBinding : ScrollBindingBase, IStateBinding
{
    private string _property = string.Empty;
    protected ITimer _timer;

    public BulletproofScrollBinding() : base(new BulletproofLogger()) 
    {
        if (ScrollBindingSettings.Instance == null)
            ScrollBindingSettings.SettingsChanged += OnSettingsChanged;
        else
            Initialize();
    }

    public BulletproofScrollBinding(ILogger logger) : base(logger) {}

    #region Properties

    [Property("Property"),
     PropertyValidated(nameof(ValidOptions)),
     ToolTip("Legacy Scroll Binding:\n\n" +
             "The direction of the scroll. \n\n" +
             "Note: Use 'Scroll Binding' instead as this legacy Plugin Type is depecrated & is only available for backwards compatibility.")]
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

    [Resolved]
    public ITimer Timer
    {
        get => _timer;
        set
        {
            _timer = value;

            if (_timer != null)
            {
                _timer.Interval = _scrollDelay;
                _timer.Elapsed += ScrollOnce;
            }
        }
    }

    public static IEnumerable<string> ValidOptions => _scrollDirections.Keys;

    #endregion

    #region Methods

    public void Press(TabletReference tablet, IDeviceReport report) => Scroll();

    public void Release(TabletReference tablet, IDeviceReport report) 
    {
        _scrolling = false;
        _timer.Stop();
    }

    public override void Initialize()
    {
        var settings = ScrollBindingSettings.Instance ?? new ScrollBindingSettings();

        _scrollAmount = _scrollDirection switch
        {
            ScrollDirection.Forward => settings.ForwardScroll,
            ScrollDirection.Backward => settings.BackwardScroll,
            ScrollDirection.Left => settings.LeftScroll,
            ScrollDirection.Right => settings.RightScroll,
            _ => 20
        };

        _scrollDelay = settings.ScrollDelay;
        _timer.Interval = _scrollDelay;
    }

    protected override void ScrollContinuously() => _timer.Start();

    private void OnSettingsChanged(object sender, EventArgs e) => Initialize();

    #endregion
}