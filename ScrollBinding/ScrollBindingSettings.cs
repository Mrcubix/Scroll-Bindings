using System;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;

namespace ScrollBinding;

public class ScrollBindingSettings : ITool
{
    private int _forwardScroll = 20;
    private int _backwardScroll = 20;
    private int _leftScroll = 20;
    private int _rightScroll = 20;

    public bool Initialize()
    {
        Instance = this;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        
        return true;
    }

    public void Dispose() 
    {
        Instance = null;
    }

    [Property("Forward Scroll"),
     DefaultPropertyValue(20),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll forward. \n\n" +
             "Default: 20")]
    public int ForwardScroll
    {
        get => _forwardScroll;
        set => _forwardScroll = Math.Max(0, Math.Min(120, value));
    }

    [Property("Backward Scroll"),
     DefaultPropertyValue(20),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll backward. \n\n" +
             "Default: 20")]
    public int BackwardScroll
    {
        get => _backwardScroll;
        set => _backwardScroll = Math.Max(0, Math.Min(120, value));
    }

    [Property("Left Scroll"),
     DefaultPropertyValue(20),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll left. \n\n" +
             "Default: 20")]
    public int LeftScroll
    {
        get => _leftScroll;
        set => _leftScroll = Math.Max(0, Math.Min(120, value));
    }

    [Property("Right Scroll"),
     DefaultPropertyValue(20),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll right. \n\n" +
             "Default: 20")]
    public int RightScroll
    {
        get => _rightScroll;
        set => _rightScroll = Math.Max(0, Math.Min(120, value));
    }

    public static ScrollBindingSettings Instance { get; private set; } = new();
    public static event EventHandler SettingsChanged;
}