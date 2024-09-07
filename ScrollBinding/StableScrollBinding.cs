using System;
using System.Linq;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using ScrollBinding.Lib.Enums;

namespace ScrollBinding;

[PluginName("Scroll Binding")]
public class StableScrollBinding : ScrollBinding, IValidateBinding, IBinding
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

    public string[] ValidProperties => _scrollDirections.Keys.ToArray();

    #endregion

    #region Actions

    public Action Press => Scroll;

    public Action Release => null;

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
            _ => 20
        };
    }

    #endregion
}