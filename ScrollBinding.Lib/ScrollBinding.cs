using System.Collections.Generic;
using ScrollBinding.Lib.Enums;
using ScrollBinding.Lib.Interfaces;

namespace ScrollBinding;

public abstract class ScrollBinding
{
    protected ScrollDirection _scrollDirection = ScrollDirection.None;
    protected int _scrollAmount = 20;

    #region Properties

    public abstract string Property { get; set; }

    public IMouseWheel Wheel { get; protected set; }

    #endregion

    #region Methods

    public abstract void Initialize();

    public void Scroll()
    {
        if (Wheel == null) return;

        switch (_scrollDirection)
        {
            case ScrollDirection.Forward:
                Wheel.ScrollForward(_scrollAmount);
                break;
            case ScrollDirection.Backward:
                Wheel.ScrollBackward(_scrollAmount);
                break;
            case ScrollDirection.Left:
                Wheel.ScrollLeft(_scrollAmount);
                break;
            case ScrollDirection.Right:
                Wheel.ScrollRight(_scrollAmount);
                break;
            default:
                break;
        }
    }

    #endregion

    #region Static Fields

    protected static Dictionary<string, ScrollDirection> _scrollDirections = new()
    {
        { "Scroll Forward", ScrollDirection.Forward },
        { "Scroll Backward", ScrollDirection.Backward },
        { "Scroll Left", ScrollDirection.Left },
        { "Scroll Right", ScrollDirection.Right }
    };

    #endregion
}