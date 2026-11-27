using System.Numerics;
using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Timing;
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
    private const double THRESHOLD = 1;

    private readonly IMouseWheel Wheel = ScrollBindingBase.CurrentPlatformWheel;
    private readonly HPETDeltaStopwatch _reportTimer = new(); // timer for tool reports.

    #endregion

    private ScrollBindingFilter? _filter;
    private IOutputMode? _outputMode;
    private TabletReference? _tablet;
    private ITimer? _timer; // for decceleration

    private Matrix3x2 _rotationMatrix;

    private double[] _currentVelocity = [0, 0];
    private bool _hasVelocityX;
    private bool _hasVelocityY;

    private Vector2? _anchorPosition;
    private Vector2? _lastPosition;
    private bool _aboveThreshold;
    private bool _pressing;

    private uint _PenMaxPressure = 1024;
    private bool _scrollOnDrag = false;
    private Vector2 _direction = Vector2.One;

    private bool _fullyinitialized;

    #endregion

    #region Properties

    /// <summary>
    ///   This timer is used for decceleration.
    /// </summary>
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

    #region Parameters

    [Property("X Sensitivity"),
     DefaultPropertyValue(0.1d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The horizontal sensitivity of the drag scroll binding. Higher values will result in faster scrolling.\n" +
             "A negative value will result in inverted scrolling.\n" +
             "Horizontal scrolling cannot be properly supported on Windows due to SendInput limitations.")]
    public double XSensitivity { get; set; } = 0.1d;

    [Property("Y Sensitivity"),
     DefaultPropertyValue(1d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The vertical sensitivity of the drag scroll binding. Higher values will result in faster scrolling.\n" +
             "A negative value will result in inverted scrolling.")]
    public double YSensitivity { get; set; } = 1d;

    [BooleanProperty("Invert X Scroll", ""),
     DefaultPropertyValue(false),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Inverts the horizontal scroll direction of the drag scroll binding.")]
    public bool InvertScrollX { get; set; }

    [BooleanProperty("Invert Y Scroll", ""),
     DefaultPropertyValue(false),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Inverts the vertical scroll direction of the drag scroll binding.")]
    public bool InvertScrollY { get; set; }

    [BooleanProperty("Freeze Cursor", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The cursor will remain at the same position while scrolling.")]
    public bool FrozenCursor { get; set; } = true;

    [BooleanProperty("Cancel Pressure", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The pressure will be canceled while scrolling.")]
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
             "A pressure threshold under 1% implies scrolling will be active even when hovering over the tablet.")]
    public float DragScrollingPressureThreshold { get; set; }

    #endregion

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

        if (_outputMode is AbsoluteOutputMode absoluteOutputMode)
            _rotationMatrix = CreateTransformationMatrix(absoluteOutputMode.Input?.Rotation ?? 0f);
        else if (_outputMode is RelativeOutputMode relativeOutputMode)
            _rotationMatrix = CreateTransformationMatrix(relativeOutputMode.Rotation);
        else
        {
            Log.Write("Drag Scroll Binding", $"Unsupported Output Mode: {_outputMode.GetType().Name}", LogLevel.Error);
            return;
        }

        _filter = _outputMode.Elements.OfType<ScrollBindingFilter>().FirstOrDefault();
        _filter?.PositionChanged += Consume;

        if (_filter == null)
            Log.Write("Drag Scroll Binding", $"Failed to find Scroll Binding Filter in the pipeline for '{_tablet?.Properties.Name}'.\n" +
                                              "Enabling 'Scroll Binding Filter' in the Filter tab is required for Drag Scrolling to work.",
                                              LogLevel.Error, false, true);
        else
            _fullyinitialized = true;

        _scrollOnDrag = DragScrollingPressureThreshold > 0;
        _direction = new Vector2(InvertScrollX ? -1 : 1, InvertScrollY ? -1 : 1);
    }

    private static Matrix3x2 CreateTransformationMatrix(float rotation)
        => Matrix3x2.CreateRotation((float)(-rotation * Math.PI / 180));

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
        _reportTimer.Start();
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        _pressing = false;
        _anchorPosition = null;
        _reportTimer.Stop();
    }

    #endregion

    #region Position Processing

    public void Consume(object? sender, IDeviceReport report)
    {
        if (_pressing && report is IAbsolutePositionReport positionReport)
        {
            HandleReport(positionReport);

            if (FrozenCursor && _anchorPosition.HasValue)
                positionReport.Position = _anchorPosition.GetValueOrDefault();

            // Cancel pressure during scroll so users don't click while scrolling
            if (CancelPressure && positionReport is ITabletReport tabletReport)
                tabletReport.Pressure = 0;
        }
    }

    public void HandleReport(IAbsolutePositionReport report)
    {
        switch (report)
        {
            case ITabletReport tabletReport:
                _aboveThreshold = ((float)tabletReport.Pressure / (float)_PenMaxPressure * 100f) > DragScrollingPressureThreshold;
                var requiredPressureReleased = !_scrollOnDrag || !_aboveThreshold;

                if (!_scrollOnDrag || _aboveThreshold)
                    Scroll(tabletReport);
                else if (requiredPressureReleased)
                    _lastPosition = null;

                // The timer is only used for kinetic scrolling.
                // There is of course no point in starting it, if there is no velocity.
                // Finally, there is no point in starting it if the threshold isn't met (or one isn't needed)
                if (EnableKineticScrolling && (_hasVelocityX || _hasVelocityY) && requiredPressureReleased)
                    _timer?.Start();
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
        // Store the first position so we can anchor the cursor
        // The copy expires on Binding Release
        _anchorPosition ??= new Vector2(positionReport.Position.X, positionReport.Position.Y);

        var pos = Vector2.Transform(positionReport.Position, _rotationMatrix);
        
        _lastPosition ??= pos;

        // skip if the position hasn't changed
        if (_lastPosition == pos)
            return;

        var deltaPos = pos - _lastPosition;
        var deltaTime = _reportTimer.Restart();

        _currentVelocity[0] = (((deltaPos?.X ?? 0) * XSensitivity) / deltaTime.Milliseconds) * _direction.X;
        _currentVelocity[1] = (((deltaPos?.Y ?? 0) * YSensitivity) / deltaTime.Milliseconds) * _direction.Y;

        _lastPosition = pos;

        //Log.Debug("Drag Scroll Binding", $"Velocity: X = {_currentVelocity[0]}, Y = {_currentVelocity[1]}");
        
        if (_hasVelocityX = HasVelocity(_currentVelocity[0], 1))
            Wheel.ScrollHorizontally((int)_currentVelocity[0]);
        if (_hasVelocityY = HasVelocity(_currentVelocity[1], 1))
            Wheel.ScrollVertically((int)_currentVelocity[1]);

        if (_hasVelocityX || _hasVelocityY)
            Wheel.Flush();
    }

    private void Decelerate(uint axis)
    {
        if (axis > 1) return;
        
        var deceleration = _currentVelocity[axis] > 0 ? -Deceleration : Deceleration;
        var oldVelocity = (double[])_currentVelocity.Clone();

        _currentVelocity[axis] += deceleration * INTERVAL_MILLISECONDS;

        if (IsOvershooting(oldVelocity[axis], _currentVelocity[axis], THRESHOLD))
            _currentVelocity[axis] = 0;
        
        if (axis == 0)
            Wheel.ScrollHorizontally((int)_currentVelocity[0]);
        else if (axis == 1)
            Wheel.ScrollVertically((int)_currentVelocity[1]);
    }

    #endregion

    #region Event Handlers

    public void IntervalElapsed()
    {
        if (_timer == null || _aboveThreshold) return;

        _hasVelocityX = HasVelocity(_currentVelocity[0], THRESHOLD);
        _hasVelocityY = HasVelocity(_currentVelocity[1], THRESHOLD);

        if (EnableKineticScrolling && _hasVelocityX)
            Decelerate(0);

        if (EnableKineticScrolling && _hasVelocityY)
            Decelerate(1);

        // In the situation where the velocity becomes 0,
        // This will send one last event to the OS to cancel any scrolling.
        if (_hasVelocityX || _hasVelocityY)
            Wheel.Flush();
        else
            _timer?.Stop();
    }

    #endregion

    #region Static Methods

    public static bool HasVelocity(double velocity, double threshold)
        => velocity < -threshold || velocity > threshold;

    /// <summary>
    ///   Prevent scrolling in the opposite direction after deceleration
    /// </summary>
    /// <param name="oldVelocity"></param>
    /// <param name="newVelocity"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public static bool IsOvershooting(double oldVelocity, double newVelocity, double threshold)
    {
        if (oldVelocity > threshold && newVelocity < -threshold)
            return true;
        else if (oldVelocity < -threshold && newVelocity > threshold)
            return true;
        else
            return false;
    }

    #endregion

    #region Interfaces

    public void Dispose()
    {
        _reportTimer.Stop();

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