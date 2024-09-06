using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFTPicoModule;

public class VRCFTPicoModule : ExtTrackingModule
{
    private const int port = 29765;
    private readonly UdpClient udpClient = new(port);

    # region Data
    private enum BlendShapeIndex
    {
        EyeLookDown_L = 0,
        NoseSneer_L = 1,
        EyeLookIn_L = 2,
        BrowInnerUp = 3,
        BrowDown_R = 4,
        MouthClose = 5,
        MouthLowerDown_R = 6,
        JawOpen = 7,
        MouthUpperUp_R = 8,
        MouthShrugUpper = 9,
        MouthFunnel = 10,
        EyeLookIn_R = 11,
        EyeLookDown_R = 12,
        NoseSneer_R = 13,
        MouthRollUpper = 14,
        JawRight = 15,
        BrowDown_L = 16,
        MouthShrugLower = 17,
        MouthRollLower = 18,
        MouthSmile_L = 19,
        MouthPress_L = 20,
        MouthSmile_R = 21,
        MouthPress_R = 22,
        MouthDimple_R = 23,
        MouthLeft = 24,
        JawForward = 25,
        EyeSquint_L = 26,
        MouthFrown_L = 27,
        EyeBlink_L = 28,
        CheekSquint_L = 29,
        BrowOuterUp_L = 30,
        EyeLookUp_L = 31,
        JawLeft = 32,
        MouthStretch_L = 33,
        MouthPucker = 34,
        EyeLookUp_R = 35,
        BrowOuterUp_R = 36,
        CheekSquint_R = 37,
        EyeBlink_R = 38,
        MouthUpperUp_L = 39,
        MouthFrown_R = 40,
        EyeSquint_R = 41,
        MouthStretch_R = 42,
        CheekPuff = 43,
        EyeLookOut_L = 44,
        EyeLookOut_R = 45,
        EyeWide_R = 46,
        EyeWide_L = 47,
        MouthRight = 48,
        MouthDimple_L = 49,
        MouthLowerDown_L = 50,
        TongueOut = 51,
        PP = 52,
        CH = 53,
        o = 54,
        O = 55,
        I = 56,
        u = 57,
        RR = 58,
        XX = 59,
        aa = 60,
        i = 61,
        FF = 62,
        U = 63,
        TH = 64,
        kk = 65,
        SS = 66,
        e = 67,
        DD = 68,
        E = 69,
        nn = 70,
        sil = 71
    };

    // From PicoStreamingAssistantFTUDP
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
    # endregion

    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        ModuleInformation.Name = "PICO Connect";

        var stream = GetType().Assembly.GetManifestResourceStream("VRCFTPicoModule.Assets.pico.png");
        ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        udpClient.Client.ReceiveTimeout = 100;

