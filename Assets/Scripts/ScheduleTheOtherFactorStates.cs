using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[Serializable]
public class FactorState
{
    #region State Management Related
    public string Name;
    #endregion
    #region Inspector Variables
    #region Attraction
    public float AttractionStrength;
    public float VelocityLerp;
    public float DistForMinAtt;
    public float MinAtt;
    public float AttToOgHands;
    public float AttToPlaneMirror;
    public float AttToPointMirror;
    public float AttToReplay;
    public float AttToTorus;
    public float AttToTorusMirror;
    public Vector2 HandDistRange;
    public Vector2 HandSpeedRange;
    #endregion
    #region Particle Group Bias
    public Vector2 HandBiasRange;
    public int HandBiasRangeExp;
    public Vector4 AttGroup1;
    public int HandBiasG1;
    public Vector4 AttGroup2;
    public int HandBiasG2;
    public Vector4 AttGroup3;
    public int HandBiasG3;
    public Vector4 AttGroup4;
    public int HandBiasG4;
    #endregion
    #region Positions
    public Vector2 IndexStepSizeRange;
    public Vector2 PosOffsetRange;
    [HideInInspector]
    public Vector2 StretchFactorRange = new Vector2(0f, 1f);
    [HideInInspector]
    public int StretchFactorExponent = 2;
    [HideInInspector]
    public float StretchMax = 1f;
    public float MirrorDistance;
    #endregion
    #region Torus
    [Header("Torus")]
    public Vector2 TorusRadiusRange;
    public float TorusMajorWraps;
    #endregion
    #region Particles
    public int ParticlesPerHand;
    public Vector2 ParticleSizeRange;
    public float DistanceForMinSize;
    public float SizeLerp;
    public Material ParticleMaterial;
    #endregion
    #region Color
    public bool UseDebugColors;
    public Gradient BaseColorTimeGradient;
    public float ColorTimeGradientUpdateSpeed;
    public float ColorLerp;
    public float Alpha;
    public bool DisplayOculusHands;
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
    private int currentPresetIndex = 0;
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
    #region Attraction
    [Header("Attraction")]
    [Tooltip("The general attraction strength used for all sources of attraction. It get scaled by the distance to the attraction source.")]
    [Range(0f, 1f)]
    public float AttractionStrength = 1f;
    [Tooltip("The linear interpolation factor for the velocity change in one update step.")]
    [Range(0.05f, 1f)]
    public float VelocityLerp = .1f;
    [Tooltip("The distance between a particle and its attraction source affects the attraction strength for this copmutation. If the distance is 0 the strength is 1, and for distance < DistForMinAtt it gets scaled between 1 and MinAtt, reaching MinAtt at distance == DistForMinAtt.")]
    [Range (0f, 1f)]
    public float DistForMinAtt = 1f;
    [Tooltip("The distance between a particle and its attraction source affects the attraction strength for this copmutation. If the distance is 0 the strength is 1, and for distance < DistForMinAtt it gets scaled between 1 and MinAtt, reaching MinAtt at distance == DistForMinAtt.")]
    [Range(0,1f)]
    public float MinAtt = .33f;
    [Tooltip("A float value that scales the attraction of all particles to the hands of the user.")]
    [Range(0, 1f)]
    public float AttToOgHands = 1f;
    [Tooltip("A float value that scales the attraction of all particles to the hands of the user, but the attraction sources are mirrored on a plane compared to the OG hands.")]
    [Range(0, 1f)]
    public float AttToPlaneMirror = 0f;
    [Tooltip("A float value that scales the attraction of all particles to the hands of the user, but the attraction sources are mirrored on a point compared to the OG hands. The mirror point is an object in the scene that can be moved around.")]
    [Range(0, 1f)]
    public float AttToPointMirror = 0f;
    [Tooltip("A float value that scales the attraction of all particles to the hands of the replay (could be replaced by another user).")]
    [Range(0, 1f)]
    public float AttToReplay = 0f;
    [Tooltip("A float value that scales the attraction of all particles to the torus.")]
    [Range(0, 1f)]
    public float AttToTorus = 0f;
    [Tooltip("A float value that scales the attraction of all particles to the point mirrored (mirrorpoint1) torus torus.")]
    [Range(0, 1f)]
    public float AttToTorusMirror = 0f;
    [Tooltip("The attraction gets scaled with the speed at which the hands are moving. The speed is calculated from the distance between hand positions in each update. These values determine at which distances we reach the min and max speed values.")]
    public Vector2 HandDistRange = Vector2.zero;
    [Tooltip("The attraction gets scaled with the speed at which the hands are moving. The speed is calculated from the distance between hand positions in each update. These values determine the min and max speed values reached at min and max distance moved between updates.")]
    public Vector2 HandSpeedRange = Vector2.zero;
    #endregion
    #region Particle Group Bias
    [Header("Particle Group Bias")]
    [Tooltip("Every group of particles has a preferred hand and the attraction to all other hands gets scaled down. It does get scaled down within the HandBiasRange, but not all to the same degree, rather there is a distribution of these scaling values that get randomly generated and then multiplied with themselves HandBiasRangeExp times.")]
    public Vector2 HandBiasRange = new Vector2(0f, 1f);
    [Tooltip("Every group of particles has a preferred hand and the attraction to all other hands gets scaled down. It does get scaled down within the HandBiasRange, but not all to the same degree, rather there is a distribution of these scaling values that get randomly generated and then multiplied with themselves HandBiasRangeExp times.")]
    [Range(1,4)]
    public int HandBiasRangeExp = 2;
    
