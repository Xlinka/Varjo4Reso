using Elements.Core;
using FrooxEngine;
using ResoniteModLoader;
using System.Runtime.CompilerServices;

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
        public readonly static ModConfigurationKey<float> userPupilScale = new ModConfigurationKey<float>("pupil_Dilaiton_Scale", "Pupil Dilation Scale", () => 0.008f);

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

        public static ModConfiguration config;
        public static VarjoNativeInterface tracker;  

        public override string Name => "VarjoEyeIntegration";
        public override string Author => "Xlinka";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/Xlinka/Varjo4Reso";

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            tracker = new VarjoNativeInterface();

            UniLog.Log($"Initializing the Varjo module");

            if (!tracker.Initialize())
            {
                Error("Varjo eye tracking will be unavailable for this session.");
                return;
            }

            Engine.Current.OnReady += () =>
                Engine.Current.InputInterface.RegisterInputDriver(new VarjoEyeInputDevice());
            Engine.Current.OnShutdown += () => tracker.Teardown();
        }
    }
}