using System;
using System.Runtime.InteropServices;

namespace AforgeCameraWPF
{
    public static class GetacDLLWrapper
    {
        [DllImport(@"GetacDLL\GetacDLL.dll", EntryPoint = "initGetacDLL", CallingConvention = CallingConvention.StdCall)]
        public static extern void InitGetacDLL();

        [DllImport(@"GetacDLL\GetacDLL.dll", EntryPoint = "CameraFlashLEDON", CallingConvention = CallingConvention.StdCall)]
        public static extern void CameraFlashLEDON();

        [DllImport(@"GetacDLL\GetacDLL.dll", EntryPoint = "CameraFlashLEDON_Timer", CallingConvention = CallingConvention.StdCall)]
        public static extern void CameraFlashLEDON_Timer(uint FlashTimer);
    }
}