    [Tooltip("x,y,z,w value determine the attraction for each particle in that group towards the OG lefthand, OG right hand, Replay left hand and Replay right hand respectively. Green when using debug color in attraciton job.")]  
    public Vector4 AttGroup1 = Vector4.one;
    [Range(0, 3)]
    [Tooltip("This value indicates which of the 4 hands should be the preference of this particle group. This hands attraction will not be affected by the values in the distribution based on HandBiasRange, so it will always be scaled with 1 whereas all other hands get scaled with a distribution of values HandBiasRange self multiplied HandBiasRangeExp times.")]
    public int HandBiasG1 = 0;
    
    [Tooltip("x,y,z,w value determine the attraction for each particle in that group towards the OG lefthand, OG right hand, Replay left hand and Replay right hand respectively. Red when using debug color in attraciton job.")]
    public Vector4 AttGroup2 = Vector4.one;
    [Range(0, 3)]
    [Tooltip("This value indicates which of the 4 hands should be the preference of this particle group. This hands attraction will not be affected by the values in the distribution based on HandBiasRange, so it will always be scaled with 1 whereas all other hands get scaled with a distribution of values HandBiasRange self multiplied HandBiasRangeExp times.")]
    public int HandBiasG2 = 0;
   
    [Tooltip("x,y,z,w value determine the attraction for each particle in that group towards the OG lefthand, OG right hand, Replay left hand and Replay right hand respectively. Blue when using debug color in attraciton job.")]
    public Vector4 AttGroup3 = Vector4.one;
    [Range(0, 3)]
    [Tooltip("This value indicates which of the 4 hands should be the preference of this particle group. This hands attraction will not be affected by the values in the distribution based on HandBiasRange, so it will always be scaled with 1 whereas all other hands get scaled with a distribution of values HandBiasRange self multiplied HandBiasRangeExp times.")]
    public int HandBiasG3 = 1;
    
