using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFTPicoModule.Data;

namespace VRCFTPicoModule.Utils
{
    public class Updater(UdpClient udpClient, ILogger logger, bool isLegacy)
    {
        private int _timeOut;
        private float _lastMouthLeft;
        private float _lastMouthRight;
        private const float SmoothingFactor = 0.5f;
        public ModuleState ModuleState;

        public void Update()
        {
            if (ModuleState != ModuleState.Active) return;

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = udpClient.Receive(ref endPoint);
                var pShape = ParseData(data, isLegacy);

                UpdateEye(pShape);
                UpdateExpression(pShape);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                if (++_timeOut > 600)
                {
                    logger.LogWarning("Receive data timed out.");
                    _timeOut = 0;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Update failed with exception: {0}", ex);
            }
        }

        private static float[] ParseData(byte[] data, bool isLegacy)
        {
            if (isLegacy && data.Length >= Marshal.SizeOf<LegacyDataPacket.DataPackBody>())
                return DataPacketHelpers.ByteArrayToStructure<LegacyDataPacket.DataPackBody>(data).blendShapeWeight;

            if (data.Length >= Marshal.SizeOf<DataPacket.DataPackHeader>() + Marshal.SizeOf<DataPacket.DataPackBody>())
            {
                var header = DataPacketHelpers.ByteArrayToStructure<DataPacket.DataPackHeader>(data);
                if (header.trackingType == 2)
                    return DataPacketHelpers.ByteArrayToStructure<DataPacket.DataPackBody>(data, Marshal.SizeOf<DataPacket.DataPackHeader>()).blendShapeWeight;
            }
            return Array.Empty<float>();
        }

        private static void UpdateEye(float[] pShape)
        {
            var eye = UnifiedTracking.Data.Eye;

            #region LeftEye
            eye.Left.Openness = 1f - pShape[(int)BlendShape.Index.EyeBlink_L];
            eye.Left.Gaze.x = pShape[(int)BlendShape.Index.EyeLookIn_L] - pShape[(int)BlendShape.Index.EyeLookOut_L];
            eye.Left.Gaze.y = pShape[(int)BlendShape.Index.EyeLookUp_L] - pShape[(int)BlendShape.Index.EyeLookDown_L];
            #endregion

            #region RightEye
            eye.Right.Openness = 1f - pShape[(int)BlendShape.Index.EyeBlink_R];
            eye.Right.Gaze.x = pShape[(int)BlendShape.Index.EyeLookOut_R] - pShape[(int)BlendShape.Index.EyeLookIn_R];
            eye.Right.Gaze.y = pShape[(int)BlendShape.Index.EyeLookUp_R] - pShape[(int)BlendShape.Index.EyeLookDown_R];
            #endregion
        }

