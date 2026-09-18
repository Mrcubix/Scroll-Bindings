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

[PluginName("Pen Scroll")]
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
    private TabletReference? _tablet;
    private ITimer? _timer;

    private double[] _currentVelocity = [0, 0];
    private Vector2? _initiatingPosition;
    private Vector2? _lastPosition;
    
    private uint _PenMaxPressure= 1024;
    private double _deltaTime; // in milliseconds
    private bool _pressing;
    
    private bool _fullyinitialized;

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

    [Property("X Sensitivity"),
     DefaultPropertyValue(0.5d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The horizontal sensitivity of the drag scroll binding. Higher values will result in faster scrolling." +
             "Horizontal scrolling cannot be properly supported on Windows due to SendInput limitations.")]
    public double XSensitivity { get; set; } = 0.1d;

    [Property("Y Sensitivity"),
     DefaultPropertyValue(1d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The vertical sensitivity of the drag scroll binding. Higher values will result in faster scrolling.")]
    public double YSensitivity { get; set; } = 1d;

    [BooleanProperty("Invert Scroll", ""),
     DefaultPropertyValue(false),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Inverts the scroll direction of the drag scroll binding.")]
    public bool InvertScroll { get; set; }

    [BooleanProperty("Freeze Cursor", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The cursor will remain at the same position while scrolling.")]
    public bool FrozenCursor { get; set; } = true;

    /*[BooleanProperty("Cancel Pressure", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The pressure will be canceled while scrolling.")]*/
    public bool CancelPressure { get; set; } = true;

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

    [SliderProperty("Drag Scrolling Pressure Threshold", 0f, 100f, 1f),
     DefaultPropertyValue(1f),
     Unit("%"),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The amount of pressure required for to start scrolling.\n" +
             "A pressure threshold under 1% implies you will be scroll while hovering.")]
    public float DragScrollingPressureThreshold { get; set; }

    #region Obsolete Properties

    [Obsolete("Sensitivity has been divided into XSensitivity & YSensitivity")]
    public double Sensitivity
    {
        get => YSensitivity;
        set => YSensitivity = XSensitivity = value;
    }

    [Obsolete("TipActivationThreshold has been renamed to PressureThreshold")]
    public float TipActivationThreshold
    {
        get => DragScrollingPressureThreshold;
        set => DragScrollingPressureThreshold = value;
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
            _fullyinitialized = true;
    }

    #endregion

    #region Binding

    public void Press(TabletReference tablet, IDeviceReport report)
    {
        if (!_fullyinitialized)
            PostElementInitialize();

        // Cancel any existing velocity
        _currentVelocity = [0d, 0d];

        _pressing = true;
        _lastPosition = null;
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        _pressing = false;
        _initiatingPosition = null;
    }

    #endregion

    #region Position Processing

    public void Consume(object? sender, IDeviceReport report)
    {
        if (_pressing && report is IAbsolutePositionReport positionReport)
        {
            HandleReport(positionReport);

            if (FrozenCursor)
            {
                // Store the first position so we can freeze the cursor
                // The copy expires on Release
                _initiatingPosition ??= new Vector2(positionReport.Position.X, positionReport.Position.Y);

                positionReport.Position = (Vector2)_initiatingPosition;
            }

            // Cancel pressure during scroll so users don't click while scrolling
            if (CancelPressure)
                if (positionReport is ITabletReport tabletReport)
                    tabletReport.Pressure = 0;
        }
    }

    public void HandleReport(IAbsolutePositionReport report)
    {
        switch (report)
        {
            case ITabletReport tabletReport when !(DragScrollingPressureThreshold > 0) || ((float)tabletReport.Pressure / (float)_PenMaxPressure * 100f) > DragScrollingPressureThreshold:
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

    #region Scrolling

    // This method is run every time a tablet report is received
    private void Scroll(IAbsolutePositionReport positionReport)
    {
        if (!_pressing || _deltaTime == 0) return;

        _lastPosition ??= positionReport.Position;

        // skip if this is the initiating position
        if (_lastPosition == positionReport.Position)
            return;

        var delta = positionReport.Position - _lastPosition;
        var direction = InvertScroll ? -1 : 1;

        _currentVelocity[0] = (((delta?.X ?? 0) * XSensitivity) / _deltaTime) * direction;
        _currentVelocity[1] = (((delta?.Y ?? 0) * YSensitivity) / _deltaTime) * direction;

        _lastPosition = positionReport.Position;
        _deltaTime = 0;

        //Log.Debug("Drag Scroll Binding", $"Velocity: X = {_currentVelocity[0]}, Y = {_currentVelocity[1]}");

        if (_currentVelocity[0] < -1 || _currentVelocity[0] > 1)
            Wheel.ScrollHorizontally((int)_currentVelocity[0]);
        if (_currentVelocity[1] < -1 || _currentVelocity[1] > 1)
            Wheel.ScrollVertically((int)_currentVelocity[1]);

        Wheel.Flush();
    }

    private void DecelerateX()
    {
        var deccelerationX = _currentVelocity[0] > 0 ? -Deceleration : Deceleration;
        var oldVelocity = (double[])_currentVelocity.Clone();

        _currentVelocity[0] += deccelerationX * INTERVAL_MILLISECONDS;

        // Necessary to prevent scrolling in the opposite direction after deceleration
        if (oldVelocity[0] > 1 && _currentVelocity[0] < -1)
            _currentVelocity[0] = 0;
        else if (oldVelocity[0] < -1 && _currentVelocity[0] > 1)
            _currentVelocity[0] = 0;

        if (_currentVelocity[0] != 0)
            Wheel.ScrollHorizontally((int)_currentVelocity[0]);
    }

    private void DecelerateY()
    {
        var deccelerationY = _currentVelocity[1] > 0 ? -Deceleration : Deceleration;
        var oldVelocity = (double[])_currentVelocity.Clone();

        _currentVelocity[1] += deccelerationY * INTERVAL_MILLISECONDS;

        // Necessary to prevent scrolling in the opposite direction after deceleration
        if (oldVelocity[1] > 1 && _currentVelocity[1] < -1)
            _currentVelocity[1] = 0;
        else if (oldVelocity[1] < -1 && _currentVelocity[1] > 1)
            _currentVelocity[1] = 0;

        if (_currentVelocity[1] != 0)
            Wheel.ScrollVertically((int)_currentVelocity[1]);
    }

    #endregion

    #region Event Handlers

    public void IntervalElapsed()
    {
        if (_timer == null) return;

        _deltaTime += (ulong)_timer.Interval;

        if (EnableKineticScrolling && (_currentVelocity[0] < -1 || _currentVelocity[0] > 1))
            DecelerateX();

        if (EnableKineticScrolling && (_currentVelocity[1] < -1 || _currentVelocity[1] > 1))
            DecelerateY();

        Wheel.Flush();
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