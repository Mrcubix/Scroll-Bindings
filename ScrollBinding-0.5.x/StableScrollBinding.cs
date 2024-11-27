using System;
using System.Linq;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Timers;
using ScrollBinding.Lib.Enums;
using ScrollBinding.Logging;

namespace ScrollBinding;

[PluginName("Scroll Bindings")]
public class StableScrollBinding : ScrollBindingBase, IValidateBinding, IBinding
{
    private ITimer _timer = SystemInterop.Timer;
    private string _property = string.Empty;

    public StableScrollBinding() : base(new StableLogger()) 
    { 
        if (ScrollBindingSettings.Instance == null)
            ScrollBindingSettings.SettingsChanged += OnSettingsChanged;
        else
            Initialize();

        _timer.Elapsed += ScrollOnce;
    }

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

    public string[] ValidProperties => _scrollDirections.Keys.ToArray();

    #endregion

    #region Actions

    public Action Press => Scroll;

    public Action Release => () => 
    {
        _scrolling = false;
        _timer.Stop();
    };

    #endregion

    #region Methods

    public override void Initialize()
    {
        var settings = ScrollBindingSettings.Instance ?? new ScrollBindingSettings();

        _scrollAmount = _scrollDirection switch
        {
            ScrollDirection.Forward => settings.ForwardScroll,
            ScrollDirection.Backward => settings.BackwardScroll,
            ScrollDirection.Left => settings.LeftScroll,
            ScrollDirection.Right => settings.RightScroll,
            _ => 120
        };

        _scrollDelay = settings.ScrollDelay;
        _timer.Interval = _scrollDelay;
    }

    protected override void ScrollContinuously() => _timer.Start();

    private void OnSettingsChanged(object sender, EventArgs e) => Initialize();

    #endregion
}