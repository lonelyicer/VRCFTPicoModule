using System;
using System.Runtime.InteropServices;

namespace VRCFTPicoModule.Utils
{
    public static class DataPacketHelpers
    {
        public static T ByteArrayToStructure<T>(byte[] bytes, int offset = 0) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.Copy(bytes, offset, ptr, size);
                return (T)Marshal.PtrToStructure(ptr, typeof(T))!;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}