        return (true, true);
    }

    public override void Update()
    {
        byte[] receivedData;

        try
        {
            var endPoint = new IPEndPoint(IPAddress.Any, port);
            receivedData = udpClient.Receive(ref endPoint);
        }
        catch (Exception)
        {
            return;
        }

        var pShape = ParseData(receivedData);

        if (pShape == null) 
            return;

        UpdateEye(pShape);
        UpdataExpression(pShape);
    }

    private static float[] ParseData(byte[] receivedData)
    {
        var dataHeaderSize = Marshal.SizeOf<DataPackHeader>();
        var dataBodySize = Marshal.SizeOf<DataPackBody>();
        var dataSize = dataHeaderSize + dataBodySize;
        if (receivedData.Length < dataSize)
            return Array.Empty<float>();

        var span = receivedData.AsSpan();

        var header = ByteArrayToStructure<DataPackHeader>(receivedData, 0);
        if (header.trackingType != 2)
            return Array.Empty<float>();

        var data = ByteArrayToStructure<DataPackBody>(receivedData, Marshal.SizeOf(typeof(DataPackHeader)));

        var pShape = data.blendShapeWeight;

        return pShape;
    }

    private static void UpdateEye(float[] pShape)
    {
        var eye = UnifiedTracking.Data.Eye;

        #region LeftEye
        eye.Left.Openness = 1f - pShape[(int)BlendShapeIndex.EyeBlink_L];
        eye.Left.Gaze.x = pShape[(int)BlendShapeIndex.EyeLookIn_L] - pShape[(int)BlendShapeIndex.EyeLookOut_L];
        eye.Left.Gaze.y = pShape[(int)BlendShapeIndex.EyeLookUp_L] - pShape[(int)BlendShapeIndex.EyeLookDown_L];
        #endregion

        #region RightEye
        eye.Right.Openness = 1f - pShape[(int)BlendShapeIndex.EyeBlink_R];
        eye.Right.Gaze.x = pShape[(int)BlendShapeIndex.EyeLookOut_R] - pShape[(int)BlendShapeIndex.EyeLookIn_R];
        eye.Right.Gaze.y = pShape[(int)BlendShapeIndex.EyeLookUp_R] - pShape[(int)BlendShapeIndex.EyeLookDown_R];
        #endregion
    }

    private static void UpdataExpression(float[] pShape)
    {
        #region Brow
        SetParam(pShape, BlendShapeIndex.BrowInnerUp, UnifiedExpressions.BrowInnerUpLeft);
        SetParam(pShape, BlendShapeIndex.BrowInnerUp, UnifiedExpressions.BrowInnerUpRight);
        SetParam(pShape, BlendShapeIndex.BrowOuterUp_L, UnifiedExpressions.BrowOuterUpLeft);
        SetParam(pShape, BlendShapeIndex.BrowOuterUp_R, UnifiedExpressions.BrowOuterUpRight);
        SetParam(pShape, BlendShapeIndex.BrowDown_L, UnifiedExpressions.BrowLowererLeft);
        SetParam(pShape, BlendShapeIndex.BrowDown_L, UnifiedExpressions.BrowPinchLeft);
        SetParam(pShape, BlendShapeIndex.BrowDown_R, UnifiedExpressions.BrowLowererRight);
        SetParam(pShape, BlendShapeIndex.BrowDown_R, UnifiedExpressions.BrowPinchRight);
        #endregion

        #region Eye
        SetParam(pShape, BlendShapeIndex.EyeSquint_L, UnifiedExpressions.EyeSquintLeft);
        SetParam(pShape, BlendShapeIndex.EyeSquint_R, UnifiedExpressions.EyeSquintRight);
        SetParam(pShape, BlendShapeIndex.EyeWide_L, UnifiedExpressions.EyeWideLeft);
        SetParam(pShape, BlendShapeIndex.EyeWide_R, UnifiedExpressions.EyeWideRight);
        #endregion

        #region Jaw
        SetParam(pShape, BlendShapeIndex.JawOpen, UnifiedExpressions.JawOpen);
        SetParam(pShape, BlendShapeIndex.JawLeft, UnifiedExpressions.JawLeft);
        SetParam(pShape, BlendShapeIndex.JawRight, UnifiedExpressions.JawRight);
        SetParam(pShape, BlendShapeIndex.JawForward, UnifiedExpressions.JawForward);
        SetParam(pShape, BlendShapeIndex.MouthClose, UnifiedExpressions.MouthClosed);
        #endregion

        #region Cheek
        SetParam(pShape, BlendShapeIndex.CheekPuff, UnifiedExpressions.CheekPuffLeft);
        SetParam(pShape, BlendShapeIndex.CheekPuff, UnifiedExpressions.CheekPuffRight);
        SetParam(pShape, BlendShapeIndex.CheekSquint_L, UnifiedExpressions.CheekSquintLeft);
        SetParam(pShape, BlendShapeIndex.CheekSquint_R, UnifiedExpressions.CheekSquintRight);
        #endregion

        #region Nose
        SetParam(pShape, BlendShapeIndex.NoseSneer_L, UnifiedExpressions.NoseSneerLeft);
        SetParam(pShape, BlendShapeIndex.NoseSneer_R, UnifiedExpressions.NoseSneerRight);
        #endregion

        #region Mouth
        SetParam(pShape, BlendShapeIndex.MouthUpperUp_L, UnifiedExpressions.MouthUpperUpLeft);
        SetParam(pShape, BlendShapeIndex.MouthUpperUp_R, UnifiedExpressions.MouthUpperUpRight);
        SetParam(pShape, BlendShapeIndex.MouthLowerDown_L, UnifiedExpressions.MouthLowerDownLeft);
        SetParam(pShape, BlendShapeIndex.MouthLowerDown_R, UnifiedExpressions.MouthLowerDownRight);
        SetParam(pShape, BlendShapeIndex.MouthFrown_L, UnifiedExpressions.MouthFrownLeft);
        SetParam(pShape, BlendShapeIndex.MouthFrown_R, UnifiedExpressions.MouthFrownRight);
        SetParam(pShape, BlendShapeIndex.MouthDimple_L, UnifiedExpressions.MouthDimpleLeft);
        SetParam(pShape, BlendShapeIndex.MouthDimple_R, UnifiedExpressions.MouthDimpleRight);
        SetParam(pShape, BlendShapeIndex.MouthLeft, UnifiedExpressions.MouthUpperLeft);
        SetParam(pShape, BlendShapeIndex.MouthLeft, UnifiedExpressions.MouthLowerLeft);
        SetParam(pShape, BlendShapeIndex.MouthRight, UnifiedExpressions.MouthUpperRight);
        SetParam(pShape, BlendShapeIndex.MouthRight, UnifiedExpressions.MouthLowerRight);
        SetParam(pShape, BlendShapeIndex.MouthPress_L, UnifiedExpressions.MouthPressLeft);
        SetParam(pShape, BlendShapeIndex.MouthPress_R, UnifiedExpressions.MouthPressRight);
        SetParam(pShape, BlendShapeIndex.MouthShrugLower, UnifiedExpressions.MouthRaiserLower);
        SetParam(pShape, BlendShapeIndex.MouthShrugUpper, UnifiedExpressions.MouthRaiserUpper);
        SetParam(pShape, BlendShapeIndex.MouthSmile_L, UnifiedExpressions.MouthCornerPullLeft);
        SetParam(pShape, BlendShapeIndex.MouthSmile_L, UnifiedExpressions.MouthCornerSlantLeft);
        SetParam(pShape, BlendShapeIndex.MouthSmile_R, UnifiedExpressions.MouthCornerPullRight);
        SetParam(pShape, BlendShapeIndex.MouthSmile_R, UnifiedExpressions.MouthCornerSlantRight);
        SetParam(pShape, BlendShapeIndex.MouthStretch_L, UnifiedExpressions.MouthStretchLeft);
        SetParam(pShape, BlendShapeIndex.MouthStretch_R, UnifiedExpressions.MouthStretchRight);
        #endregion

        #region Lip
        SetParam(pShape, BlendShapeIndex.MouthFunnel, UnifiedExpressions.LipFunnelUpperLeft);
        SetParam(pShape, BlendShapeIndex.MouthFunnel, UnifiedExpressions.LipFunnelUpperRight);
        SetParam(pShape, BlendShapeIndex.MouthFunnel, UnifiedExpressions.LipFunnelLowerLeft);
        SetParam(pShape, BlendShapeIndex.MouthFunnel, UnifiedExpressions.LipFunnelLowerRight);
        SetParam(pShape, BlendShapeIndex.MouthPucker, UnifiedExpressions.LipPuckerUpperLeft);
        SetParam(pShape, BlendShapeIndex.MouthPucker, UnifiedExpressions.LipPuckerUpperRight);
        SetParam(pShape, BlendShapeIndex.MouthPucker, UnifiedExpressions.LipPuckerLowerLeft);
        SetParam(pShape, BlendShapeIndex.MouthPucker, UnifiedExpressions.LipPuckerLowerRight);
        SetParam(pShape, BlendShapeIndex.MouthRollUpper, UnifiedExpressions.LipSuckUpperLeft);
        SetParam(pShape, BlendShapeIndex.MouthRollUpper, UnifiedExpressions.LipSuckUpperRight);
        SetParam(pShape, BlendShapeIndex.MouthRollLower, UnifiedExpressions.LipSuckLowerLeft);
        SetParam(pShape, BlendShapeIndex.MouthRollLower, UnifiedExpressions.LipSuckLowerRight);
        #endregion

        #region Tongue
        SetParam(pShape, BlendShapeIndex.TongueOut, UnifiedExpressions.TongueOut);
        #endregion
    }

    private static void SetParam(float[] pShape, BlendShapeIndex index, UnifiedExpressions outputType)
    {
        UnifiedTracking.Data.Shapes[(int)outputType].Weight = pShape[(int)index];
    }

    private static T ByteArrayToStructure<T>(byte[] bytes, int offset) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        IntPtr ptr = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.Copy(bytes, offset, ptr, size);
            return (T)Marshal.PtrToStructure(ptr, typeof(T));
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public override void Teardown()
    {
        udpClient.Dispose();
    }
}