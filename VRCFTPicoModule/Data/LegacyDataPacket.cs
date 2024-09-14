using System.Runtime.InteropServices;

namespace VRCFTPicoModule.Data
{
    public class LegacyDataPacket
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DataPackBody
        {
            public Int64 timestamp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 72)]
            public float[] blendShapeWeight;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public float[] videoInputValid;
            public float laughingProb;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public float[] emotionProb;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public float[] reserved;
        };
    }
}
