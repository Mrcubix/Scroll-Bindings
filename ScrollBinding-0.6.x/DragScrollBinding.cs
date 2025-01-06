using System.Numerics;
using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Timers;
using ScrollBinding.Lib.Interfaces;

namespace ScrollBinding;

[PluginName("Drag Scroll")]
public sealed class DragScrollBinding : IStateBinding, IEquatable<DragScrollBinding>, IDisposable
{
    #region Fields

    private static readonly DragScrollingElement _element = new();
    private readonly static object _lock = new();
    private static bool _sharedInitialized;

    private const double INTERVAL_MILLISECONDS = 1;
    private const double INTERVAL_SECONDS = INTERVAL_MILLISECONDS / 1000;

    private readonly IMouseWheel Wheel = ScrollBindingBase.CurrentPlatformWheel;
    private Vector<double> _currentVelocity = new([0d, 0d, 0d, 0d]);
    private double[] _currentVelocityArray = [0, 0, 0, 0];
    private TabletReference _tablet;
    private Vector2? _lastPosition;
    private double _deltaTime; // in milliseconds
    private bool _toolActive;
    private bool _pressing;
    private ITimer _timer;

    #endregion

    #region Properties

    [Resolved]
    public ITimer Timer
    {
        get => _timer;
        set
        {
            _timer = value;

            if (_timer != null)
            {
                _timer.Interval = (float)INTERVAL_MILLISECONDS;
                _timer.Elapsed += OnElasped;
                _timer.Start();
            }
        }
    }

    [Resolved]
    public IDriver Driver { get; set; }

    [TabletReference]
    public TabletReference Tablet
    {
        get => _tablet;
        set
        {
            _tablet = value;
            _ = Task.Run(InitializeAsync);
        }
    }

    [Property("Sensitivity"),
     DefaultPropertyValue(1d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The sensitivity of the drag scroll binding. Higher values will result in faster scrolling.")]
    public double Sensitivity { get; set; } = 1;

    [BooleanProperty("Enable Kinetic Scrolling", ""),
     DefaultPropertyValue(true),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Scrolling speed will slowly drop to 0 after releasing pressure.")]
    public bool EnableKineticScrolling { get; set; } = true;

    [Property("Deceleration"),
     DefaultPropertyValue(0.1d),
     ToolTip("Drag Scroll Binding:\n\n" +
             "The amount of decceleration applied to the scroll velocity when the user releases the binding.")]
    public double Deceleration { get; set; } = 0.1;

    [BooleanProperty("Invert Scroll", ""),
     DefaultPropertyValue(false),
     ToolTip("Drag Scroll Binding:\n\n" +
             "Inverts the scroll direction of the drag scroll binding.")]
    public bool InvertScroll { get; set; }

    #endregion

    #region Methods

    // It has to be done async as we need to wait for the driver to set elements in the output mode which is done after Constructing bindings then filters
    public async Task InitializeAsync()
    {
        if (_element.Bindings.Contains(this) == false)
            _element.Bindings.Add(this);

        while (_sharedInitialized == false)
        {
            if (Driver is Driver driver)
            {
                IOutputMode outputMode = driver.InputDevices.Where(dev => dev.OutputMode.Tablet == _tablet)
                                                            .Select(dev => dev.OutputMode)
                                                            .FirstOrDefault();

                if (outputMode != null && outputMode.Elements != null && outputMode.Elements.Count != 0)
                {
                    lock (_lock)
                    {
                        if (outputMode.Elements.OfType<DragScrollingElement>().Any() == false)
                        {
                            outputMode.Elements.Add(_element);
                            outputMode.Elements = outputMode.Elements;
                        }

                        _sharedInitialized = true;
                    }
                }
                else
                    await Task.Delay(15);
            }
        }
    }

    public void Press(TabletReference tablet, IDeviceReport report)
    {
        _currentVelocity = new([0d, 0d, 0d, 0d]);
        _pressing = true;
        _lastPosition = null;
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        _pressing = false;
    }

    public void Scroll(IDeviceReport report)
    {
        if (report is OutOfRangeReport ||
           (report is ITabletReport tabletReport && tabletReport.Pressure == 0))
        {
            Reset();
        }
        else if (report is IAbsolutePositionReport positionReport)
        {
            _toolActive = true;

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
    }

    private void Decelerate()
    {
        var deccelerationX = _currentVelocityArray[0] > 0 ? -Deceleration : Deceleration;
        var deccelerationY = _currentVelocityArray[1] > 0 ? -Deceleration : Deceleration;

        var oldVelocity = _currentVelocityArray.Clone() as double[];

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

    #region Event Handlers

    public void OnElasped()
    {
        _deltaTime += (ulong)Timer.Interval;

        if (EnableKineticScrolling && !_toolActive && (_currentVelocity[1] < -1 || _currentVelocity[1] > 1))
            //(_currentVelocity[0] < -1 || _currentVelocity[0] > 1))
            Decelerate();
    }

    #endregion

    #region Reset & Interfaces

    private void Reset()
    {
        _lastPosition = null;
        _toolActive = false;
        _deltaTime = 0;
    }

    public bool Equals(DragScrollBinding other)
    {
        return this == other ||
               other != null &&
               Sensitivity == other.Sensitivity &&
               Deceleration == other.Deceleration &&
               InvertScroll == other.InvertScroll;
    }

    public override bool Equals(object obj) 
        => obj is DragScrollBinding binding && Equals(binding);

    public override int GetHashCode() => base.GetHashCode();

    public void Dispose()
    {
        _sharedInitialized = false;

        if (_timer != null)
        {
            _timer.Elapsed -= OnElasped;
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
    }

    #endregion

    #endregion
}

#region Pipeline Element

[PluginIgnore]
public sealed class DragScrollingElement : IPositionedPipelineElement<IDeviceReport>, IDisposable
{
    private readonly object _lock = new();

    public event Action<IDeviceReport> Emit;

    public PipelinePosition Position => PipelinePosition.PostTransform;
    public List<DragScrollBinding> Bindings { get; } = new();

    public void Consume(IDeviceReport report)
    {
        if (report is OutOfRangeReport || report is IAbsolutePositionReport)
            lock (_lock)
                foreach (var binding in Bindings)
                    binding.Scroll(report);
        Emit?.Invoke(report);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var binding in Bindings)
                binding.Dispose();

            Bindings.Clear();
        }
    }
}

#endregion