        private void UpdateExpression(float[] pShape)
        {
            #region Brow
            SetParam(pShape, BlendShape.Index.BrowInnerUp, UnifiedExpressions.BrowInnerUpLeft);
            SetParam(pShape, BlendShape.Index.BrowInnerUp, UnifiedExpressions.BrowInnerUpRight);
            SetParam(pShape, BlendShape.Index.BrowOuterUp_L, UnifiedExpressions.BrowOuterUpLeft);
            SetParam(pShape, BlendShape.Index.BrowOuterUp_R, UnifiedExpressions.BrowOuterUpRight);
            SetParam(pShape, BlendShape.Index.BrowDown_L, UnifiedExpressions.BrowLowererLeft);
            SetParam(pShape, BlendShape.Index.BrowDown_L, UnifiedExpressions.BrowPinchLeft);
            SetParam(pShape, BlendShape.Index.BrowDown_R, UnifiedExpressions.BrowLowererRight);
            SetParam(pShape, BlendShape.Index.BrowDown_R, UnifiedExpressions.BrowPinchRight);
            #endregion

            #region Eye
            SetParam(pShape, BlendShape.Index.EyeSquint_L, UnifiedExpressions.EyeSquintLeft);
            SetParam(pShape, BlendShape.Index.EyeSquint_R, UnifiedExpressions.EyeSquintRight);
            SetParam(pShape, BlendShape.Index.EyeWide_L, UnifiedExpressions.EyeWideLeft);
            SetParam(pShape, BlendShape.Index.EyeWide_R, UnifiedExpressions.EyeWideRight);
            #endregion

            #region Jaw
            SetParam(pShape, BlendShape.Index.JawOpen, UnifiedExpressions.JawOpen);
            SetParam(pShape, BlendShape.Index.JawLeft, UnifiedExpressions.JawLeft);
            SetParam(pShape, BlendShape.Index.JawRight, UnifiedExpressions.JawRight);
            SetParam(pShape, BlendShape.Index.JawForward, UnifiedExpressions.JawForward);
            SetParam(pShape, BlendShape.Index.MouthClose, UnifiedExpressions.MouthClosed);
            #endregion

            #region Cheek
            SetParam(pShape, BlendShape.Index.CheekSquint_L, UnifiedExpressions.CheekSquintLeft);
            SetParam(pShape, BlendShape.Index.CheekSquint_R, UnifiedExpressions.CheekSquintRight);

            var mouthLeft = SmoothValue(pShape[(int)BlendShape.Index.MouthLeft], ref _lastMouthLeft);
            var mouthRight = SmoothValue(pShape[(int)BlendShape.Index.MouthRight], ref _lastMouthRight);

            var cheekPuff = pShape[(int)BlendShape.Index.CheekPuff];
            var diffThreshold = 0.1f;

            if (cheekPuff > 0.1f)
            {
                if (mouthLeft > mouthRight + diffThreshold)
                {
                    SetParam(cheekPuff, UnifiedExpressions.CheekPuffLeft);
                    SetParam(cheekPuff + mouthLeft, UnifiedExpressions.CheekPuffRight);
                }
                else if (mouthRight > mouthLeft + diffThreshold)
                {
                    SetParam(cheekPuff + mouthRight, UnifiedExpressions.CheekPuffLeft);
                    SetParam(cheekPuff, UnifiedExpressions.CheekPuffRight);
                }
                else
                {
                    SetParam(pShape, BlendShape.Index.CheekPuff, UnifiedExpressions.CheekPuffLeft);
                    SetParam(pShape, BlendShape.Index.CheekPuff, UnifiedExpressions.CheekPuffRight);
                }
            }
            else
            {
                SetParam(pShape, BlendShape.Index.CheekPuff, UnifiedExpressions.CheekPuffLeft);
                SetParam(pShape, BlendShape.Index.CheekPuff, UnifiedExpressions.CheekPuffRight);
            }
            #endregion

            #region Nose
            SetParam(pShape, BlendShape.Index.NoseSneer_L, UnifiedExpressions.NoseSneerLeft);
            SetParam(pShape, BlendShape.Index.NoseSneer_R, UnifiedExpressions.NoseSneerRight);
            #endregion

            #region Mouth
            SetParam(pShape, BlendShape.Index.MouthUpperUp_L, UnifiedExpressions.MouthUpperUpLeft);
            SetParam(pShape, BlendShape.Index.MouthUpperUp_R, UnifiedExpressions.MouthUpperUpRight);
            SetParam(pShape, BlendShape.Index.MouthLowerDown_L, UnifiedExpressions.MouthLowerDownLeft);
            SetParam(pShape, BlendShape.Index.MouthLowerDown_R, UnifiedExpressions.MouthLowerDownRight);
            SetParam(pShape, BlendShape.Index.MouthFrown_L, UnifiedExpressions.MouthFrownLeft);
            SetParam(pShape, BlendShape.Index.MouthFrown_R, UnifiedExpressions.MouthFrownRight);
            SetParam(pShape, BlendShape.Index.MouthDimple_L, UnifiedExpressions.MouthDimpleLeft);
            SetParam(pShape, BlendShape.Index.MouthDimple_R, UnifiedExpressions.MouthDimpleRight);
            SetParam(pShape, BlendShape.Index.MouthLeft, UnifiedExpressions.MouthUpperLeft);
            SetParam(pShape, BlendShape.Index.MouthLeft, UnifiedExpressions.MouthLowerLeft);
            SetParam(pShape, BlendShape.Index.MouthRight, UnifiedExpressions.MouthUpperRight);
            SetParam(pShape, BlendShape.Index.MouthRight, UnifiedExpressions.MouthLowerRight);
            SetParam(pShape, BlendShape.Index.MouthPress_L, UnifiedExpressions.MouthPressLeft);
            SetParam(pShape, BlendShape.Index.MouthPress_R, UnifiedExpressions.MouthPressRight);
            SetParam(pShape, BlendShape.Index.MouthShrugLower, UnifiedExpressions.MouthRaiserLower);
            SetParam(pShape, BlendShape.Index.MouthShrugUpper, UnifiedExpressions.MouthRaiserUpper);
            SetParam(pShape, BlendShape.Index.MouthSmile_L, UnifiedExpressions.MouthCornerPullLeft);
            SetParam(pShape, BlendShape.Index.MouthSmile_L, UnifiedExpressions.MouthCornerSlantLeft);
            SetParam(pShape, BlendShape.Index.MouthSmile_R, UnifiedExpressions.MouthCornerPullRight);
            SetParam(pShape, BlendShape.Index.MouthSmile_R, UnifiedExpressions.MouthCornerSlantRight);
            SetParam(pShape, BlendShape.Index.MouthStretch_L, UnifiedExpressions.MouthStretchLeft);
            SetParam(pShape, BlendShape.Index.MouthStretch_R, UnifiedExpressions.MouthStretchRight);
            #endregion

            #region Lip
            SetParam(pShape, BlendShape.Index.MouthFunnel, UnifiedExpressions.LipFunnelUpperLeft);
            SetParam(pShape, BlendShape.Index.MouthFunnel, UnifiedExpressions.LipFunnelUpperRight);
            SetParam(pShape, BlendShape.Index.MouthFunnel, UnifiedExpressions.LipFunnelLowerLeft);
            SetParam(pShape, BlendShape.Index.MouthFunnel, UnifiedExpressions.LipFunnelLowerRight);
            SetParam(pShape, BlendShape.Index.MouthPucker, UnifiedExpressions.LipPuckerUpperLeft);
            SetParam(pShape, BlendShape.Index.MouthPucker, UnifiedExpressions.LipPuckerUpperRight);
            SetParam(pShape, BlendShape.Index.MouthPucker, UnifiedExpressions.LipPuckerLowerLeft);
            SetParam(pShape, BlendShape.Index.MouthPucker, UnifiedExpressions.LipPuckerLowerRight);
            SetParam(pShape, BlendShape.Index.MouthRollUpper, UnifiedExpressions.LipSuckUpperLeft);
            SetParam(pShape, BlendShape.Index.MouthRollUpper, UnifiedExpressions.LipSuckUpperRight);
            SetParam(pShape, BlendShape.Index.MouthRollLower, UnifiedExpressions.LipSuckLowerLeft);
            SetParam(pShape, BlendShape.Index.MouthRollLower, UnifiedExpressions.LipSuckLowerRight);
            #endregion

            #region Tongue
            SetParam(pShape, BlendShape.Index.TongueOut, UnifiedExpressions.TongueOut);
            #endregion
        }

        private float SmoothValue(float newValue, ref float lastValue)
        {
            lastValue += (newValue - lastValue) * SmoothingFactor;
            return lastValue;
        }

        private static void SetParam(float[] pShape, BlendShape.Index index, UnifiedExpressions outputType)
        {
            UnifiedTracking.Data.Shapes[(int)outputType].Weight = pShape[(int)index];
        }

        private static void SetParam(float param, UnifiedExpressions outputType)
        {
            UnifiedTracking.Data.Shapes[(int)outputType].Weight = param;
        }
    }
}