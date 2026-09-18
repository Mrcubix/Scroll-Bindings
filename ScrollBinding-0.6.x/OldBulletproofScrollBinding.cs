using System.Numerics;
using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Platform.Pointer;
using OpenTabletDriver.Plugin.Tablet;
using ScrollBinding.Lib.Enums;
using ScrollBinding.Lib.Interfaces;
using ScrollBinding.Logging;
using ITimer = OpenTabletDriver.Plugin.Timers.ITimer;
using LogLevel = ScrollBinding.Lib.Enums.LogLevel;

#nullable enable

namespace ScrollBinding;

[PluginIgnore]
[PluginName("Lagacy Scroll Bindings (Obselete)")]
public class BulletproofScrollBinding : ScrollBindingBase, IStateBinding
{
    #region Fields

    private Vector2? _lastPositionCopy = null; 
    private ScrollBindingFilter? _filter;
    private TabletReference? _tablet;
    private IOutputMode? _outputMode;
    private Driver? _driver;
    private string _property = string.Empty;

    protected bool _syncCursorOnPress = false;
    protected ITimer? _timer;

    #endregion

    #region Constructor

    public BulletproofScrollBinding() : base(new BulletproofLogger()) 
    {
        if (ScrollBindingSettings.Instance == null)
            ScrollBindingSettings.SettingsChanged += OnSettingsChanged;
        else
            Initialize();
    }

    public BulletproofScrollBinding(ILogger logger) : base(logger) {}

    #endregion

    #region Properties

    [Property("Property"),
     PropertyValidated(nameof(ValidOptions)),
     ToolTip("Legacy Scroll Binding:\n\n" +
             "The direction of the scroll. \n\n" +
             "Note: Use 'Scroll Binding' instead as this legacy Plugin Type is depecrated & is only available for backwards compatibility.")]
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

    [TabletReference]
    public TabletReference? Tablet
    {
        get => _tablet;
        set
        {
            _tablet = value;
            Initialize();
        }
    }

    [Resolved]
    public IAbsolutePointer? Pointer { get; set; }

    [Resolved]
    public IDriver? Driver
    {
        get => _driver;
        set => _driver = value as Driver;
    }

    [Resolved]
    public ITimer? Timer
    {
        get => _timer;
        set
        {
            _timer = value;

            if (_timer != null)
            {
                _timer.Interval = _scrollDelay;
                _timer.Elapsed += Scroll;
            }
        }
    }

    public static IEnumerable<string> ValidOptions => _scrollDirections.Keys;

    #endregion

    #region Initialization Methods

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

        _scrollDelay = settings.ScrollDelay;
        _timer?.Interval = _scrollDelay;

        if (_syncCursorOnPress)
            FetchPositionFilter();
    }

    private void FetchPositionFilter()
    {
        if (_tablet == null || _driver == null)
            return;

        var tree = _driver.InputDevices.FirstOrDefault(dev => dev.Properties.Name == _tablet.Properties.Name);
        
        if (tree == null || tree.OutputMode == null)
        {
            Logger.Write("Drag Scroll Binding", $"Failed to find the Device Tree or Output Mode for '{_tablet.Properties.Name}'.", LogLevel.Error);
            return;
        }
        
        _outputMode = tree.OutputMode;
        _filter = _outputMode.Elements.OfType<ScrollBindingFilter>().FirstOrDefault();

        if (_filter == null)
        {
            Logger.Write("Drag Scroll Binding", 
                        $"Failed to find the Position Filter for : '{_tablet.Properties.Name}'.\n" +
                         "This is required for the Binding to scroll at the right location in Artist or Windows Ink Output Modes.\n" +
                         "Without it, activating the binding may not scroll within the active application.", LogLevel.Warning);
            return;
        }

        // No point in syncing while using a mouse mode
        if (_outputMode is AbsoluteOutputMode or RelativeOutputMode)
        {
            _syncCursorOnPress = false;
            return;
        }

        _filter.PositionChanged += Consume;
    }

    #endregion

    #region Binding Methods

    public void Press(TabletReference tablet, IDeviceReport report) => StartScrolling();

    public void Release(TabletReference tablet, IDeviceReport report) 
    {
        _scrolling = false;
        _timer?.Stop();
    }

    #endregion

    #region Scroll Methods

    protected override void ScrollContinuously() => _timer?.Start();

    protected override void Scroll()
    {
        if (_syncCursorOnPress && _lastPositionCopy is { } position)
            SyncCursor(Pointer, position);

        base.Scroll();
    }

    #endregion

    private void Consume(object? sender, IDeviceReport report)
    {
        if (report is IAbsolutePositionReport positionReport)
            _lastPositionCopy = new Vector2(positionReport.Position.X, positionReport.Position.Y);
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Initialize();

    #region Static Methods

    private static void SyncCursor(IAbsolutePointer? pointer, Vector2 position)
    {
        pointer?.SetPosition(position);
        if (pointer is ISynchronousPointer synchronousPointer)
            synchronousPointer.Flush();
    }

    #endregion
}