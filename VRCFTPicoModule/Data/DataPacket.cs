using System.Runtime.InteropServices;

namespace VRCFTPicoModule.Data
{
    public abstract class DataPacket
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DataPackHeader
        {
            public byte startCode1;
            public byte startCode2;
            public byte trackingType;
            public byte subType;
            public byte multiPack;
            public byte currentIndex;
            public ushort version;
            public ulong timeStamp;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DataPackBody
        {
            public long timeStamp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 72)]
            public float[] blendShapeWeight;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public float[] videoInputValid;
            public float laughingProb;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public float[] emotionProb;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public float[] reserved;
        }
    }
}
