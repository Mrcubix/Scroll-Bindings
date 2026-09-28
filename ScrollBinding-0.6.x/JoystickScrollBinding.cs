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

[PluginName(PLUGIN_NAME)]
public sealed class JoystickScrollBinding : IStateBinding, IDisposable
{
    #region Fields

    #region Constants

    private const string PLUGIN_NAME = "Joystick Scroll";

    private const double INTERVAL_MILLISECONDS = 1;
    private const double INTERVAL_SECONDS = INTERVAL_MILLISECONDS / 1000;

    private const double BASE_SPEED = 12d;
    private const double INTERNAL_COEFFICIENT = 0.01;
    
    private readonly IMouseWheel Wheel = ScrollBindingBase.CurrentPlatformWheel;

    #endregion

    private ScrollBindingFilter? _filter;
    private IOutputMode? _outputMode;
    private TabletReference? _tablet;
    private ITimer? _timer;

    private double[] _currentVelocity = [0, 0];
    private Vector2? _initiatingPosition;
    
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
     ToolTip("Joystick Scroll Binding:\n\n" +
             "The horizontal sensitivity of the drag scroll binding. Higher values will result in faster scrolling." +
             "Horizontal scrolling cannot be properly supported on Windows due to SendInput limitations.")]
    public double XSensitivity { get; set; } = 0.1d;

    [Property("Y Sensitivity"),
     DefaultPropertyValue(1d),
     ToolTip("Joystick Scroll Binding:\n\n" +
             "The vertical sensitivity of the drag scroll binding. Higher values will result in faster scrolling.")]
    public double YSensitivity { get; set; } = 1d;

    [Property("Deadzone"),
     DefaultPropertyValue(30d),
     ToolTip("Joystick Scroll Binding:\n\n" +
             "Scrolling will not happen while the joystick is within the deadzone.\n" +
             "Unit is in tablet units, this is not pourcentage-based.")]
    public double Deadzone { get; set; } = 30d;

    [BooleanProperty("Invert Scroll", ""),
     DefaultPropertyValue(false),
     ToolTip("Joystick Scroll Binding:\n\n" +
             "Inverts the scroll direction of the drag scroll binding.\n")]
    public bool InvertScroll { get; set; }

    [BooleanProperty("Freeze Cursor", ""),
     DefaultPropertyValue(true),
     ToolTip("Joystick Scroll Binding:\n\n" +
             "The cursor will remain at the same position while scrolling.")]
    public bool FrozenCursor { get; set; } = true;

    /*[BooleanProperty("Cancel Pressure", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The pressure will be canceled while scrolling.")]*/
    public bool CancelPressure { get; set; } = true;

    /*[BooleanProperty("Scroll when dragging", ""),
     DefaultPropertyValue(true),
     ToolTip("Joystick Scroll Binding:\n\n" +
             "This setting only takes effect when a pen is used.\n" +
             "Only scroll when the applied pressure is greater than the user defined threshold.\n" +
             "When enabled, this effectively prevents scrolling when hovering over the tablet.")]*/
    public bool ScrollOnDrag { get; set; } = true;

    [SliderProperty("Drag Scrolling Pressure Threshold", 0f, 100f, 1f),
     DefaultPropertyValue(1f),
     Unit("%"),
     ToolTip("Joystick Scroll Binding:\n\n" +
             "The amount of pressure required for to start scrolling.\n" +
             "A pressure threshold under 1% implies you will be scroll while hovering.")]
    public float DragScrollingPressureThreshold { get; set; }

    /*[BooleanProperty("Reset origin when hovering", ""),
     DefaultPropertyValue(true),
     ToolTip("Joystick Scroll Binding:\n\n" +
             "When enabled, the origin will be reset when hovering on the tablet.\n" +
             "This allows resetting the origin without releasing the Binding.\n" +
             "Only takes effect when Threshold is above 0%.")]*/
    public bool ResetOrigin { get; set; } = false;

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
                Log.Write(PLUGIN_NAME, $"Failed to find the Device Tree for '{_tablet?.Properties.Name}'.", LogLevel.Error);
            else if (tree.OutputMode == null)
                Log.Write(PLUGIN_NAME, $"Failed to find the Output Mode for '{_tablet?.Properties.Name}'.", LogLevel.Error);
        }
    }

    private void PostElementInitialize()
    {
        if (_outputMode == null) return;

        _filter = _outputMode.Elements.OfType<ScrollBindingFilter>().FirstOrDefault();
        _filter?.PositionChanged += Consume;

        if (_filter == null)
            Log.Write(PLUGIN_NAME, $"Failed to find Scroll Binding Filter in the pipeline for '{_tablet?.Properties.Name}'.\n" +
                                    "Enabling 'Scroll Binding Filter' in the Filter tab is required for Drag Scrolling to work.", 
                                    LogLevel.Error, false, true);
        else
            _fullyinitialized = true;

        ScrollOnDrag = DragScrollingPressureThreshold > 0;
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

            if (FrozenCursor & _initiatingPosition != null)
                positionReport.Position = _initiatingPosition!.Value;

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
            case ITabletReport tabletReport:
                var aboveThreshold = ((float)tabletReport.Pressure / (float)_PenMaxPressure * 100f) > DragScrollingPressureThreshold;

                if (!ScrollOnDrag || aboveThreshold)
                    Scroll(tabletReport);

                // Reset origin when hovering if threshold is above 0%
                if (ScrollOnDrag && !aboveThreshold)
                    _initiatingPosition = null;
                
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

    public void Scroll(IAbsolutePositionReport positionReport)
    {
        if (_deltaTime == 0) return;

        // Store the first position so we can freeze the cursor
        // The copy expires on Release
        _initiatingPosition ??= new Vector2(positionReport.Position.X, positionReport.Position.Y);

        // skip if the position hasn't changed
        if (_initiatingPosition == positionReport.Position)
            return;

        // Shamelessly inspired from Mozilla Firefox's repo
        var XSpeed = Math.Max(1, BASE_SPEED * 100f / (XSensitivity * 100f));
        var YSpeed = Math.Max(1, BASE_SPEED * 100f / (YSensitivity * 100f));

        var delta = positionReport.Position - _initiatingPosition;
        var direction = InvertScroll ? -1 : 1;

        var minDeltaTime = Math.Min(100f, _deltaTime) / 20;

        _currentVelocity[0] = (delta?.X ?? 0 / XSpeed) / minDeltaTime * direction * INTERNAL_COEFFICIENT;
        _currentVelocity[1] = (delta?.Y ?? 0 / YSpeed) / minDeltaTime * -direction * INTERNAL_COEFFICIENT;

        _deltaTime = 0;

        //Log.Debug("Joystick Scroll Binding", $"Velocity: X = {_currentVelocity[0]}, Y = {_currentVelocity[1]}");

        // Windows is annoying, as it will only scroll in whichever direction has the hiest scroll amount.
        if (_currentVelocity[0] < -Deadzone)
            Wheel.ScrollHorizontally((int)(_currentVelocity[0] + Deadzone));
        else if (_currentVelocity[0] > Deadzone)
            Wheel.ScrollHorizontally((int)(_currentVelocity[0] - Deadzone));

        if (_currentVelocity[1] < -Deadzone)
            Wheel.ScrollVertically((int)(_currentVelocity[1] + Deadzone));
        else if (_currentVelocity[1] > Deadzone)
            Wheel.ScrollVertically((int)(_currentVelocity[1] - Deadzone));

        Wheel.Flush();
    }

    #endregion

    #region Event Handlers

    public void IntervalElapsed()
    {
        if (_timer == null) return;

        _deltaTime += (ulong)_timer.Interval;
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