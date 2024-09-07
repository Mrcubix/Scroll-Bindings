using System;
using System.Linq;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using ScrollBinding.Lib.Enums;
using ScrollBinding.Logging;

namespace ScrollBinding;

[PluginName("Scroll Bindings")]
public class StableScrollBinding : ScrollBindingBase, IValidateBinding, IBinding
{
    private string _property = string.Empty;

    public StableScrollBinding() : base(new StableLogger()) 
    { 
        if (ScrollBindingSettings.Instance == null)
            ScrollBindingSettings.SettingsChanged += OnSettingsChanged;
        else
            Initialize();
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

    public Action Release => () => _scrolling = false;

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
    }

    private void OnSettingsChanged(object sender, EventArgs e) => Initialize();

    #endregion
}