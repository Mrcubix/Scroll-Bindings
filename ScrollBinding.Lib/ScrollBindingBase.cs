using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScrollBinding.Lib.Devices;
using ScrollBinding.Lib.Enums;
using ScrollBinding.Lib.Interfaces;
using ScrollBinding.Lib.Interop;

namespace ScrollBinding;

public abstract class ScrollBindingBase : IDisposable
{
    protected ScrollDirection _scrollDirection = ScrollDirection.None;
    protected int _scrollAmount = 120;
    protected int _scrollDelay = 15;
    protected bool _scrolling = false;
    protected bool _disposed = false;

    public ScrollBindingBase(ILogger logger)
    {
        Logger = logger;

        Wheel ??= CurrentPlatformWheel;
    }

    #region Properties

    public abstract string Property { get; set; }

    public static IMouseWheel Wheel { get; protected set; }

    #endregion

    #region Methods

    public abstract void Initialize();

    public void Scroll()
    {
        if (Wheel == null || _scrolling) return;

        _scrolling = true;
        ScrollContinuously();
    }

    protected abstract void ScrollContinuously();

    protected void ScrollOnce()
    {
        switch (_scrollDirection)
        {
            case ScrollDirection.Forward:
                Wheel.ScrollVertically(_scrollAmount);
                break;
            case ScrollDirection.Backward:
                Wheel.ScrollVertically(_scrollAmount * -1);
                break;
            case ScrollDirection.Left:
                Wheel.ScrollHorizontally(_scrollAmount * -1);
                break;
            case ScrollDirection.Right:
                Wheel.ScrollHorizontally(_scrollAmount);
                break;
            default:
                break;
        }

        Wheel.Flush();
    }

    public void Dispose()
    {
        if (_disposed == false && Wheel is IDisposable disposableWheel)
            disposableWheel.Dispose();
    }

    #endregion

    #region Static Fields

    protected static ILogger Logger { get; set; }

    public static IMouseWheel CurrentPlatformWheel => SystemInterop.CurrentPlatform switch
    {
        PluginPlatform.Windows => new WindowsMouseWheel(),
        PluginPlatform.Linux => new LinuxMouseWheel(Logger),
        _ => null
    };

    protected static Dictionary<string, ScrollDirection> _scrollDirections = new()
    {
        { "Scroll Forward", ScrollDirection.Forward },
        { "Scroll Backward", ScrollDirection.Backward },
        { "Scroll Left", ScrollDirection.Left },
        { "Scroll Right", ScrollDirection.Right }
    };

    #endregion
}