using System;
using ScrollBinding.Lib.Interfaces;

namespace ScrollBinding.Lib.Devices
{
    public class MacOSMouseWheel : IMouseWheel
    {
        #pragma warning disable CS0414
        private bool _dirty;

        public MacOSMouseWheel(ILogger logger)
        {
            throw new NotImplementedException();
        }

        public void ScrollVertically(int amount)
        {
            throw new NotImplementedException();
        }

        public void ScrollHorizontally(int amount)
        {
            throw new NotImplementedException();
        }

        public void Flush()
        {
            throw new NotImplementedException();
        }

        protected void SetDirty()
        {
            _dirty = true;
        }
    }
}