using System;
using ScrollBinding.Lib.Interfaces;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ScrollBinding.Lib.Devices
{
    public unsafe class WindowsMouseWheel : IMouseWheel
    {
        private bool _dirty;

        protected INPUT[] inputs = new[]
        {
            new INPUT
            {
                type = INPUT_TYPE.INPUT_MOUSE,
                Anonymous = new()
                {
                    mi = new MOUSEINPUT
                    {
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            }
        };

        public void ScrollVertically(int amount)
        {
            SetDirty();

            inputs[0].Anonymous.mi.dwFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;
            inputs[0].Anonymous.mi.mouseData = (uint)amount;
        }

        public void ScrollHorizontally(int amount)
        {
            SetDirty();

            inputs[0].Anonymous.mi.dwFlags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_HWHEEL | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;
            inputs[0].Anonymous.mi.mouseData = (uint)amount;
        }

        public void Flush()
        {
            if (_dirty)
            {
                PInvoke.SendInput(inputs, sizeof(INPUT));

                inputs[0].Anonymous.mi.dwFlags = 0;
                inputs[0].Anonymous.mi.mouseData = 0;
                
                _dirty = false;
            }
        }

        protected void SetDirty()
        {
            _dirty = true;
        }
    }
}