    [Tooltip("x,y,z,w value determine the attraction for each particle in that group towards the OG lefthand, OG right hand, Replay left hand and Replay right hand respectively. Yellow when using debug color in attraciton job.")]
    public Vector4 AttGroup4 = Vector4.one;
    [Range(0, 3)]
    [Tooltip("This value indicates which of the 4 hands should be the preference of this particle group. This hands attraction will not be affected by the values in the distribution based on HandBiasRange, so it will always be scaled with 1 whereas all other hands get scaled with a distribution of values HandBiasRange self multiplied HandBiasRangeExp times.")]
    public int HandBiasG4 = 1;
    #endregion
    #region Positions
    [Header("Positions")]
    [Tooltip("The min and max size step a particle can take in the array that holds all mesh positions. 0-0 = particles stick to one position. 1-1 = particles always progress 1 position each update. 0-2 = particles might stay in place, move ahead one position or 2 positions in one update.")]
    public Vector2 IndexStepSizeRange = new Vector2(0, 2);
    [Tooltip("Determines the min and max interpolation between the positions on the mesh and the closest joint. 0 = full mesh, 1 = full joint")]
    public Vector2 PosOffsetRange = new Vector2(0f, .7f);
    [HideInInspector]
    [Tooltip("The min and max amount a particles stretch value can increase in one update step. These values fill an array and each particle keeps its value, so ome will stretch faster then others.")]
    public Vector2 StretchFactorRange = new Vector2(0f, 0f);
    [HideInInspector]
    [Range(0, 4)]
    [Tooltip("Determines how often the StretchFactorRange values get multiplied with themselves to create a gradient distribution for the stretch.")]
    public int StretchFactorExponent = 2;
    // the stop condition of stretch needs to be revisited
    [HideInInspector]
    public float StretchMax = 1f;
    [Range(0f, 1f)]
    [Tooltip("The distance from the headset in the forward direction at activation which the mirror positions will get set.")]
    public float MirrorDistance = .5f; 
    #endregion
    #region Torus
    [Header("Torus")]
    [Tooltip("The minor and major radius of the torus. The major radius is the distance from the center of the torus to the center of the tube, while the minor radius is the radius of the tube itself.")]
    public Vector2 TorusRadiusRange = new Vector2(.0000001f, 1f);
    [Tooltip("Determines how often the string of particles is supposed to wrap around the major radius of torus.")]
    public float TorusMajorWraps = 1.0f;
    [Tooltip("The min and max values for the torus particles color value, which can be used to influence attraction of TheOtheFactor particles.")]
    private SnakingTorusParticles STP;
    #endregion
    #region Particles
    [Header("Particles")]
    [Tooltip("Restart of the runtime jobs (button in the inspector) required to apply a change here while in play mode.")]
    public int ParticlesPerHand = 35000;
    [Tooltip("The min and max values for particle size, max reached at 0 distance from joint, min reached at DistanceForMinSize.")]
    public Vector2 ParticleSizeRange = new Vector2(.003f, .008f);
    [Tooltip("The distance between particle and its current mesh position at which the particle size reaches ParticleSizeRange.x")]
    [Range(0,1f)]
    public float DistanceForMinSize = .008f;
    [Tooltip("The linear interpolation factor for size change in one update.")]
    [Range(0.05f,1f)]
    public float SizeLerp = .05f;
    public Material ParticleMaterial;
    #endregion
    #region Color
    [Header("Color")]
    [Tooltip("If true, the 4 particle groups will get individual colors for debugging purposes.")]
    public bool UseDebugColors = false;
    [Tooltip("The color for each particle is computed by linearly interpolating from the current base color towards the coloring based on velocity and the alpha component of the current base color determines the linear interpolation amount.")]
    public Gradient BaseColorTimeGradient;
    [Tooltip("This determines how quickly the base color changes based on the color gradient.")]
    public float ColorTimeGradientUpdateSpeed = 3.14f;
    [Tooltip("The linear interpolation factor for color change in one opdate step.")]
    [Range(0.05f,1f)]
    public float ColorLerp = .05f;
    [Tooltip("The alpha component of TheOtherFactor particles color.")]
    public float Alpha = .7f;
    [Tooltip("Whether or not to show the default oculus hands. Mostly for debugging.")]
    public bool DisplayOculusHands = false;
    #endregion
    #endregion
    #endregion
    private void UpdateTOF()
    {
        tof.DisplayOculusHands = DisplayOculusHands;
        #region Attraction
        tof.AttractionStrength = AttractionStrength;
        tof.VelocityLerp = VelocityLerp;
        tof.DistForMinAtt = DistForMinAtt;
        tof.MinAtt = MinAtt;
        tof.AttToOgHands = AttToOgHands;
        tof.AttToPlaneMirror = AttToPlaneMirror;
        tof.AttToPointMirror = AttToPointMirror;
        tof.AttToReplay = AttToReplay;
        tof.AttToTorus = AttToTorus;
        tof.AttToTorusMirror = AttToTorusMirror;
        tof.HandDistRange = HandDistRange;
        tof.HandSpeedRange = HandSpeedRange;
        #endregion
        #region Particle Group Bias
        tof.HandBiasRange = HandBiasRange;
        tof.HandBiasRangeExp = HandBiasRangeExp;
        tof.AttGroup1 = AttGroup1;
        tof.HandBiasG1 = HandBiasG1;
        tof.AttGroup2 = AttGroup2;
        tof.HandBiasG2 = HandBiasG2;
        tof.AttGroup3 = AttGroup3;
        tof.HandBiasG3 = HandBiasG3;
        tof.AttGroup4 = AttGroup4;
        tof.HandBiasG4 = HandBiasG4;
        #endregion
        #region Positions
        tof.IndexStepSizeRange = IndexStepSizeRange;
        tof.PosOffsetRange = PosOffsetRange;
        tof.MirrorDistance = MirrorDistance;
        tof.StretchFactorRange = StretchFactorRange;
        tof.StretchFactorExponent = StretchFactorExponent;
        tof.StretchMax = StretchMax;
        #endregion
        #region Particles
        tof.ParticlesPerHand = ParticlesPerHand;
        tof.ParticleSizeRange = ParticleSizeRange;
        tof.DistanceForMinSize = DistanceForMinSize;
        tof.SizeLerp = SizeLerp;
        tof.ParticleMaterial = ParticleMaterial;
        #endregion
        #region Color
        tof.UseDebugColors = UseDebugColors;
        tof.BaseColorTimeGradient = BaseColorTimeGradient;
        tof.ColorTimeGradientUpdateSpeed = ColorTimeGradientUpdateSpeed;
        tof.ColorLerp = ColorLerp;
        tof.Alpha = Alpha;
        #endregion
    }
    private void UpdateTorus()
    {
        STP.RadiusRange = TorusRadiusRange;
        STP.MajorWraps = TorusMajorWraps;
    }
    public void SavePreset(string name)
    {
        bool foundExistingPresetWithSameName = false;

        FactorState newState = new FactorState
        {
            #region State Manage Related
            Name = name,
            #endregion
            #region Attraction
            AttractionStrength = AttractionStrength,
            VelocityLerp = VelocityLerp,
            DistForMinAtt = DistForMinAtt,
            MinAtt = MinAtt,
            AttToOgHands = AttToOgHands,
            AttToPlaneMirror = AttToPlaneMirror,
            AttToPointMirror = AttToPointMirror,
            AttToReplay = AttToReplay,
            AttToTorus = AttToTorus,
            AttToTorusMirror = AttToTorusMirror,
            HandDistRange = HandDistRange,
            HandSpeedRange = HandSpeedRange,
            #endregion
            #region Particle Group Bias
            HandBiasRange = HandBiasRange,
            HandBiasRangeExp = HandBiasRangeExp,
            AttGroup1 = AttGroup1,
            HandBiasG1 = HandBiasG1,
            AttGroup2 = AttGroup2,
            HandBiasG2 = HandBiasG2,
            AttGroup3 = AttGroup3,
            HandBiasG3 = HandBiasG3,
            AttGroup4 = AttGroup4,
            HandBiasG4 = HandBiasG4,
            #endregion
            #region Positions
            IndexStepSizeRange = IndexStepSizeRange,
            PosOffsetRange = PosOffsetRange,
            MirrorDistance = MirrorDistance,
            StretchFactorRange = StretchFactorRange,
            StretchFactorExponent = StretchFactorExponent,
            StretchMax = StretchMax,
            #endregion
            #region Torus
            TorusRadiusRange = TorusRadiusRange,
            TorusMajorWraps = TorusMajorWraps,
            #endregion
            #region Particles
            ParticlesPerHand = ParticlesPerHand,
            ParticleSizeRange = ParticleSizeRange,
            DistanceForMinSize = DistanceForMinSize,
            SizeLerp = SizeLerp,
            ParticleMaterial = ParticleMaterial,
            #endregion
            #region Color
            UseDebugColors = UseDebugColors,
            BaseColorTimeGradient = BaseColorTimeGradient,
            ColorTimeGradientUpdateSpeed = ColorTimeGradientUpdateSpeed,
            ColorLerp = ColorLerp,
            Alpha = Alpha,
            DisplayOculusHands = DisplayOculusHands,
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
        }
    }
    private void ApplyPreset(FactorState state)
    {
        #region Attraction
        tof.AttractionStrength = state.AttractionStrength;
        AttractionStrength = state.AttractionStrength; 

        tof.VelocityLerp = state.VelocityLerp;
        VelocityLerp = state.VelocityLerp;

        tof.DistForMinAtt = state.DistForMinAtt;
        DistForMinAtt = state.DistForMinAtt;

        tof.MinAtt = state.MinAtt;
        MinAtt = state.MinAtt;

        tof.AttToOgHands = state.AttToOgHands;
        AttToOgHands = state.AttToOgHands;

        tof.AttToPlaneMirror = state.AttToPlaneMirror;
        AttToPlaneMirror = state.AttToPlaneMirror;

        tof.AttToPointMirror = state.AttToPointMirror;
        AttToPointMirror = state.AttToPointMirror;

        tof.AttToReplay = state.AttToReplay;
        AttToReplay = state.AttToReplay;

        tof.AttToTorus = state.AttToTorus;
        AttToTorus = state.AttToTorus;

        tof.AttToTorusMirror = state.AttToTorusMirror;
        AttToTorusMirror = state.AttToTorusMirror;

        tof.HandDistRange = state.HandDistRange;
        HandDistRange = state.HandDistRange;

        tof.HandSpeedRange = state.HandSpeedRange;
        HandSpeedRange = state.HandSpeedRange;
        #endregion
        #region Particle Group Bias
        tof.HandBiasRange = state.HandBiasRange;
        HandBiasRange = state.HandBiasRange;

        tof.HandBiasRangeExp = state.HandBiasRangeExp;
        HandBiasRangeExp = state.HandBiasRangeExp;

        tof.AttGroup1 = state.AttGroup1;
        AttGroup1 = state.AttGroup1;

        tof.HandBiasG1 = HandBiasG1;
        HandBiasG1 = state.HandBiasG1;

        tof.AttGroup2 = state.AttGroup2;
        AttGroup2 = state.AttGroup2;

        tof.HandBiasG2 = HandBiasG2;
        HandBiasG2 = state.HandBiasG2;

        tof.AttGroup3 = state.AttGroup3;
        AttGroup3 = state.AttGroup3;

        tof.HandBiasG3 = HandBiasG3;
        HandBiasG3 = state.HandBiasG3;

        tof.AttGroup4 = state.AttGroup4;
        AttGroup4 = state.AttGroup4;

        tof.HandBiasG4 = HandBiasG4;
        HandBiasG4 = state.HandBiasG4;
        #endregion
        #region Positions
        tof.IndexStepSizeRange = state.IndexStepSizeRange;
        IndexStepSizeRange = state.IndexStepSizeRange;

        tof.PosOffsetRange = state.PosOffsetRange;
        PosOffsetRange = state.PosOffsetRange;

        tof.MirrorDistance = state.MirrorDistance;
        MirrorDistance = state.MirrorDistance;

        tof.StretchFactorRange = state.StretchFactorRange;
        StretchFactorRange = state.StretchFactorRange;

        tof.StretchFactorExponent = state.StretchFactorExponent;
        StretchFactorExponent = state.StretchFactorExponent;

        tof.StretchMax = state.StretchMax;
        StretchMax = state.StretchMax;
        #endregion
        #region Torus
        STP.RadiusRange = state.TorusRadiusRange;
        TorusRadiusRange = state.TorusRadiusRange;

        STP.MajorWraps = state.TorusMajorWraps;
        TorusMajorWraps = state.TorusMajorWraps;
        #endregion
        #region Particles
        tof.ParticlesPerHand = state.ParticlesPerHand;
        ParticlesPerHand = state.ParticlesPerHand;

        tof.ParticleSizeRange = state.ParticleSizeRange;
        ParticleSizeRange = state.ParticleSizeRange;

        tof.DistanceForMinSize = state.DistanceForMinSize;
        DistanceForMinSize = state.DistanceForMinSize;

        tof.SizeLerp = state.SizeLerp;
        SizeLerp = state.SizeLerp;

        tof.ParticleMaterial = state.ParticleMaterial;
        ParticleMaterial = state.ParticleMaterial;
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

        tof.Alpha = state.Alpha;
        Alpha = state.Alpha;

        tof.DisplayOculusHands = state.DisplayOculusHands;
        DisplayOculusHands = state.DisplayOculusHands;
        #endregion

        canvasMover.SetCanvasPosition();
        if (DisplayCanvas) presetDisplayText.text = FormatVariablesForDisplay();
    }
    #region Functions without the need to touch when variables change
    void Start()
    {
        tof = GetComponent<TheOtherFactor>();
        STP = GameObject.Find("Torus").GetComponent<SnakingTorusParticles>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        canvas = GameObject.Find("PresetDisplayCanvas").transform;
        canvasMover = canvas.GetComponent<CanvasMover>();
        presetDisplayText = canvas.GetComponentInChildren<TextMeshProUGUI>();
        canvasMover.SetCanvasPosition();
    }
    private void Update()
    {
        UpdateTOF();
        UpdateTorus();

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
    //needs revisiting
    string FormatVariablesForDisplay()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine($"Current Preset Index: {currentPresetIndex}");

        // Particles Section
        sb.AppendLine("\nParticles:");
        sb.AppendLine($"Particles Per Hand: {ParticlesPerHand}");
        sb.AppendLine($"Particle Size Min/Max: ({ParticleSizeRange.x}, {ParticleSizeRange.y})");
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
        sb.AppendLine($"Particles Attraction Group 1: {AttGroup1}");
        sb.AppendLine($"Particles Attraction Group 2: {AttGroup2}");
        sb.AppendLine($"Particles Attraction Group 3: {AttGroup3}");
        sb.AppendLine($"Particles Attraction Group 4: {AttGroup4}");

        // Per Particle Scaling Section
        sb.AppendLine("\nPer Particle Scaling:");
        sb.AppendLine($"Per Particle Scaling Min/Max: ({HandBiasRange.x}, {HandBiasRange.y})");
        sb.AppendLine($"Per Particle Scaling Exponent: {HandBiasRangeExp}");

        // Position Offsets Section
        sb.AppendLine("\nPosition Offsets:");
        sb.AppendLine($"Position Offset Min/Max: ({PosOffsetRange.x}, {PosOffsetRange.y})");

        // Joint Mirror Section
        sb.AppendLine("\nJoint Mirror:");
        sb.AppendLine($"Att To Replay: {AttToReplay}");
        sb.AppendLine($"Mirror Distance: {MirrorDistance}");

        // Stretch Section
        sb.AppendLine("\nStretch:");
        sb.AppendLine($"Stretch Factor Min/Max: ({StretchFactorRange.x}, {StretchFactorRange.y})");
        sb.AppendLine($"Stretch Factor Exponent: {StretchFactorExponent}");
        sb.AppendLine($"Stretch Max: {StretchMax}");

        // Index Step Size Section
        sb.AppendLine("\nIndex Step Size:");
        sb.AppendLine($"Index Step Size Min/Max: ({IndexStepSizeRange.x}, {IndexStepSizeRange.y})");

        // Color Section
        sb.AppendLine("\nColor:");
        sb.AppendLine($"Use Debug Colors: {UseDebugColors}");
        sb.AppendLine($"Color Time Gradient Update Speed: {ColorTimeGradientUpdateSpeed}");
        sb.AppendLine($"Color Lerp: {ColorLerp}");
        sb.AppendLine($"Alpha Min/Max: ({Alpha})");

        return sb.ToString();
    }
    #endregion
}
