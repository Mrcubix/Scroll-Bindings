using System.Numerics;
using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using ScrollBinding.Lib.Interfaces;
using ITimer = OpenTabletDriver.Plugin.Timers.ITimer;

#nullable enable

namespace ScrollBinding;

[PluginName("Drag Scroll")]
public sealed class DragScrollBinding : IStateBinding, IDisposable
{
    #region Fields

    #region Constants

    private const double INTERVAL_MILLISECONDS = 1;
    private const double INTERVAL_SECONDS = INTERVAL_MILLISECONDS / 1000;

    private readonly IMouseWheel Wheel = ScrollBindingBase.CurrentPlatformWheel;

    #endregion

    private ScrollBindingFilter? _filter;
    private IOutputMode? _outputMode;
    private Vector<double> _currentVelocity = new([0d, 0d, 0d, 0d]);
    private double[] _currentVelocityArray = [0, 0, 0, 0];
    private TabletReference? _tablet;
    private Vector2? _lastPosition;
    private Vector2? _lastPositionCopy;
    private double _deltaTime; // in milliseconds
    private uint _PenMaxPressure= 1024;
    private bool _pressing;
    private ITimer? _timer;
    private bool _postinitialized;

    #endregion

    #region Properties

    [Resolved]
    public ITimer? Timer
    {
        get => _timer;
        set
        {
            _timer = value;

            if (_timer != null)
            {
                _timer.Interval = (float)INTERVAL_MILLISECONDS;
                _timer.Elapsed += IntervalElapsed;
                _timer.Start();
            }
        }
    }

    [Resolved]
    public IDriver? Driver { get; set; }

    [TabletReference]
    public TabletReference? Tablet
    {
        get => _tablet;
        set
        {
            _tablet = value;
            PreElementInitialize();
        }
    }

