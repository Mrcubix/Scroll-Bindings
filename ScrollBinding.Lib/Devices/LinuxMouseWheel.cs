using ScrollBinding.Lib.Enums;
using ScrollBinding.Lib.Interfaces;
using ScrollBinding.Native.Linux.Evdev;

namespace ScrollBinding.Lib.Devices
{
    public class LinuxMouseWheel : IMouseWheel
    {
        private bool _dirty;

        public LinuxMouseWheel(ILogger logger)
        {
            Device = new EvdevDevice("OpenTabletDriver Mouse Wheel");

            Device.EnableTypeCodes(EventType.EV_REL);

            Device.EnableTypeCodes(
                EventType.EV_REL,
                EventCode.REL_WHEEL,
                EventCode.REL_WHEEL_HI_RES,
                EventCode.REL_HWHEEL,
                EventCode.REL_HWHEEL_HI_RES
            );

            var result = Device.Initialize();

            switch (result)
            {
                case ERRNO.NONE:
                    logger?.Debug("Evdev", $"Successfully initialized virtual mouse. (code {result})");
                    break;
                default:
                    logger?.Write("Evdev", $"Failed to initialize virtual mouse. (error code {result})", LogLevel.Error);
                    break;
            }
        }


        public EvdevDevice Device { get; }

        public void ScrollVertically(int amount)
        {
            SetDirty();

            Device.Write(EventType.EV_REL, EventCode.REL_WHEEL_HI_RES, amount);
        }

        public void ScrollHorizontally(int amount)
        {
            SetDirty();

            Device.Write(EventType.EV_REL, EventCode.REL_HWHEEL_HI_RES, amount);
        }

        public void Flush()
        {
            if (_dirty)
            {
                Device.Sync();
                _dirty = false;
            }
        }

        protected void SetDirty()
        {
            _dirty = true;
        }
    }
}