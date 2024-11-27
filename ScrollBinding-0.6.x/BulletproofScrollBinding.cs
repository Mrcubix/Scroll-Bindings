using OpenTabletDriver.Plugin.Attributes;
using ScrollBinding.Logging;

namespace ScrollBinding;

[PluginName("Scroll Bindings")]
public class NewBulletproofScrollBinding : BulletproofScrollBinding
{
    public NewBulletproofScrollBinding() : base(new BulletproofLogger()) {}

    #region Properties

    [Property("Property"),
     PropertyValidated(nameof(ValidOptions)),
     ToolTip("Legacy Scroll Binding:\n\n" +
             "The direction of the scroll.")]
    public override string Property
    {
        get => base.Property;
        set => base.Property = value;
    }

    [Property("Scroll Delay"),
     DefaultPropertyValue(15),
     Unit("ms"),
     ToolTip("Scroll Binding:\n\n" +
             "The amount of delay between scrolls. \n" +
             "A smaller value will result in a smoother but also faster scroll. \n\n" +
             "Default: 15 ms | Range: 1 - 1000 ms")]
    public int Delay
    {
        get => _scrollDelay;
        set
        {
            
            _scrollDelay = Math.Clamp(value, 1, 1000);

            if (_timer != null)
                _timer.Interval = _scrollDelay;
        }
    }

    [Property("Amount"), 
     DefaultPropertyValue(120),
     ToolTip("Scroll Binding:\n\n" +
             "The amount to scroll in the specified direction. \n" +
             "Note: A tick equals to 120. \n" +
             "Note: This will only affect target applications that supports high resolution scrolling. \n\n" +
             "Default: 120 | Range: 0 - 2400")]
    public int Amount
    {
        get => _scrollAmount;
        set => _scrollAmount = Math.Clamp(value, 0, 2400);
    }

    public static new IEnumerable<string> ValidOptions => BulletproofScrollBinding.ValidOptions;

    #endregion

    public override void Initialize() {}
}