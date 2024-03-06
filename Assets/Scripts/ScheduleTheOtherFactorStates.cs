using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[Serializable]
public class FactorState
{
    #region State Management Related
    public bool RestartEngine;
    public string Name;
    #endregion
    #region Inspector Variables
    public bool DisplayOculusHands;
    #region Particles
    public int ParticlesPerHand;
    public Vector2 ParticleSizeMinMax;
    public float DistanceForMinSize;
    public float SizeLerp;
    public Material ParticleMaterial;
    #endregion
    #region Attraction
    public float AttractionStrength;
    public float DistForMinAtt;
    public Vector2 AttByDistRange;
    public Vector4 AttMirrorGroups;
    #endregion
    #region Velocity
    public float VelocityLerp;
    #endregion
    #region Attraction Scaling Per Group/Hand
    public Vector4 ParticlesAttractionGroup1;
    public Vector4 ParticlesAttractionGroup2;
    public Vector4 ParticlesAttractionGroup3;
    public Vector4 ParticlesAttractionGroup4;
    #endregion
    #region Per Particle Scaling
    public Vector2 PerParticleScalingMinMax;
    public float PerParticleScalingExponent;
    public string PerParticleScalingGroups;
    #endregion
    #region Position Offsets
    public Vector2 PositionOffsetMinMax;
    public bool AttToReplay;
    public float MirrorDistance;
    #endregion
    #region Index Step Size
    public Vector2 IndexStepSizeMinMax;
    #endregion
    #region Color
    public bool UseDebugColors;
    public Gradient BaseColorTimeGradient;
    public float ColorTimeGradientUpdateSpeed;
    public float ColorLerp;
    public float HeartbeatSpeed;
    public bool UseHeartbeat;
    public Vector2 AlphaMinMax;
    #endregion
    #region Stretch
    public Vector2 StretchFactorMinMax = new Vector2(0f, 1f);
    public float StretchFactorExponent = 1f;
    public float StretchMax = 1f;
    #endregion
    #endregion
}

