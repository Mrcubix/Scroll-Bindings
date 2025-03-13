using System;
using ScrollBinding.Lib.Interfaces;
using ScrollBinding.Native.OSX;
using ScrollBinding.Native.OSX.Input;

namespace ScrollBinding.Lib.Devices
{
    public class MacOSMouseWheel : IMouseWheel
    {
        private bool _dirty;

        int dx, dy;

        public void ScrollVertically(int amount)
        {
            dy = -amount;
            SetDirty();
        }

        public void ScrollHorizontally(int amount)
        {
            dx = -amount;
            SetDirty();
        }

        public void Flush()
        {
            if (_dirty)
            {
                var eventRef = OSX.CGEventCreateScrollWheelEvent2(IntPtr.Zero, CGScrollEventUnit.kCGScrollEventUnitPixel, 2, dy, dx, 0);
                OSX.CGEventPost(CGEventTapLocation.kCGHIDEventTap, eventRef);
                OSX.CFRelease(eventRef);
                _dirty = false;
                dx = dy = 0;
            }
        }

        protected void SetDirty()
        {
            _dirty = true;
        }
    }
}