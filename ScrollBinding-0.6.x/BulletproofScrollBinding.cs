using System;
using System.Collections.Generic;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using ScrollBinding.Lib.Enums;
using ScrollBinding.Logging;

namespace ScrollBinding;

[PluginName("Scroll Bindings")]
public class BulletproofScrollBinding : ScrollBindingBase, IStateBinding
{
    private string _property = string.Empty;

    public BulletproofScrollBinding() : base(new BulletproofLogger()) 
    {
        if (ScrollBindingSettings.Instance == null)
            ScrollBindingSettings.SettingsChanged += OnSettingsChanged;
        else
            Initialize();
    }

    #region Properties

    [Property("Property"), PropertyValidated(nameof(ValidOptions))]
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

    public static IEnumerable<string> ValidOptions => _scrollDirections.Keys;

    #endregion

    #region Methods

    public void Press(TabletReference tablet, IDeviceReport report) => Scroll();

    public void Release(TabletReference tablet, IDeviceReport report) 
    {
        _scrolling = false;
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
    }

    private void OnSettingsChanged(object sender, EventArgs e) => Initialize();

    #endregion
}