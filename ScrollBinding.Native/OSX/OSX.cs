using System;
using System.Runtime.InteropServices;
using ScrollBinding.Native.OSX.Input;

namespace ScrollBinding.Native.OSX
{
    public class OSX
    {
        private const string Quartz = "/System/Library/Frameworks/Quartz.framework/Versions/Current/Quartz";

        [DllImport(Quartz)]
        public static extern void CFRelease(IntPtr handle);

        [DllImport(Quartz)]
        public extern static IntPtr CGEventPost(CGEventTapLocation tap, IntPtr eventRef);

        [DllImport(Quartz)]
        public extern static IntPtr CGEventCreateScrollWheelEvent2(IntPtr source, CGScrollEventUnit units, uint wheelCount, int wheel1, int wheel2, int wheel3);
    }
}