using System.Collections.Generic;
using UnityEngine;

namespace DroneSimulator.Settings
{
    [System.Serializable]
    public class GraphicsSettings
    {
        public bool fullscreen = true;
        public int windowMode = 0;
        public int resolutionIndex = -1;
        public bool vSync = true;
        public int fpsLimit = 0;
        public float renderScale = 1f;
        public int upscaleFilter = 0;
        public int preset = 3;
        public int qualityLevel = 5;
        public bool separateMissionSettings = false;

        public int bunkerShadowQuality = 2;
        public float bunkerShadowDistance = 20f;
        public int bunkerShadowResolution = 2048;
        public bool bunkerSoftShadows = true;
        public int bunkerAdditionalLights = 1;
        public int bunkerMaxLights = 4;
        public float bunkerFarClip = 80f;
        public float bunkerLodBias = 1f;
        public int bunkerMsaa = 1;
        public int bunkerAniso = 1;
        public int bunkerTextureQuality = 0;
        public bool bunkerPostProcessing = true;
        public bool bunkerMotionBlur = false;

        public float droneFov = 100f;
        public int droneDefaultZoomIndex = 0;
        public float droneZoomSpeed = 10f;
        public float droneFarClip = 2000f;
        public float droneShadowDistance = 300f;
        public float droneLodBias = 1.5f;
        public bool droneFog = true;
        public float droneFogDensity = 0.01f;
        public int ewPostFxQuality = 1;
        public float ewVisualDistortion = 1f;
        public int ewNoiseResolution = 128;
        public int nightVisionColor = 0;
        public float nightVisionGain = 1f;
        public int droneAntiAliasing = 0;
        public bool droneLensDistortion = true;
    }

    [System.Serializable]
    public class AudioSettings
    {
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public float motorsVolume = 1f;
        public float ewVolume = 1f;
        public float uiVolume = 1f;
        public float voiceVolume = 1f;
        public bool duckOnMenus = true;
        public bool monoMode = false;
        public bool muteOnSignalLoss = false;
    }

    [System.Serializable]
    public class KeyBinding
    {
        public string actionId;
        public string bindingJson;
    }

    [System.Serializable]
    public class ControlSettings
    {
        public float fpsMouseSensitivity = 1.67f;
        public float droneMouseSensitivity = 1.67f;
        public bool fpsInvertY = false;
        public bool droneInvertY = false;
        public bool gimbalInvertY = false;
        public float droneStickSpeed = 4f;
        public float droneCenteringSpeed = 8f;
        public float gimbalSpeed = 45f;
        public float throttleSpeed = 1.8f;
        public int throttleMode = 1;
        public float stickDeadZone = 0f;
        public float zoomMouseScale = 0.5f;
        public float verticalClampMin = -70f;
        public float verticalClampMax = 90f;
        public List<KeyBinding> keyBindings = new List<KeyBinding>();
    }

    [System.Serializable]
    public class GameplaySettings
    {
        public float hudTransparency = 1f;
        public float hudScale = 1f;
        public float crosshairSize = 14f;
        public int crosshairColorIndex = 0;
        public Color customCrosshairColor = new Color(0.2f, 0.9f, 0.3f, 1f);
        public bool crosshairOutline = true;
        public int hudLineThickness = 2;
        public bool showTargetFrame = true;
        public int osdLevel = 1;
        public bool subtitleBackground = true;
        public int subtitleFontSize = 20;
        public bool subtitlesEnabled = true;
        public bool showInteractionHints = true;
        public bool showFpsCounter = false;
        public int language = 0;
        public float cameraShakeIntensity = 1f;
        public bool throttleShake = true;
        public bool gimbalAutoCenter = false;
        public float gimbalAutoCenterDelay = 1.5f;
        public bool ewMotionComfort = false;
        public bool reduceDangerousEffects = false;
        public bool trainingHints = true;
        public bool showSubtitlesOnPause = true;
    }

    [System.Serializable]
    public class SettingsData
    {
        public int version = 2;
        public GraphicsSettings graphics = new GraphicsSettings();
        public AudioSettings audio = new AudioSettings();
        public ControlSettings controls = new ControlSettings();
        public GameplaySettings gameplay = new GameplaySettings();

        public float masterVolume { get => audio.masterVolume; set => audio.masterVolume = value; }
        public float sfxVolume { get => audio.sfxVolume; set => audio.sfxVolume = value; }
        public int qualityLevel { get => graphics.qualityLevel; set => graphics.qualityLevel = value; }
        public bool fullscreen { get => graphics.fullscreen; set => graphics.fullscreen = value; }
    }
}