    [Property("Sensitivity"),
     DefaultPropertyValue(1d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The sensitivity of the drag scroll binding. Higher values will result in faster scrolling.")]
    public double Sensitivity { get; set; } = 1d;

    [BooleanProperty("Enable Kinetic Scrolling", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Scrolling speed will slowly drop to 0 after releasing pressure.")]
    public bool EnableKineticScrolling { get; set; } = true;

    [Property("Deceleration"),
     DefaultPropertyValue(0.1d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The amount of decceleration applied to the scroll velocity when the user releases the binding.")]
    public double Deceleration { get; set; } = 0.1d;

    [BooleanProperty("Invert Scroll", ""),
     DefaultPropertyValue(false),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Inverts the scroll direction of the drag scroll binding.")]
    public bool InvertScroll { get; set; }

    [BooleanProperty("Scroll when dragging", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "This setting only takes effect when a pen is used.\n" +
             "Only scroll when the applied pressure is greater than the user defined threshold.\n" +
             "When enabled, this effectively prevents scrolling when hovering over the tablet.")]
    public bool ScrollOnDrag { get; set; } = true;

    [SliderProperty("Pressure Threshold", 0f, 100f, 1f),
     DefaultPropertyValue(1f),
     Unit("%"),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Only scroll when the pressure is greater than the user defined threshold.\n" +
             "Only takes effect when a pen is used & Require Pressure is enabled.")]
    public float PressureThreshold { get; set; }

    [BooleanProperty("Freeze Cursor", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The cursor will remain at the same position while scrolling.")]
    public bool FrozenCursor { get; set; } = true;

    #region Obsolete Properties

    [Obsolete("TipActivationThreshold has been renamed to PressureThreshold")]
    public float TipActivationThreshold
    {
        get => PressureThreshold;
        set => PressureThreshold = value;
    }

    [Obsolete("StaticPositionWhileScrolling has been renamed to FrozenCursor")]
    public bool StaticPositionWhileScrolling
    {
        get => FrozenCursor;
        set => FrozenCursor = value;
    }

    #endregion

    #endregion

    #region Methods

    #region Initialization

    private void PreElementInitialize()
    {
        if (_tablet != null && _tablet.Properties.Specifications.Pen is { } pen)
            _PenMaxPressure = pen.MaxPressure;

        if (Driver is Driver driver)
        {
            var tree = driver.InputDevices.FirstOrDefault(dev => dev.OutputMode != null && dev.Properties.Name == _tablet?.Properties.Name);

            _outputMode = tree?.OutputMode;

            if (tree == null)
                Log.Write("Drag Scroll Binding", $"Failed to find the Device Tree for '{_tablet?.Properties.Name}'.", LogLevel.Error);
            else if (tree.OutputMode == null)
                Log.Write("Drag Scroll Binding", $"Failed to find the Output Mode for '{_tablet?.Properties.Name}'.", LogLevel.Error);
        }
    }

    private void PostElementInitialize()
    {
        if (_outputMode == null) return;

        _filter = _outputMode.Elements.OfType<ScrollBindingFilter>().FirstOrDefault();
        _filter?.PositionChanged += Consume;

        if (_filter == null)
            Log.Write("Drag Scroll Binding", $"Failed to find Scroll Binding Filter in the pipeline for '{_tablet?.Properties.Name}'.\n" +
                                              "Enabling 'Scroll Binding Filter' in the Filter tab is required for Drag Scrolling to work.", 
                                              LogLevel.Error, false, true);
        else
            _postinitialized = true;
    }

    #endregion

    #region Position Processing

    public void Consume(object? sender, IDeviceReport report)
    {
        if (_pressing && report is IAbsolutePositionReport positionReport)
        {
            HandleReport(positionReport);

            _lastPositionCopy ??= new Vector2(positionReport.Position.X, positionReport.Position.Y);

            if (FrozenCursor)
            {
                positionReport.Position = (Vector2)_lastPositionCopy;
                if (positionReport is ITabletReport tabletReport)
                    tabletReport.Pressure = 0;
            }
        }
    }

    public void HandleReport(IAbsolutePositionReport report)
    {
        switch (report)
        {
            case ITabletReport tabletReport when !ScrollOnDrag || ((float)tabletReport.Pressure / (float)_PenMaxPressure * 100f) > PressureThreshold:
                Scroll(tabletReport);
                break;
            case IMouseReport mouseReport:
                Scroll(mouseReport);
                break;
            default:
                break;
        }
    }

    #endregion

    #region Binding

    public void Press(TabletReference tablet, IDeviceReport report)
    {
        if (!_postinitialized)
            PostElementInitialize();

        _currentVelocity = new([0d, 0d, 0d, 0d]);
        _pressing = true;
        _lastPosition = null;
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        _pressing = false;
        _lastPositionCopy = null;
    }

    #endregion

    #region Scrolling

    private void Scroll(IAbsolutePositionReport positionReport)
    {
        if (!_pressing || _deltaTime == 0) return;

        _lastPosition ??= positionReport.Position;

        var delta = positionReport.Position - _lastPosition;
        var direction = InvertScroll ? -1 : 1;

        _currentVelocityArray[0] = (((delta?.X ?? 0) * Sensitivity) / _deltaTime) * direction;
        _currentVelocityArray[1] = (((delta?.Y ?? 0) * Sensitivity) / _deltaTime) * direction;

        _currentVelocity = new Vector<double>(_currentVelocityArray);

        _lastPosition = positionReport.Position;
        _deltaTime = 0;

        //Wheel.ScrollHorizontally((int)_currentVelocity[0]);
        //Wheel.Flush();
        Wheel.ScrollVertically((int)_currentVelocity[1]);
        Wheel.Flush();
    }

    private void Decelerate()
    {
        var deccelerationX = _currentVelocityArray[0] > 0 ? -Deceleration : Deceleration;
        var deccelerationY = _currentVelocityArray[1] > 0 ? -Deceleration : Deceleration;

        var oldVelocity = (double[])_currentVelocityArray.Clone();

        _currentVelocityArray[0] += deccelerationX * INTERVAL_MILLISECONDS;
        _currentVelocityArray[1] += deccelerationY * INTERVAL_MILLISECONDS;

        if (oldVelocity[1] > 1 && _currentVelocityArray[1] < -1)
            _currentVelocityArray[1] = 0;
        else if (oldVelocity[1] < -1 && _currentVelocityArray[1] > 1)
            _currentVelocityArray[1] = 0;

        _currentVelocity = new Vector<double>(_currentVelocityArray);

        //Wheel.ScrollHorizontally((int)_currentVelocity[0]);
        //Wheel.Flush();
        Wheel.ScrollVertically((int)_currentVelocity[1]);
        Wheel.Flush();
    }

    #endregion

    #region Event Handlers

    public void IntervalElapsed()
    {
        if (_timer == null) return;

        _deltaTime += (ulong)_timer.Interval;

        if (EnableKineticScrolling && (_currentVelocity[1] < -1 || _currentVelocity[1] > 1))
            //(_currentVelocity[0] < -1 || _currentVelocity[0] > 1))
            Decelerate();
    }

    #endregion

    #region Interfaces

    public void Dispose()
    {
        if (_timer != null)
        {
            _timer.Elapsed -= IntervalElapsed;
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
    }

    #endregion

    #endregion
}