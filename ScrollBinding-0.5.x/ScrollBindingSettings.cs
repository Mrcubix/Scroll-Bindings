using System;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;

namespace ScrollBinding;

[PluginName("Scroll Bindings Settings")]
public class ScrollBindingSettings : ITool
{
    private int _forwardScroll = 120;
    private int _backwardScroll = 120;
    private int _leftScroll = 120;
    private int _rightScroll = 120;
    private int _scrollDelay = 15;

    #region Methods

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

    #endregion

    #region Properties

    [Property("Scroll Delay"),
     DefaultPropertyValue(15),
     Unit("ms"),
     ToolTip("Scroll Binding:\n\n" +
             "The amount of delay between scrolls. \n" +
             "A smaller value will result in a smoother but also faster scroll. \n\n" +
             "Minimum: 1 ms \n" +
             "Maximum: 1000 ms \n" +
             "Default: 15 ms")]
    public int ScrollDelay
    {
        get => _scrollDelay;
        set
        {
            _scrollDelay = Math.Clamp(value, 1, 1000);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Property("Forward Scroll"),
     DefaultPropertyValue(120),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll forward. \n" +
             "Note: A tick equals to 120. \n" +
             "Note: This will only affect target applications that supports high resolution scrolling. \n\n" +
             "Default: 120")]
    public int ForwardScroll
    {
        get => _forwardScroll;
        set
        {
            _forwardScroll = Math.Clamp(value, 0, 2400);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Property("Backward Scroll"),
     DefaultPropertyValue(120),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll backward. \n" +
             "Note: A tick equals to 120. \n" +
             "Note: This will only affect target applications that supports high resolution scrolling. \n\n" +
             "Default: 120")]
    public int BackwardScroll
    {
        get => _backwardScroll;
        set
        {
            _backwardScroll = Math.Clamp(value, 0, 2400);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Property("Left Scroll"),
     DefaultPropertyValue(120),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll left. \n" +
             "Note: A tick equals to 120. \n" +
             "Note: This will only affect target applications that supports high resolution scrolling. \n\n" +
             "Default: 120")]
    public int LeftScroll
    {
        get => _leftScroll;
        set
        {
            _leftScroll = Math.Clamp(value, 0, 2400);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Property("Right Scroll"),
     DefaultPropertyValue(120),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll right. \n" +
             "Note: A tick equals to 120. \n" +
             "Note: This will only affect target applications that supports high resolution scrolling. \n\n" +
             "Default: 120")]
    public int RightScroll
    {
        get => _rightScroll;
        set
        {
            _rightScroll = Math.Clamp(value, 0, 2400);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion

    public static ScrollBindingSettings Instance { get; private set; }
    public static event EventHandler SettingsChanged;
}