[ExecuteInEditMode]
public class ScheduleTheOtherFactorStates : MonoBehaviour
{
    #region Variables
    #region Schedule
    private TheOtherFactor tof;
    public List<FactorState> presets = new List<FactorState>();
    public List<string> presetNamesForReference = new List<string>();
    [SerializeField]
    private int currentPresetIndex = 0;
    [SerializeField]
    private string currentPresetName;
    #endregion
    #region Utility
    [Header("Utility")]
    public bool PinchToSwitchPresets = false;
    public bool DisplayCanvas = true;
    public AudioClip switchSound; // Sound to play when switching presets
    private Transform canvas;
    private CanvasMover canvasMover;
    private TextMeshProUGUI presetDisplayText; // Reference to the TMP component
    private AudioSource audioSource;
    [HideInInspector]
    public bool tofIsRunningJobs = false;
    #endregion
    #region Preset Variables
    [Header("Preset Variables")]
    public bool DisplayOculusHands = false;
    #region Particles
    [Header("Particles")]
    [Tooltip("Restart of the runtime jobs (button in the inspector) required to apply a change here while in play mode.")]
    public int ParticlesPerHand = 35000;
    [Tooltip("The min and max values for aprticle size, max reached at 0 distance from joint, min reached at DistanceForMinSize.")]
    public Vector2 ParticleSizeMinMax = new Vector2(.003f, .008f);
    [Tooltip("The distance between particle and joint at which the particle size reaches ParticleSizeMinMax.x")]
    public float DistanceForMinSize = .008f;
    [Tooltip("The linear interpolation factor for size change in one update.")]
    public float SizeLerp = .05f;
    [Tooltip("Restart of the runtime jobs (button in the inspector) required to apply a change here while in play mode.")]
    public Material ParticleMaterial;
    #endregion
    #region Attraction
    [Header("Attraction")]
    public float AttractionStrength = 1f;
    public float DistForMinAtt = 1f;
    public Vector2 AttByDistRange = new Vector2(0f, 1f);
    public Vector4 AttMirrorGroups = Vector4.one;
    #endregion
    #region Velocity
    [Header("Velocity")]
    [Tooltip("The linear interpolation factor for the velocity change in one update step.")]
    public float VelocityLerp = .1f;
    #endregion
    #region Attraction Scaling Per Group/Hand
    [Header("Attraction Scaling Per Group/Hand")]
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Green when using debug color in attraciton job.")]
    public Vector4 ParticlesAttractionGroup1 = Vector4.one;
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Red when using debug color in attraciton job.")]
    public Vector4 ParticlesAttractionGroup2 = Vector4.one;
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Green when using debug color in attraciton job.")]
    public Vector4 ParticlesAttractionGroup3 = Vector4.one;
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Red when using debug color in attraciton job.")]
    public Vector4 ParticlesAttractionGroup4 = Vector4.one;
    #region Per Particle Scaling
    [Header("Per Particle Scaling")]
    public Vector2 PerParticleScalingMinMax = new Vector2(0f, 1f);
    public float PerParticleScalingExponent = .1f;
    public string PerParticleScalingGroups = "xyzw";
    #endregion
    #endregion
    #region Position Offsets
    // should not really be here because it belongs to positions job, but looks better in inspector. need create custom inspector
    [Header("Position Offsets")]
    [Tooltip("Determines the min and max interpolation between the relative positions on the mesh and the joint. 0 = full mesh, 1 = full joint")]
    public Vector2 PositionOffsetMinMax = new Vector2(0f, .7f);
    #region Joint Mirror
    [Header("Joint Mirror")]
    public bool AttToReplay = false;
    public float MirrorDistance = .5f; // Distance from the camera to the virtual mirror plane
    #endregion
    #region Stretch
    [Header("Stretch")]
    public Vector2 StretchFactorMinMax = new Vector2(0f, 1f);
    public float StretchFactorExponent = 1f;
    public float StretchMax = 1f;
    #endregion
    #endregion
    #region Index Step Size
    [Header("Index Step Size")]
    [Tooltip("The min and max size step a particle can take in the array that holds all mesh positions. 0-0 = particles stick to one position. 1-1 = particles always progress 1 position each update. 0-2 = particles might stay in place, move ahead one position or 2 positions in one update.")]
    public Vector2 IndexStepSizeMinMax = new Vector2(0, 2);
    #endregion
    #region Color
    [Header("Color")]
    public bool UseDebugColors = false;
    public Gradient BaseColorTimeGradient;
    public float ColorTimeGradientUpdateSpeed = 3.14f;
    [Tooltip("The linear interpolation factor for color change in one opdate step.")]
    public float ColorLerp = .05f;
    public float HeartbeatSpeed = 1f;
    public bool UseHeartbeat = true;
    public Vector2 AlphaMinMax = new Vector2(.2f, .7f);
    #endregion
    #endregion
    #endregion
    private void UpdateTOF()
    {
        tof.DisplayOculusHands = DisplayOculusHands;
        #region Particles
        tof.ParticlesPerHand = ParticlesPerHand;
        tof.ParticleSizeMinMax = ParticleSizeMinMax;
        tof.DistanceForMinSize = DistanceForMinSize;
        tof.SizeLerp = SizeLerp;
        tof.ParticleMaterial = ParticleMaterial;
        #endregion
        #region Attraction Strength
        tof.AttractionStrength = AttractionStrength;
        tof.DistForMinAtt = DistForMinAtt;
        tof.AttByDistRange = AttByDistRange;
        tof.AttMirrorGroups = AttMirrorGroups;
        #endregion
        #region Velocity
        tof.VelocityLerp = VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        tof.ParticlesAttractionGroup1 = ParticlesAttractionGroup1;
        tof.ParticlesAttractionGroup2 = ParticlesAttractionGroup2;
        tof.ParticlesAttractionGroup3 = ParticlesAttractionGroup3;
        tof.ParticlesAttractionGroup4 = ParticlesAttractionGroup4;
        #region Per Particle Scaling
        tof.PerParticleScalingMinMax = PerParticleScalingMinMax;
        tof.PerParticleScalingExponent = PerParticleScalingExponent;
        tof.PerParticleScalingGroups = PerParticleScalingGroups;
        #endregion
        #endregion
        #region Position Offsets
        tof.PositionOffsetMinMax = PositionOffsetMinMax;
        #region Joint Mirror
        tof.AttractToReplay = AttToReplay;
        tof.MirrorDistance = MirrorDistance;
        #endregion
        #region Stretch
        tof.StretchFactorMinMax = StretchFactorMinMax;
        tof.StretchFactorExponent = StretchFactorExponent;
        tof.StretchMax = StretchMax;
        #endregion
        #endregion
        #region Index Step Size
        tof.IndexStepSizeMinMax = IndexStepSizeMinMax;
        #endregion
        #region Color
        tof.UseDebugColors = UseDebugColors;
        tof.BaseColorTimeGradient = BaseColorTimeGradient;
        tof.ColorTimeGradientUpdateSpeed = ColorTimeGradientUpdateSpeed;
        tof.ColorLerp = ColorLerp;
        tof.HeartbeatSpeed = HeartbeatSpeed;
        tof.UseHeartbeat = UseHeartbeat;
        tof.AlphaMinMax = AlphaMinMax;
        #endregion
    }
    public void SavePreset(string name)
    {
        bool foundExistingPresetWithSameName = false;

        FactorState newState = new FactorState
        {
            #region State Manage Related
            Name = name,
            #endregion
            DisplayOculusHands = DisplayOculusHands,
            #region Particles
            ParticlesPerHand = ParticlesPerHand,
            ParticleSizeMinMax = ParticleSizeMinMax,
            DistanceForMinSize = DistanceForMinSize,
            SizeLerp = SizeLerp,
            ParticleMaterial = ParticleMaterial,
            #endregion
            #region Attraction
            AttractionStrength = AttractionStrength,
            DistForMinAtt = DistForMinAtt,
            AttByDistRange = AttByDistRange,
            #endregion
            #region Velocity
            VelocityLerp = VelocityLerp,
            #endregion
            #region Attraction Scaling Per Group/Hand
            ParticlesAttractionGroup1 = ParticlesAttractionGroup1,
            ParticlesAttractionGroup2 = ParticlesAttractionGroup2,
            ParticlesAttractionGroup3 = ParticlesAttractionGroup3,
            ParticlesAttractionGroup4 = ParticlesAttractionGroup4,
            #endregion
            #region Per Particle Scaling
            PerParticleScalingMinMax = PerParticleScalingMinMax,
            PerParticleScalingExponent = PerParticleScalingExponent,
            PerParticleScalingGroups = PerParticleScalingGroups,
            #endregion
            #region Position Offsets
            PositionOffsetMinMax = PositionOffsetMinMax,
            #region Joint Mirror
            AttToReplay = AttToReplay,
            MirrorDistance = MirrorDistance,
            #endregion
            #region Stretch
            StretchFactorMinMax = StretchFactorMinMax,
            StretchFactorExponent = StretchFactorExponent,
            StretchMax = StretchMax,
            #endregion
            #endregion
            #region Index Step Size
            IndexStepSizeMinMax = IndexStepSizeMinMax,
            #endregion
            #region Color
            UseDebugColors = UseDebugColors,
            BaseColorTimeGradient = BaseColorTimeGradient,
            ColorTimeGradientUpdateSpeed = ColorTimeGradientUpdateSpeed,
            ColorLerp = ColorLerp,
            HeartbeatSpeed = HeartbeatSpeed,
            UseHeartbeat = UseHeartbeat,
            AlphaMinMax = AlphaMinMax,
            #endregion

        };
        for (int i = 0; i < presets.Count; i++)
        {
            if (presets[i].Name == name)
            {
                presets[i] = newState;
                foundExistingPresetWithSameName = true;
                break;
            }
        }
        if (!foundExistingPresetWithSameName)
        {
            presets.Add(newState);
            presetNamesForReference.Add(newState.Name);
        }
    }
    private void ApplyPreset(FactorState state)
    {
        #region Particles
        tof.ParticlesPerHand = state.ParticlesPerHand;
        ParticlesPerHand = state.ParticlesPerHand;
        #endregion
        #region Per Particle Scaling
        tof.PerParticleScalingMinMax = state.PerParticleScalingMinMax;
        PerParticleScalingMinMax = state.PerParticleScalingMinMax;
        tof.PerParticleScalingExponent = state.PerParticleScalingExponent;
        PerParticleScalingExponent = state.PerParticleScalingExponent;
        #endregion
        #region Position Offsets
        tof.PositionOffsetMinMax = state.PositionOffsetMinMax;
        PositionOffsetMinMax = state.PositionOffsetMinMax;
        #endregion
        #region Index Step Size
        tof.IndexStepSizeMinMax = state.IndexStepSizeMinMax;
        IndexStepSizeMinMax = state.IndexStepSizeMinMax;
        #endregion
        tof.AttractToReplay = state.AttToReplay;
        AttToReplay = state.AttToReplay;
        tof.DisplayOculusHands = state.DisplayOculusHands;
        DisplayOculusHands = state.DisplayOculusHands;
        #region Particles
        tof.ParticleSizeMinMax = state.ParticleSizeMinMax;
        ParticleSizeMinMax = state.ParticleSizeMinMax;
        tof.DistanceForMinSize = state.DistanceForMinSize;
        DistanceForMinSize = state.DistanceForMinSize;
        tof.SizeLerp = state.SizeLerp;
        SizeLerp = state.SizeLerp;
        tof.ParticleMaterial = state.ParticleMaterial;
        ParticleMaterial = state.ParticleMaterial;
        #endregion
        #region Attraction Strength
        tof.AttractionStrength = state.AttractionStrength;
        AttractionStrength = state.AttractionStrength;
        tof.DistForMinAtt = state.DistForMinAtt;
        DistForMinAtt = state.DistForMinAtt;
        tof.AttByDistRange = state.AttByDistRange;
        AttByDistRange = state.AttByDistRange;
        tof.AttMirrorGroups = state.AttMirrorGroups;
        AttMirrorGroups = state.AttMirrorGroups;
        #endregion
        #region Velocity
        tof.VelocityLerp = state.VelocityLerp;
        VelocityLerp = state.VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        tof.ParticlesAttractionGroup1 = state.ParticlesAttractionGroup1;
        ParticlesAttractionGroup1 = state.ParticlesAttractionGroup1;
        tof.ParticlesAttractionGroup2 = state.ParticlesAttractionGroup2;
        ParticlesAttractionGroup2 = state.ParticlesAttractionGroup2;
        tof.ParticlesAttractionGroup3 = state.ParticlesAttractionGroup3;
        ParticlesAttractionGroup3 = state.ParticlesAttractionGroup3;
        tof.ParticlesAttractionGroup4 = state.ParticlesAttractionGroup4;
        ParticlesAttractionGroup4 = state.ParticlesAttractionGroup4;
        #region Per Particle Scaling
        tof.PerParticleScalingGroups = state.PerParticleScalingGroups;
        PerParticleScalingGroups = state.PerParticleScalingGroups;
        #endregion
        #endregion
        #region Position Offsets
        #region Joint Mirror
        tof.AttractToReplay = state.AttToReplay;
        AttToReplay = state.AttToReplay;
        tof.MirrorDistance = state.MirrorDistance;
        MirrorDistance = state.MirrorDistance;
        #endregion
        #region Stretch
        tof.StretchFactorMinMax = state.StretchFactorMinMax;
        tof.StretchFactorExponent = state.StretchFactorExponent;
        StretchFactorMinMax = state.StretchFactorMinMax;
        StretchFactorExponent = state.StretchFactorExponent;
        tof.StretchMax = state.StretchMax;
        StretchMax = state.StretchMax;
        #endregion
        #endregion
        #region Color
        tof.UseDebugColors = state.UseDebugColors;
        UseDebugColors = state.UseDebugColors;
        tof.BaseColorTimeGradient = state.BaseColorTimeGradient;
        BaseColorTimeGradient = state.BaseColorTimeGradient;
        tof.ColorTimeGradientUpdateSpeed = state.ColorTimeGradientUpdateSpeed;
        ColorTimeGradientUpdateSpeed = state.ColorTimeGradientUpdateSpeed;
        tof.ColorLerp = state.ColorLerp;
        ColorLerp = state.ColorLerp;
        tof.HeartbeatSpeed = state.HeartbeatSpeed;
        HeartbeatSpeed = state.HeartbeatSpeed;
        tof.UseHeartbeat = state.UseHeartbeat;
        UseHeartbeat = state.UseHeartbeat;
        tof.AlphaMinMax = state.AlphaMinMax; AlphaMinMax = state.AlphaMinMax;
        #endregion

        currentPresetName = state.Name;

        canvasMover.SetCanvasPosition();
        if (DisplayCanvas) presetDisplayText.text = FormatVariablesForDisplay();
    }
    #region Functions without the need to touch when variables change
    void Start()
    {
        tof = GetComponent<TheOtherFactor>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        canvas = GameObject.Find("PresetDisplayCanvas").transform;
        canvasMover = canvas.GetComponent<CanvasMover>();
        presetDisplayText = canvas.GetComponentInChildren<TextMeshProUGUI>();
        canvasMover.SetCanvasPosition();
        presetDisplayText.text = currentPresetName;
    }
    private void Update()
    {
        UpdateTOF();
        UpdatePresetNameList();

        if (DisplayCanvas) presetDisplayText.text = FormatVariablesForDisplay();
        else presetDisplayText.text = "";

        if (PinchToSwitchPresets)
        {
            // Check for a pinch on the left hand
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
            {
                PreviousPreset();
            }

            // Check for a pinch on the right hand
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                NextPreset();
                PlaySwitchSound();
            }
        }
    }
    public void ApplyPresetByNameOrIndex(string name = null, int index = -1)
    {
        FactorState selectedPreset = null;

        if (!string.IsNullOrEmpty(name))
        {
            // Find preset by name
            selectedPreset = presets.FirstOrDefault(p => p.Name == name);
        }
        else if (index >= 0 && index < presets.Count)
        {
            // Find preset by index
            selectedPreset = presets[index];
        }

        if (selectedPreset != null)
        {
            ApplyPreset(selectedPreset);
            currentPresetIndex = presets.IndexOf(selectedPreset);
        }
        else
        {
            Debug.LogWarning("Preset not found.");
        }
    }
    public void UpdatePresetNameList()
    {
        if (presetNamesForReference.Count != presets.Count)
        {
            presetNamesForReference.Clear();
            foreach (var preset in presets)
            {
                presetNamesForReference.Add(preset.Name);
            }
        }
        else
        {
            for (int i = 0; i < presets.Count; i++)
            {
                presetNamesForReference[i] = presets[i].Name;
            }
        }
    }
    public void NextPreset()
    {
        currentPresetIndex = (currentPresetIndex + 1) % presets.Count;
        ApplyPreset(presets[currentPresetIndex]);
        PlaySwitchSound();
    }
    public void PreviousPreset()
    {
        if (currentPresetIndex == 0) currentPresetIndex = presets.Count;
        currentPresetIndex--;
        ApplyPreset(presets[currentPresetIndex]);
        PlaySwitchSound();
    }
    private void PlaySwitchSound()
    {
        if (switchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(switchSound);
        }
    }
    public void StartTOF()
    {
        tof.StartTheOtherFactor();
        tofIsRunningJobs = true;
    }
    public void StopTOF()
    {
        tof.StopTheOtherFactor();
        tofIsRunningJobs = false;
    }
    string FormatVariablesForDisplay()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"Current Preset Index: {currentPresetIndex}");
        sb.AppendLine($"Current Preset Name: {currentPresetName}");

        // Particles Section
        sb.AppendLine("\nParticles:");
        sb.AppendLine($"Particles Per Hand: {ParticlesPerHand}");
        sb.AppendLine($"Particle Size Min/Max: ({ParticleSizeMinMax.x}, {ParticleSizeMinMax.y})");
        sb.AppendLine($"Distance For Min Size: {DistanceForMinSize}");
        sb.AppendLine($"Size Lerp: {SizeLerp}");
        sb.AppendLine($"Particle Material: {(ParticleMaterial != null ? ParticleMaterial.name : "None")}");

        // Attraction Section
        sb.AppendLine("\nAttraction:");
        sb.AppendLine($"Attraction Strength: {AttractionStrength}");

        // Velocity Section
        sb.AppendLine("\nVelocity:");
        sb.AppendLine($"Velocity Lerp: {VelocityLerp}");

        // Attraction Scaling Per Group/Hand Section
        sb.AppendLine("\nAttraction Scaling Per Group/Hand:");
        sb.AppendLine($"Particles Attraction Group 1: {ParticlesAttractionGroup1}");
        sb.AppendLine($"Particles Attraction Group 2: {ParticlesAttractionGroup2}");
        sb.AppendLine($"Particles Attraction Group 3: {ParticlesAttractionGroup3}");
        sb.AppendLine($"Particles Attraction Group 4: {ParticlesAttractionGroup4}");

        // Per Particle Scaling Section
        sb.AppendLine("\nPer Particle Scaling:");
        sb.AppendLine($"Per Particle Scaling Min/Max: ({PerParticleScalingMinMax.x}, {PerParticleScalingMinMax.y})");
        sb.AppendLine($"Per Particle Scaling Exponent: {PerParticleScalingExponent}");
        sb.AppendLine($"Per Particle Scaling Groups: {PerParticleScalingGroups}");

        // Position Offsets Section
        sb.AppendLine("\nPosition Offsets:");
        sb.AppendLine($"Position Offset Min/Max: ({PositionOffsetMinMax.x}, {PositionOffsetMinMax.y})");

        // Joint Mirror Section
        sb.AppendLine("\nJoint Mirror:");
        sb.AppendLine($"Att To Replay: {AttToReplay}");
        sb.AppendLine($"Mirror Distance: {MirrorDistance}");

        // Stretch Section
        sb.AppendLine("\nStretch:");
        sb.AppendLine($"Stretch Factor Min/Max: ({StretchFactorMinMax.x}, {StretchFactorMinMax.y})");
        sb.AppendLine($"Stretch Factor Exponent: {StretchFactorExponent}");
        sb.AppendLine($"Stretch Max: {StretchMax}");

        // Index Step Size Section
        sb.AppendLine("\nIndex Step Size:");
        sb.AppendLine($"Index Step Size Min/Max: ({IndexStepSizeMinMax.x}, {IndexStepSizeMinMax.y})");

        // Color Section
        sb.AppendLine("\nColor:");
        sb.AppendLine($"Use Debug Colors: {UseDebugColors}");
        sb.AppendLine($"Color Time Gradient Update Speed: {ColorTimeGradientUpdateSpeed}");
        sb.AppendLine($"Color Lerp: {ColorLerp}");
        sb.AppendLine($"Heartbeat Speed: {HeartbeatSpeed}");
        sb.AppendLine($"Use Heartbeat: {UseHeartbeat}");
        sb.AppendLine($"Alpha Min/Max: ({AlphaMinMax.x}, {AlphaMinMax.y})");

        return sb.ToString();
    }
    #endregion
}
