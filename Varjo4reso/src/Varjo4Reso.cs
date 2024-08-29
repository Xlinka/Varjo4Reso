using Elements.Core;
using FrooxEngine;
using ResoniteModLoader;
using System;

namespace Varjo4Reso
{
    public class VarjoEyeIntegration : ResoniteMod
    {
        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<bool> useLegacyBlinkDetection = new ModConfigurationKey<bool>("using_blink_detection", "Use Legacy Blink Detection", () => false);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<bool> blinkSmoothing = new ModConfigurationKey<bool>("using_blink_smoothing", "Use Blink Smoothing", () => true);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<bool> useLegacyPupilDilation = new ModConfigurationKey<bool>("use_Legacy_Pupil_Dilation", "Use Legacy Pupil Dilation", () => false);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> userPupilScale = new ModConfigurationKey<float>("pupil_Dilaiton_Scale", "Pupil Dilation Scale. Used to correct legacy Varjo Companion readings", () => 0.008f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> blinkSpeed = new ModConfigurationKey<float>("blink_Speed", "Blink Speed", () => 10.0f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> middleStateSpeedMultiplier = new ModConfigurationKey<float>("middle_State_Speed_Multiplier", "Middle State Speed Multiplier", () => 0.025f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> blinkDetectionMultiplier = new ModConfigurationKey<float>("blink_Detection_Multiplier", "Blink Detection Multiplier", () => 2.0f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> fullOpenState = new ModConfigurationKey<float>("full_Open_State", "Fully Open State", () => 1.0f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> halfOpenState = new ModConfigurationKey<float>("half_Open_State", "Half Open State", () => 0.5f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> quarterOpenState = new ModConfigurationKey<float>("quarter_Open_State", "Quarter Open State", () => 0.25f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> closedState = new ModConfigurationKey<float>("closed_State", "Eye Closed State", () => 0.0f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> minPupilSize = new ModConfigurationKey<float>("min_Pupil_Size", "Minimum Pupil Size", () => 0.003f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> gazeSensitivity = new ModConfigurationKey<float>("gaze_sensitivity", "Gaze Sensitivity", () => 1.0f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> gazeSmoothing = new ModConfigurationKey<float>("gaze_smoothing", "Gaze Smoothing", () => 0.5f);

        [AutoRegisterConfigKey]
        public readonly static ModConfigurationKey<float> pupilSizeMultiplier = new ModConfigurationKey<float>("pupil_size_multiplier", "Pupil Size Multiplier", () => 1.0f);

        public static ModConfiguration config;
        public static VarjoNativeInterface tracker;

        public override string Name => "VarjoEyeIntegration";
        public override string Author => "Xlinka";
        public override string Version => "1.0.1";
        public override string Link => "https://github.com/Xlinka/Varjo4Reso";

        public override void OnEngineInit()
        {
            UniLog.Log("Initializing Varjo Eye Integration mod...");
            config = GetConfiguration();
            tracker = new VarjoNativeInterface();

            ResoniteMod.Msg("Initializing the Varjo module");

            if (!tracker.Initialize())
            {
                Error("Varjo eye tracking will be unavailable for this session.");
                UniLog.Log("Varjo module initialization failed.");
                return;
            }

            UniLog.Log("Varjo module initialized successfully.");

            Engine.Current.OnReady += () =>
            {
                Engine.Current.InputInterface.RegisterInputDriver(new VarjoEyeInputDevice());
                UniLog.Log("Varjo Eye Input Device registered.");
            };

            Engine.Current.OnShutdown += () =>
            {
                tracker.Teardown();
                UniLog.Log("Varjo Eye Integration mod shutdown.");
            };

            ResoniteMod.Msg("Varjo module initialized successfully.");
        }

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
                UniLog.Log("Collecting Varjo Eye Tracking device info...");
                var dataTreeDictionary = new DataTreeDictionary();
                dataTreeDictionary.Add("Name", "Varjo Eye Tracking");
                dataTreeDictionary.Add("Type", "Eye Tracking");
                dataTreeDictionary.Add("Model", "Varjo HMD");
                list.Add(dataTreeDictionary);
                UniLog.Log("Device info collected and added to the list.");
            }

            public void RegisterInputs(InputInterface inputInterface)
            {
                UniLog.Log("Registering Varjo Eye Inputs...");
                eyes = new Eyes(inputInterface, "Varjo Eye Tracking", true);
                UniLog.Log("Varjo Eye Inputs registered successfully.");
            }

            public void UpdateInputs(float deltaTime)
            {
                UniLog.Log("Updating Varjo Eye Inputs...");
                tracker.Update();
                var gazeData = tracker.GetGazeData();
                var eyeData = tracker.GetEyeMeasurements();

                UniLog.Log("Processing pupil sizes...");
                var leftPupil = config.GetValue(useLegacyPupilDilation) ?
                    (float)(gazeData.leftPupilSize * config.GetValue(userPupilScale)) :
                    eyeData.leftPupilDiameterInMM * 0.001f;
                var rightPupil = config.GetValue(useLegacyPupilDilation) ?
                    (float)(gazeData.rightPupilSize * config.GetValue(userPupilScale)) :
                    eyeData.rightPupilDiameterInMM * 0.001f;

                UniLog.Log("Processing blink detection...");
                if (config.GetValue(useLegacyBlinkDetection))
                {
                    var leftOpen = DetermineEyeOpenness(gazeData.leftStatus);
                    var rightOpen = DetermineEyeOpenness(gazeData.rightStatus);

                    ApplyBlinkSmoothing(gazeData, deltaTime);

                    _leftOpen = MathX.Lerp(_leftOpen, leftOpen, deltaTime * config.GetValue(blinkSpeed) * _leftEyeBlinkMultiplier);
                    _rightOpen = MathX.Lerp(_rightOpen, rightOpen, deltaTime * config.GetValue(blinkSpeed) * _rightEyeBlinkMultiplier);
                }
                else
                {
                    _leftOpen = eyeData.leftEyeOpenness;
                    _rightOpen = eyeData.rightEyeOpenness;
                }

                bool leftStatus = IsEyeStatusValid(gazeData.leftStatus);
                bool rightStatus = IsEyeStatusValid(gazeData.rightStatus);

                eyes.IsEyeTrackingActive = Engine.Current.InputInterface.VR_Active;

                UpdateEye(gazeData.leftEye, leftStatus, leftPupil, _leftOpen, deltaTime, eyes.LeftEye);
                UpdateEye(gazeData.rightEye, rightStatus, rightPupil, _rightOpen, deltaTime, eyes.RightEye);

                bool combinedStatus = gazeData.status == GazeStatus.Valid;
                float combinedPupil = MathX.Average(leftPupil, rightPupil);
                float combinedOpen = MathX.Average(eyes.LeftEye.Openness, eyes.RightEye.Openness);

                UpdateEye(gazeData.gaze, combinedStatus, combinedPupil, combinedOpen, deltaTime, eyes.CombinedEye);

                eyes.ComputeCombinedEyeParameters();

                if (gazeData.stability > 0.75)
                    eyes.ConvergenceDistance = (float)gazeData.focusDistance;

                eyes.Timestamp = gazeData.frameNumber / 100;
                eyes.FinishUpdate();

                UniLog.Log("Varjo Eye Inputs updated successfully.");
            }

            private float DetermineEyeOpenness(GazeEyeStatus eyeStatus)
            {
                UniLog.Log($"Determining eye openness for status: {eyeStatus}");
                return eyeStatus switch
                {
                    GazeEyeStatus.Tracked => config.GetValue(fullOpenState),
                    GazeEyeStatus.Compensated => config.GetValue(halfOpenState),
                    GazeEyeStatus.Visible => config.GetValue(quarterOpenState),
                    _ => config.GetValue(closedState),
                };
            }

            private void ApplyBlinkSmoothing(GazeData gazeData, float deltaTime)
            {
                UniLog.Log("Applying blink smoothing...");
                if (gazeData.leftStatus == GazeEyeStatus.Invalid)
                {
                    _leftEyeBlinkMultiplier += config.GetValue(blinkDetectionMultiplier);
                    UniLog.Log("Left eye blink multiplier increased due to invalid status.");
                }
                else if (gazeData.leftStatus == GazeEyeStatus.Compensated || gazeData.leftStatus == GazeEyeStatus.Visible)
                {
                    _leftEyeBlinkMultiplier *= config.GetValue(middleStateSpeedMultiplier);
                    _leftEyeBlinkMultiplier = MathX.Max(1.0f, _leftEyeBlinkMultiplier);
                    UniLog.Log("Left eye blink multiplier adjusted due to compensated/visible status.");
                }
                else
                {
                    _leftEyeBlinkMultiplier = 1.0f;
                    UniLog.Log("Left eye blink multiplier reset to 1.0.");
                }

                if (gazeData.rightStatus == GazeEyeStatus.Invalid)
                {
                    _rightEyeBlinkMultiplier += config.GetValue(blinkDetectionMultiplier);
                    UniLog.Log("Right eye blink multiplier increased due to invalid status.");
                }
                else if (gazeData.rightStatus == GazeEyeStatus.Compensated || gazeData.rightStatus == GazeEyeStatus.Visible)
                {
                    _rightEyeBlinkMultiplier *= config.GetValue(middleStateSpeedMultiplier);
                    _rightEyeBlinkMultiplier = MathX.Max(1.0f, _rightEyeBlinkMultiplier);
                    UniLog.Log("Right eye blink multiplier adjusted due to compensated/visible status.");
                }
                else
                {
                    _rightEyeBlinkMultiplier = 1.0f;
                    UniLog.Log("Right eye blink multiplier reset to 1.0.");
                }

                _previouslyClosedLeft = gazeData.leftStatus == GazeEyeStatus.Invalid;
                _previouslyClosedRight = gazeData.rightStatus == GazeEyeStatus.Invalid;

                UniLog.Log("Blink smoothing applied successfully.");
            }

            private bool IsEyeStatusValid(GazeEyeStatus status)
            {
                UniLog.Log($"Checking if eye status is valid: {status}");
                return status == GazeEyeStatus.Compensated ||
                       status == GazeEyeStatus.Tracked ||
                       status == GazeEyeStatus.Visible;
            }

            private void UpdateEye(in GazeRay data, in bool status, in float pupilSize, in float openness, in float deltaTime, Eye eye)
            {
                UniLog.Log("Updating eye data...");
                eye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
                eye.IsTracking = status;

                if (eye.IsTracking)
                {
                    UniLog.Log("Eye is being tracked. Updating direction and position...");
                    var direction = (float3)new double3(data.forward.x, data.forward.y, data.forward.z).Normalized;
                    eye.UpdateWithDirection(direction);

                    eye.RawPosition = (float3)new double3(data.origin.x, data.origin.y, data.origin.z);

                    eye.PupilDiameter = MathX.Clamp(pupilSize, config.GetValue(minPupilSize), float.MaxValue);
                }

                eye.Openness = openness;
                eye.Widen = (float)MathX.Clamp01(data.forward.y);
                eye.Squeeze = 0f;
                eye.Frown = 0f;

                UniLog.Log("Eye data updated successfully.");
            }
        }
    }
}
