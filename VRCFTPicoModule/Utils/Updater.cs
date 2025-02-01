using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFTPicoModule.Data;
using static VRCFTPicoModule.Utils.Localization;

namespace VRCFTPicoModule.Utils
{
    public class Updater()
    {
        private readonly UdpClient? _udpClient;
        private readonly ILogger? _logger;
        private readonly bool _isLegacy;
        private readonly (bool, bool) _trackingAvailable;

        public Updater(UdpClient udpClient, ILogger logger, bool isLegacy, (bool, bool) trackingAvailable) : this()
        {
            _udpClient = udpClient;
            _logger = logger;
            _isLegacy = isLegacy;
            _trackingAvailable = trackingAvailable;
        }
        
        private int _timeOut;
        private float _lastMouthLeft;
        private float _lastMouthRight;
        private const float SmoothingFactor = 0.5f;
        private ModuleState _moduleState;

        public void Update(ModuleState state)
        {
            if (_udpClient == null)
                return;
            
            if (_logger == null)
                return;
            
            _udpClient.Client.ReceiveTimeout = 100;
            _moduleState = state;
            
            if (_moduleState != ModuleState.Active) return;

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = _udpClient.Receive(ref endPoint);
                var pShape = ParseData(data, _isLegacy);
                
                if (_trackingAvailable.Item1)
                    UpdateEye(pShape);
                
                if (_trackingAvailable.Item2)
                    UpdateExpression(pShape);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                if (++_timeOut > 600)
                {
                    _logger.LogWarning(T("update-timeout"));
                    _timeOut = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(T("update-failed"), ex);
            }
        }

        private static float[] ParseData(byte[] data, bool isLegacy)
        {
            if (isLegacy && data.Length >= Marshal.SizeOf<LegacyDataPacket.DataPackBody>())
                return DataPacketHelpers.ByteArrayToStructure<LegacyDataPacket.DataPackBody>(data).blendShapeWeight;

            if (data.Length <
                Marshal.SizeOf<DataPacket.DataPackHeader>() + Marshal.SizeOf<DataPacket.DataPackBody>()) return [];
            var header = DataPacketHelpers.ByteArrayToStructure<DataPacket.DataPackHeader>(data);
            return header.trackingType == 2 ? DataPacketHelpers.ByteArrayToStructure<DataPacket.DataPackBody>(data, Marshal.SizeOf<DataPacket.DataPackHeader>()).blendShapeWeight : [];
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
        }

        private void UpdateExpression(float[] pShape)
        {
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
            const float diffThreshold = 0.1f;

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

            var mouthFrownLeft = pShape[(int)BlendShape.Index.MouthFrown_L];
            SetParam(pShape[(int)BlendShape.Index.JawOpen] > 0.1f
                    ? mouthFrownLeft / 2f
                    : pShape[(int)BlendShape.Index.MouthRollLower] > 0.2f 
                        ? mouthFrownLeft * 2.5f + pShape[(int)BlendShape.Index.MouthRollLower]
                        : mouthFrownLeft,
                UnifiedExpressions.MouthFrownLeft);

            var mouthFrownRight = pShape[(int)BlendShape.Index.MouthFrown_R];
            SetParam(pShape[(int)BlendShape.Index.JawOpen] > 0.1f
                    ? mouthFrownRight / 2f
                    : pShape[(int)BlendShape.Index.MouthRollLower] > 0.2f
                        ? mouthFrownRight * 2.5f + pShape[(int)BlendShape.Index.MouthRollLower]
                        : mouthFrownRight,
                UnifiedExpressions.MouthFrownRight);
            
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

            var mouthSmileLeft = pShape[(int)BlendShape.Index.MouthSmile_L] -
                                 pShape[(int)BlendShape.Index.MouthRollLower];
            SetParam(pShape[(int)BlendShape.Index.MouthRollLower] < 0.2f
                    ? mouthSmileLeft
                    : 0f,
                UnifiedExpressions.MouthCornerPullLeft);
            SetParam(pShape[(int)BlendShape.Index.MouthRollLower] < 0.2f
                    ? mouthSmileLeft - pShape[(int)BlendShape.Index.MouthRollLower]
                    : 0f,
                UnifiedExpressions.MouthCornerSlantLeft);

            var mouthSmileRight = pShape[(int)BlendShape.Index.MouthSmile_R] -
                                  pShape[(int)BlendShape.Index.MouthRollLower];
            SetParam(pShape[(int)BlendShape.Index.MouthRollLower] < 0.2f
                    ? mouthSmileRight
                    : 0f,
                UnifiedExpressions.MouthCornerPullRight);
            SetParam(pShape[(int)BlendShape.Index.MouthRollLower] < 0.2f
                    ? mouthSmileRight
                    : 0f,
                UnifiedExpressions.MouthCornerSlantRight);
            
            SetParam(pShape, BlendShape.Index.MouthStretch_L, UnifiedExpressions.MouthStretchLeft);
            SetParam(pShape, BlendShape.Index.MouthStretch_R, UnifiedExpressions.MouthStretchRight);
            #endregion

            #region Lip
            var isFunnelLeft = pShape[(int)BlendShape.Index.MouthPucker] > 0.3f &&
                               pShape[(int)BlendShape.Index.MouthPress_L] < 0.2f;
            var isFunnelRight = pShape[(int)BlendShape.Index.MouthPucker] > 0.3f &&
                               pShape[(int)BlendShape.Index.MouthPress_R] < 0.2f;
            var mouthFunnelFixed = pShape[(int)BlendShape.Index.MouthPucker];
            SetParam(isFunnelLeft
                    ? mouthFunnelFixed
                    : pShape[(int)BlendShape.Index.MouthFunnel],
                UnifiedExpressions.LipFunnelUpperLeft);
            SetParam(isFunnelRight
                    ? mouthFunnelFixed
                    : pShape[(int)BlendShape.Index.MouthFunnel],
                UnifiedExpressions.LipFunnelUpperRight);
            SetParam(isFunnelLeft
                    ? mouthFunnelFixed
                    : pShape[(int)BlendShape.Index.MouthFunnel],
                UnifiedExpressions.LipFunnelLowerLeft);
            SetParam(isFunnelRight
                    ? mouthFunnelFixed
                    : pShape[(int)BlendShape.Index.MouthFunnel],
                UnifiedExpressions.LipFunnelLowerRight);
            
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

        private static float SmoothValue(float newValue, ref float lastValue)
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