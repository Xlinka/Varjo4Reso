using Elements.Core;
using FrooxEngine;

namespace Varjo4Reso
{
    public class VarjoEyeInputDevice : IInputDriver
    {
        public Eyes eyes;
        public int UpdateOrder => 100;

        private float _leftOpen = 1.0f;
        private float _rightOpen = 1.0f;

        private bool _previouslyClosedRight = false;
        private bool _previouslyClosedLeft = false;

        private float _leftEyeBlinkMultiplier = 1.0f;
        private float _rightEyeBlinkMultiplier = 1.0f;

        public void CollectDeviceInfos(DataTreeList list)
        {
            var dataTreeDictionary = new DataTreeDictionary();
            dataTreeDictionary.Add("Name", "Varjo Eye Tracking");
            dataTreeDictionary.Add("Type", "Eye Tracking");
            dataTreeDictionary.Add("Model", "Varjo HMD");
            list.Add(dataTreeDictionary);
        }

        public void RegisterInputs(InputInterface inputInterface)
        {
            eyes = new Eyes(inputInterface, "Varjo Eye Tracking", true);
        }

        public void UpdateInputs(float deltaTime)
        {
            VarjoNativeInterface tracker = VarjoEyeIntegration.tracker;
            tracker.Update();
            var gazeData = tracker.GetGazeData();
            var eyeData = tracker.GetEyeMeasurements();

            var gazeSmoothing = VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.gazeSmoothing);
            var gazeSensitivity = VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.gazeSensitivity);
            var pupilDilationSpeed = VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.pupilDilationSpeed);
            var pupilSizeMultiplier = VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.pupilSizeMultiplier);

            var leftPupil = VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.useLegacyPupilDilation) ?
                (float)(gazeData.leftPupilSize * VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.userPupilScale) * pupilSizeMultiplier) :
                eyeData.leftPupilDiameterInMM * 0.001f * pupilSizeMultiplier;

            var rightPupil = VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.useLegacyPupilDilation) ?
                (float)(gazeData.rightPupilSize * VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.userPupilScale) * pupilSizeMultiplier) :
                eyeData.rightPupilDiameterInMM * 0.001f * pupilSizeMultiplier;

            if (VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.useLegacyBlinkDetection))
            {
                var leftOpen =
                    gazeData.leftStatus == GazeEyeStatus.Tracked ? VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.fullOpenState) : (
                    gazeData.leftStatus == GazeEyeStatus.Compensated ? VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.halfOpenState) : (
                    gazeData.leftStatus == GazeEyeStatus.Visible ? VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.quarterOpenState)
                    : VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.closedState)));

                var rightOpen =
                    gazeData.rightStatus == GazeEyeStatus.Tracked ? VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.fullOpenState) : (
                    gazeData.rightStatus == GazeEyeStatus.Compensated ? VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.halfOpenState) : (
                    gazeData.rightStatus == GazeEyeStatus.Visible ? VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.quarterOpenState)
                    : VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.closedState)));

                if (VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.blinkSmoothing))
                {
                    if (_previouslyClosedLeft == true && gazeData.leftStatus == GazeEyeStatus.Invalid)
                    {
                        _leftEyeBlinkMultiplier += VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.blinkDetectionMultiplier);
                    }
                    else if (gazeData.leftStatus == GazeEyeStatus.Compensated || gazeData.leftStatus == GazeEyeStatus.Visible)
                    {
                        _leftEyeBlinkMultiplier *= VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.middleStateSpeedMultiplier);
                        _leftEyeBlinkMultiplier = MathX.Max(1.0f, _leftEyeBlinkMultiplier);
                    }
                    else
                    {
                        _leftEyeBlinkMultiplier = 1.0f;
                    }

                    if (_previouslyClosedRight == true && gazeData.rightStatus == GazeEyeStatus.Invalid)
                    {
                        _rightEyeBlinkMultiplier += VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.blinkDetectionMultiplier);
                    }
                    else if (gazeData.rightStatus == GazeEyeStatus.Compensated || gazeData.rightStatus == GazeEyeStatus.Visible)
                    {
                        _rightEyeBlinkMultiplier *= VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.middleStateSpeedMultiplier);
                        _rightEyeBlinkMultiplier = MathX.Max(1.0f, _rightEyeBlinkMultiplier);
                    }
                    else
                    {
                        _rightEyeBlinkMultiplier = 1.0f;
                    }

                    _previouslyClosedLeft = gazeData.leftStatus == GazeEyeStatus.Invalid;
                    _previouslyClosedRight = gazeData.rightStatus == GazeEyeStatus.Invalid;
                }

                _leftOpen = MathX.Lerp(_leftOpen, leftOpen, deltaTime * VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.blinkSpeed) * _leftEyeBlinkMultiplier);
                _rightOpen = MathX.Lerp(_rightOpen, rightOpen, deltaTime * VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.blinkSpeed) * _rightEyeBlinkMultiplier);
            }
            else
            {
                _leftOpen = eyeData.leftEyeOpenness;
                _rightOpen = eyeData.rightEyeOpenness;
            }

            bool leftStatus = gazeData.leftStatus == GazeEyeStatus.Compensated ||
                              gazeData.leftStatus == GazeEyeStatus.Tracked ||
                              gazeData.leftStatus == GazeEyeStatus.Visible;

            bool rightStatus = gazeData.rightStatus == GazeEyeStatus.Compensated ||
                               gazeData.rightStatus == GazeEyeStatus.Tracked ||
                               gazeData.rightStatus == GazeEyeStatus.Visible;

            eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;

            UpdateEye(in gazeData.leftEye, in leftStatus, in leftPupil, _leftOpen, deltaTime, eyes.LeftEye, gazeSensitivity, gazeSmoothing);
            UpdateEye(in gazeData.rightEye, in rightStatus, in rightPupil, _rightOpen, deltaTime, eyes.RightEye, gazeSensitivity, gazeSmoothing);

            bool combinedStatus = gazeData.status == GazeStatus.Valid;
            float combinedPupil = MathX.Average(leftPupil, rightPupil);
            float combinedOpen = MathX.Average(eyes.LeftEye.Openness, eyes.RightEye.Openness);

            UpdateEye(in gazeData.gaze, in combinedStatus, in combinedPupil, in combinedOpen, in deltaTime, eyes.CombinedEye, gazeSensitivity, gazeSmoothing);

            eyes.ComputeCombinedEyeParameters();

            if (gazeData.stability > 0.75)
                eyes.ConvergenceDistance = (float)gazeData.focusDistance;

            eyes.Timestamp = gazeData.frameNumber / 100;

            eyes.FinishUpdate();
        }

        private void UpdateEye(in GazeRay data, in bool status, in float pupilSize, in float openness, in float deltaTime, Eye eye, float gazeSensitivity, float gazeSmoothing)
        {
            eye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
            eye.IsTracking = status;

            if (eye.IsTracking)
            {
                var direction = (float3)new double3(data.forward.x,
                    data.forward.y,
                    data.forward.z).Normalized;

                // Apply gaze sensitivity and smoothing
                direction *= gazeSensitivity;
                direction = MathX.Lerp(eye.Direction, direction, gazeSmoothing * deltaTime);

                eye.UpdateWithDirection(direction);

                eye.RawPosition = (float3)new double3(data.origin.x,
                    data.origin.y,
                    data.origin.z);

                eye.PupilDiameter = MathX.Clamp(pupilSize, VarjoEyeIntegration.config.GetValue(VarjoEyeIntegration.minPupilSize), float.MaxValue);
            }

            eye.Openness = openness;
            eye.Widen = (float)MathX.Clamp01(data.forward.y);
            eye.Squeeze = 0f;
            eye.Frown = 0f;
        }
    }
}
