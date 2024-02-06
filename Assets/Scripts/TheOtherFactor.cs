using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;
using Unity.Burst;
using System.Collections;
using Oculus.Interaction;
using UnityEngine.ParticleSystemJobs;
using System.Threading.Tasks;
using System;
using System.Linq;

public class TheOtherFactor : MonoBehaviour
{
    #region Variables
    #region Inspector Variables
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
    private ParticleSystem particleSystem;
    private ParticleSystemRenderer particleRenderer;
    #endregion
    #region Attraction
    [Header("Attraction")]
    public float AttractionStrength = 1f;
    #endregion
    #region Velocity
    [Header("Velocity")]
    [Tooltip("The linear interpolation factor for the velocity change in one update step.")]
    public float VelocityLerp = .1f;
    #endregion
    #region Attraction Scaling Per Group/Hand
    [Header("Attraction Scaling Per Group/Hand")]
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Green when using debug color in attraciton job.")]
    public Vector2 ParticlesAttractionGroup1 = Vector2.one;
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Red when using debug color in attraciton job.")]
    public Vector2 ParticlesAttractionGroup2 = Vector2.one;
    #endregion
    #region Per Particle Scaling
    [Header("Per Particle Scaling")]
    public Vector2 PerParticleScalingMinMax = new Vector2(0f,1f);
    public float PerParticleScalingPowerFactor = .1f;
    #endregion
    #region Position Offsets
    // should not really be here because it belongs to positions job, but looks better in inspector. need create custom inspector
    [Header("Position Offsets")]
    [Tooltip("Determines the min and max interpolation between the relative positions on the mesh and the joint. 0 = full mesh, 1 = full joint")]
    public Vector2 cwp_PositionOffsetMinMax = new Vector2(0f, .7f);
    #endregion
    #region Index Step Size
    [Header("Index Step Size")]
    [Tooltip("The min and max size step a particle can take in the array that holds all mesh positions. 0-0 = particles stick to one position. 1-1 = particles always progress 1 position each update. 0-2 = particles might stay in place, move ahead one position or 2 positions in one update.")]
    public Vector2 IndexStepSizeMinMax = new Vector2(0, 2);
    #endregion
    #region Color
    [Header("Color")]
    [Tooltip("The linear interpolation factor for color change in one opdate step.")]
    public float ColorLerp = .05f;
    #endregion
    #region Pseudo Mesh
    [Header("Pseudo Mesh")]
    private PseudoMeshCreator PseudoMeshCreator;
    #endregion
    #endregion
    #region Internal Variables
    #region Attraction Job
    #region Job Handle
    private AttractionJob attractionJob;
    private JobHandle attractionJobHandle;
    #endregion
    #region Per Particle Scaling
    private float[] aJob_PerParticleScaling;
    #endregion
    #endregion
    #region World Positions Jobs
    #region Job Handle
    private JobHandle positionJobHandleL;
    private ComputeWorldPositionJob positionJobL;

    private JobHandle positionJobHandleR;
    private ComputeWorldPositionJob positionJobR;
    #endregion
    #region Position Offsets
    private int cwp_PositionOffsetsIndex = 0;
    #endregion
    #endregion
    #endregion
    #region Internal Utility
    // To make sure we can stop jobs from running before properly set up, has to do with the use of OnParticleUpdateJobScheduled
    [HideInInspector]
    public bool RunJobs = false;
    #region Hands
    #region Transform References
    private GameObject hands;
    #region Joints
    #region Oculus Script References to fetch Joints 
    private HandVisual LeftHandVisual;
    private HandVisual RightHandVisual;
    #endregion
    private List<Transform> LeftHandJoints = new List<Transform>();
    private List<Transform> RightHandJoints = new List<Transform>();
    #endregion
    #region Skinned Mesh Renderer
    private SkinnedMeshRenderer leftHandMesh;
    private SkinnedMeshRenderer rightHandMesh;
    #endregion
    #endregion
    #region Pseudo-Mesh
    // used to alternate between world position jobs and attraction job to avoid read/write issues since world positions get communicated between them
    private bool isEvenFrame = true;
    #region Setup Variables
    [System.Serializable]
    public class JointData
    {
        public int jointIndex;
        public string jointName;
        public List<Vector3> relativePositions = new List<Vector3>();
    }
    private List<JointData> JointOnMeshMap1 = new List<JointData>();
    private List<JointData> JointOnMeshMap2 = new List<JointData>();
    #endregion
    #region Runtime "Pseudo Mesh"
    private List<Vector3> LeftHandRelativePositions = new List<Vector3>();
    private List<Vector3> RightHandRelativePositions = new List<Vector3>();
    private List<int> LeftHandJointIndices = new List<int>();
    private List<int> RightHandJointIndices = new List<int>();
    #endregion
    #endregion
    #endregion

    #endregion
    #endregion
    #region Start / Stop
    private void Start()
    {
        #region Find References
        #region Particle System
        particleSystem = GetComponent<ParticleSystem>();

        ParticleSystem.MainModule mainModule = particleSystem.main;
        mainModule.maxParticles = 1000000;
        mainModule.startColor = Color.black;
        mainModule.playOnAwake = false;

        // Get the ParticleSystemRenderer component
        particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = ParticleMaterial;
        #endregion
        #region Pseudo Mesh Creator
        PseudoMeshCreator = GameObject.Find("PseudoMeshCreator").GetComponent<PseudoMeshCreator>();
        #endregion
        #region Hands
        LeftHandVisual =  PseudoMeshCreator.LeftHandVisual;
        if (LeftHandVisual == null)
        {
            Debug.LogError("LeftHandVisual GameObject or HandVisual component not found.");
        }

        RightHandVisual = PseudoMeshCreator.RightHandVisual;
        if (RightHandVisual == null)
        {
            Debug.LogError("RightHandVisual GameObject or HandVisual component not found.");
        }

        hands = PseudoMeshCreator.Hands;
        if (hands == null)
        {
            Debug.LogError("Hands GameObject not found.");
        }

        leftHandMesh = PseudoMeshCreator.leftHandMesh;
        if (leftHandMesh == null)
        {
            Debug.LogError("l_handMeshNode GameObject or SkinnedMeshRenderer component not found.");
        }

        rightHandMesh = PseudoMeshCreator.rightHandMesh;
        if (rightHandMesh == null)
        {
            Debug.LogError("r_handMeshNode GameObject or SkinnedMeshRenderer component not found.");
        }
        #endregion
        #endregion
        StartTheOtherFactor();
    }
    #region Callable Start / Stop Functions triggered by Inspector Button created in Editor Script
    public async void StartTheOtherFactor()
    {
        gameObject.SetActive(true);
        await InitializeParticlesCoroutine();
    }
    public void StopTheOtherFactor()
    {
        RunJobs = false;
        this.gameObject.SetActive(false);
    }
    #endregion
    private async Task InitializeParticlesCoroutine()
    {
        #region Fetch Pseudo Mesh
        FetchPseudoMesh();
        await Task.Yield();
        #endregion
        #region Initialze Runtime Arrays
        InitializePerParticleScalingArray();
        await Task.Yield();
        #endregion
        #region Wait For Hands Close To HMD
        await WaitForHandsCloseToHMD(.4f);
        #endregion
        #region Emit Particles
        EmitParticles();
        await Task.Yield();
        #endregion
        #region Initialize Jobs
        InitializeRuntimeJobs();
        await Task.Yield();
        #endregion
        #region Activate Runtime Functions
        RunJobs = true;
        #endregion
    }
    #region Fetch Pseudo Mesh
    private void FetchPseudoMesh()
    {
        LeftHandJoints = PseudoMeshCreator.LeftHandJoints;
        RightHandJoints = PseudoMeshCreator.RightHandJoints;
        (LeftHandJointIndices, LeftHandRelativePositions) =  PseudoMeshCreator.ResizeListsPreservingPercentages(ParticlesPerHand, "left");// PseudoMeshCreator.LeftHandRelativePositions;
        (RightHandJointIndices, RightHandRelativePositions) = PseudoMeshCreator.ResizeListsPreservingPercentages(ParticlesPerHand, "right");// PseudoMeshCreator.LeftHandRelativePositions;
    }
    #endregion
    #region Initialize Per Particle Scaling Array
    private void InitializePerParticleScalingArray()
    {
        int totalParticles = ParticlesPerHand * 2;
        aJob_PerParticleScaling = new float[totalParticles];
        for (int i = 0; i < totalParticles; i++)
        {
            float linearRandom = UnityEngine.Random.Range(PerParticleScalingMinMax.x, PerParticleScalingMinMax.y);
            aJob_PerParticleScaling[i] = Mathf.Pow(linearRandom, PerParticleScalingPowerFactor);
        }
    }
    #endregion
    # region Wait For Hands Close To HMD
    public async Task WaitForHandsCloseToHMD(float targetDistance)
    {
        var leftMiddle1Joint = LeftHandJoints.FirstOrDefault(j => j.name == "b_l_middle1");
        var rightMiddle1Joint = RightHandJoints.FirstOrDefault(j => j.name == "b_r_middle1");

        // Initially assume both hands are not within the target distance
        bool isLeftHandClose = false;
        bool isRightHandClose = false;

        // Continue checking until both hands are within the target distance
        while (!isLeftHandClose || !isRightHandClose)
        {
            // Calculate distances from the HMD to each hand's middle1 joint
            float distanceToLeftHand = Vector3.Distance(leftMiddle1Joint.position, Camera.main.transform.position);
            float distanceToRightHand = Vector3.Distance(rightMiddle1Joint.position, Camera.main.transform.position);

            // Update flags based on whether each hand is within the target distance
            isLeftHandClose = distanceToLeftHand <= targetDistance;
            isRightHandClose = distanceToRightHand <= targetDistance;

            // Wait for a short interval before checking again to avoid tight looping
            await Task.Delay(100); // Adjust the delay as needed
        }

        // Both hands are now within the target distance from the HMD
        // You can proceed with the rest of your logic here
    }
    #endregion
    #region Emit Particles
    /// <summary>
    /// Emits particles as a tiny sphere roughly at the middle of each hand.
    /// </summary>
    private void EmitParticles()
    {
        int totalParticles = ParticlesPerHand * 2;
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            startSize = ParticleSizeMinMax.x,
            startLifetime = 3600f, // 1 hour
        };

        // Find the specific joints in the arrays
        var leftMiddle1Joint = LeftHandJoints.FirstOrDefault(j => j.name == "b_l_middle1");
        var rightMiddle1Joint = RightHandJoints.FirstOrDefault(j => j.name == "b_r_middle1");

        if (leftMiddle1Joint != null && rightMiddle1Joint != null)
        {
            float emissionRadius = .05f; // Radius of the sphere around the hand joints

            // Emit particles around the left middle1 joint
            for (int i = 0; i < totalParticles / 2; i++)
            {
                emitParams.position = leftMiddle1Joint.position + UnityEngine.Random.insideUnitSphere * emissionRadius;
                particleSystem.Emit(emitParams, 1);
            }

            // Emit particles around the right middle1 joint
            for (int i = 0; i < totalParticles / 2; i++)
            {
                emitParams.position = rightMiddle1Joint.position + UnityEngine.Random.insideUnitSphere * emissionRadius;
                particleSystem.Emit(emitParams, 1);
            }
        }
    }
    #endregion
    #region Initialize Jobs
    private void InitializeRuntimeJobs()
    {
        int totalParticles = ParticlesPerHand * 2;
        #region AttractionJob
        #region Initialize Arrays
        #region Attraction Scaling Per Group/Hand
        NativeArray<Vector2> aJob_ParticlesAttractionLR = new NativeArray<Vector2>(totalParticles, Allocator.Persistent);
        #endregion
        #region Pseudo Mesh
        #region Positions
        NativeArray<Vector3> aJob_WorldPositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        NativeArray<Vector3> aJob_WorldPositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region Indices
        NativeArray<int> aJob_PseudoMeshIndicesL = new NativeArray<int>(totalParticles, Allocator.Persistent);
        NativeArray<int> aJob_PseudoMeshIndicesR = new NativeArray<int>(totalParticles, Allocator.Persistent);
        for (int i = 0; i < totalParticles; i++)
        {
            aJob_PseudoMeshIndicesL[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
            aJob_PseudoMeshIndicesR[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
        }
        NativeArray<int> aJob_IndexStepSizes = new NativeArray<int>(totalParticles, Allocator.Persistent);
        for (int i = 0; i < totalParticles; i++)
        {
            aJob_IndexStepSizes[i] = Mathf.RoundToInt(UnityEngine.Random.Range(IndexStepSizeMinMax.x, IndexStepSizeMinMax.y));
        }
        #endregion
        #endregion
        #endregion
        attractionJob = new AttractionJob
        {
            #region Attraction
            aJob_AttractionStrength = AttractionStrength,
            aJob_AttractionExponentDivisor = 2 * AttractionStrength * AttractionStrength,
            #endregion
            #region Velocity
            aJob_VelocityLerp = VelocityLerp,
            #endregion
            #region Attraction Scaling Per Group/Hand
            aJob_ParticlesAttractionLR = aJob_ParticlesAttractionLR,
            #endregion
            #region Pseudo Mesh
            #region Positions
            aJob_WorldPositionsL = aJob_WorldPositionsL,
            aJob_WorldPositionsR = aJob_WorldPositionsR,
            #endregion
            #region Indices
            aJob_PseudoMeshIndicesL = aJob_PseudoMeshIndicesL,
            aJob_PseudoMeshIndicesR = aJob_PseudoMeshIndicesR,
            aJob_IndexStepSizes = aJob_IndexStepSizes,
            aJob_IndexStepsSizeIndex = 0,
            #endregion
            #endregion
            #region Color
            aJob_ColorLerp = ColorLerp,
            #endregion
            #region Size
            aJob_ParticleSizeMinMax = ParticleSizeMinMax,
            aJob_DistanceForMinSize = DistanceForMinSize,
            aJob_SizeLerp = SizeLerp,
            #endregion
        };
        #endregion
        #region Position Jobs
        #region World Position Offsets
        NativeArray<float> cwp_PositionOffsets = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < ParticlesPerHand; i++)
        {
            cwp_PositionOffsets[i] = Mathf.RoundToInt(UnityEngine.Random.Range(cwp_PositionOffsetMinMax.x, cwp_PositionOffsetMinMax.y));
        }
        #endregion
        #region Position Job L
        #region Joints
        NativeArray<Vector3> cwp_JointPositionsL = new NativeArray<Vector3>(LeftHandJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> cwp_JointRotationsL = new NativeArray<quaternion>(LeftHandJoints.Count, Allocator.Persistent);
        NativeArray<int> cwp_JointToParticleMapL = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < LeftHandJointIndices.Count; i++)
        {
            cwp_JointToParticleMapL[i] = LeftHandJointIndices[i];
        }
        #endregion
        #region Pseudo Mesh
        NativeArray<Vector3> cwp_PseudoMeshParticlePositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < LeftHandJointIndices.Count; i++)
        {
            cwp_PseudoMeshParticlePositionsL[i] = LeftHandRelativePositions[i];
        }
        NativeArray<Vector3> cwp_WorldPositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        positionJobL = new ComputeWorldPositionJob
        {
            #region Joints
            cwp_JointPositions = cwp_JointPositionsL,
            cwp_JointRotations = cwp_JointRotationsL,
            cwp_JointToParticleMap = cwp_JointToParticleMapL,
            #endregion
            #region Pseudo Mesh
            cwp_PseudoMeshPositions = cwp_PseudoMeshParticlePositionsL,
            cwp_WorldPositions = cwp_WorldPositionsL,
            #endregion
            #region Position Offsets
            cwp_PositionOffsets = cwp_PositionOffsets,
            cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex
            #endregion
        };
        #endregion
        #region Position Job R
        #region Joints
        NativeArray<Vector3> cwp_JointPositionsR = new NativeArray<Vector3>(RightHandJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> cwp_JointRotationsR = new NativeArray<quaternion>(RightHandJoints.Count, Allocator.Persistent);
        NativeArray<int> cwp_JointToParticleMapR = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < RightHandJointIndices.Count; i++)
        {
            cwp_JointToParticleMapR[i] = RightHandJointIndices[i];
        }
        #endregion
        #region Pseudo Mesh
        NativeArray<Vector3> cwp_PseudoMeshParticlePositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < RightHandJointIndices.Count; i++)
        {
            cwp_PseudoMeshParticlePositionsR[i] = RightHandRelativePositions[i];
        }
        NativeArray<Vector3> cwp_WorldPositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        positionJobR = new ComputeWorldPositionJob
        {
            #region Joints
            cwp_JointPositions = cwp_JointPositionsR,
            cwp_JointRotations = cwp_JointRotationsR,
            cwp_JointToParticleMap = cwp_JointToParticleMapR,
            #endregion
            #region Pseudo Mesh
            cwp_PseudoMeshPositions = cwp_PseudoMeshParticlePositionsR,
            cwp_WorldPositions = cwp_WorldPositionsR,
            #endregion
            #region Position Offsets
            cwp_PositionOffsets = cwp_PositionOffsets,
            cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex
            #endregion
        };
        #endregion
        #endregion
    }
    #endregion
    #endregion
    #region Runtime Updates
    void OnParticleUpdateJobScheduled()
    {
        if (RunJobs && attractionJobHandle.IsCompleted && positionJobHandleL.IsCompleted && positionJobHandleR.IsCompleted)
        {
            if (isEvenFrame)
            {
                #region World Position Jobs
                cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex < positionJobL.cwp_PositionOffsets.Length ? cwp_PositionOffsetsIndex + 1 : 0;
                #region Schedule PositionL Job
                positionJobHandleL.Complete();
                #region Update Joint Positions
                for (int i = 0; i < positionJobL.cwp_JointPositions.Length; i++)
                {
                    positionJobL.cwp_JointPositions[i] = LeftHandJoints[i].position;
                    positionJobL.cwp_JointRotations[i] = LeftHandJoints[i].rotation;                   
                }
                #endregion
                #region Update World Positions in Neutral Array for Attraction Job to Read
                for (int i = 0; i < positionJobL.cwp_WorldPositions.Length; i++)
                {
                    attractionJob.aJob_WorldPositionsL[i] = positionJobL.cwp_WorldPositions[i];
                }
                #endregion
                #region Update PositionOffsetsIndex
                positionJobL.cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex;
                #endregion
                if (positionJobL.cwp_JointPositions.Length > 0) positionJobHandleL = positionJobL.Schedule(particleSystem.particleCount / 2, 1024);
                #endregion
                #region Schedule PositionR Job
                positionJobHandleR.Complete();
                #region Update Joint Positions
                for (int i = 0; i < positionJobR.cwp_JointPositions.Length; i++)
                {
                    positionJobR.cwp_JointPositions[i] = RightHandJoints[i].position;
                    positionJobR.cwp_JointRotations[i] = RightHandJoints[i].rotation;
                }
                #endregion
                #region Update World Positions in Neutral Array for Attraction Job to Read
                for (int i = 0; i < positionJobR.cwp_WorldPositions.Length; i++)
                {
                    attractionJob.aJob_WorldPositionsR[i] = positionJobR.cwp_WorldPositions[i];
                }
                #endregion
                #region Update PositionOffsetsIndex
                positionJobR.cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex;
                #endregion
                // This should not be necessary but somehow the first timing is weird so without it the job tries to execute before the arrays are assigned and that produces a null reference.
                if (positionJobR.cwp_JointPositions.Length > 0) positionJobHandleR = positionJobR.Schedule(particleSystem.particleCount / 2, 1024);
                #endregion
                #endregion
            }
            else
            { 
                #region Attraction Job
                attractionJobHandle.Complete();
                UpdateAttractionJob();
                attractionJobHandle = attractionJob.ScheduleBatch(particleSystem, 1024);
                #endregion
            }
            UpdateParticleMaterialAndHandVisual();
            isEvenFrame = !isEvenFrame;
        }
    }
    private void UpdateParticleMaterialAndHandVisual()
    {
        particleRenderer.material = ParticleMaterial;
        leftHandMesh.gameObject.SetActive(DisplayOculusHands);
        rightHandMesh.gameObject.SetActive(DisplayOculusHands);
    }
    private void UpdateAttractionJob()
    {
        #region Attraction
        attractionJob.aJob_AttractionStrength =  AttractionStrength;
        attractionJob.aJob_AttractionExponentDivisor = 2 * AttractionStrength * AttractionStrength; 
        #endregion
        #region Velocity
        attractionJob.aJob_VelocityLerp = VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        UpdateParticlesAttractionLR();
        #endregion
        #region Pseudo Mesh
        #region Indices
        attractionJob.aJob_IndexStepsSizeIndex = attractionJob.aJob_IndexStepsSizeIndex < attractionJob.aJob_IndexStepSizes.Length ? attractionJob.aJob_IndexStepsSizeIndex + 1 : 0;
        #endregion
        #endregion
        #region Color
        attractionJob.aJob_ColorLerp = ColorLerp;
        #endregion
        #region Size
        attractionJob.aJob_ParticleSizeMinMax = ParticleSizeMinMax;
        attractionJob.aJob_DistanceForMinSize = DistanceForMinSize;
        attractionJob.aJob_SizeLerp = SizeLerp;
        #endregion
    }
    private void UpdateParticlesAttractionLR()
    {
        Vector2 attractionVector = Vector2.one;

        for (int i = 0; i < attractionJob.aJob_ParticlesAttractionLR.Length; i++)
        {
            if (i < attractionJob.aJob_ParticlesAttractionLR.Length / 2)
            {
                attractionVector.x = ParticlesAttractionGroup1.x;
                attractionVector.y = ParticlesAttractionGroup1.y * aJob_PerParticleScaling[i];
            }
            else
            {
                attractionVector.x = ParticlesAttractionGroup2.x * aJob_PerParticleScaling[i];
                attractionVector.y = ParticlesAttractionGroup2.y;
            }
             
            attractionJob.aJob_ParticlesAttractionLR[i] = attractionVector;
        }
    }
    #endregion
    #region Jobs
    #region Attraction Job
    /// <summary>
    /// This job uses AttractionStrength parameters to control attraction strength
    /// and updates particle positions and velocities based on attraction to specific world positions. It handles particles for both left and right Hands.
    /// The job also includes logic for color copmutation based on the particles velocity.
    /// </summary>
    [BurstCompile]
    struct AttractionJob : IJobParticleSystemParallelForBatch
    {
        #region Job Variables
        #region Attraction
        [ReadOnly] public float aJob_AttractionStrength;
        [ReadOnly] public float aJob_AttractionExponentDivisor;
        #endregion
        #region Veloctiy
        [ReadOnly] public float aJob_VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        [ReadOnly] public NativeArray<Vector2> aJob_ParticlesAttractionLR;
        #endregion
        #region Pseudo Mesh
        #region Positions
        [ReadOnly] public NativeArray<Vector3> aJob_WorldPositionsL;
        [ReadOnly] public NativeArray<Vector3> aJob_WorldPositionsR;
        #endregion
        #region Indices
        public NativeArray<int> aJob_PseudoMeshIndicesL;
        public NativeArray<int> aJob_PseudoMeshIndicesR;
        [ReadOnly] public NativeArray<int> aJob_IndexStepSizes;
        [ReadOnly] public int aJob_IndexStepsSizeIndex;
        #endregion
        #endregion
        #region Color
        [ReadOnly] public float aJob_ColorLerp;
        #endregion
        #region Size
        [ReadOnly] public Vector2 aJob_ParticleSizeMinMax;
        [ReadOnly] public float aJob_DistanceForMinSize;
        [ReadOnly] public float aJob_SizeLerp;
        #endregion
        #endregion
        public void Execute(ParticleSystemJobData particles, int startIndex, int count)
        {
            #region Basic Setup
            var positions = particles.positions;
            var velocities = particles.velocities;
            var colors = particles.startColors;
            var sizes = particles.sizes.x;
            int endIndex = startIndex + count;
            #endregion

            for (int i = startIndex; i < endIndex; i++)
            {
                int particleIndex = i;
                Vector3 particlePosition = positions[particleIndex];

                #region Compute Attraction to Left Hand
                int pseudoMeshPosIndexL = aJob_PseudoMeshIndicesL[particleIndex];
                Vector3 worldPositionL = aJob_WorldPositionsL[pseudoMeshPosIndexL];
                aJob_PseudoMeshIndicesL[particleIndex] = (pseudoMeshPosIndexL + aJob_IndexStepSizes[(particleIndex + aJob_IndexStepsSizeIndex) % aJob_WorldPositionsL.Length]) % aJob_WorldPositionsL.Length;
                Vector3 velocityL = CalculateAttractionVelocity(worldPositionL, particlePosition, aJob_AttractionExponentDivisor, aJob_AttractionStrength);
                velocityL *= aJob_ParticlesAttractionLR[particleIndex].x;
                #endregion
                #region Compute Attraction to Right Hand
                int pseudoMeshPosIndexR = aJob_PseudoMeshIndicesR[particleIndex];
                Vector3 worldPositionR = aJob_WorldPositionsR[pseudoMeshPosIndexR];
                aJob_PseudoMeshIndicesR[particleIndex] = (pseudoMeshPosIndexR + aJob_IndexStepSizes[(particleIndex + aJob_IndexStepsSizeIndex) % aJob_WorldPositionsR.Length]) % aJob_WorldPositionsR.Length;
                Vector3 velocityR = CalculateAttractionVelocity(worldPositionR, particlePosition, aJob_AttractionExponentDivisor, aJob_AttractionStrength);
                velocityR *= aJob_ParticlesAttractionLR[particleIndex].y;
                #endregion

                #region Update Particle Velocity, Size and Color
                #region Veloctiy
                Vector3 velocity = velocityL + velocityR;
                velocity = math.lerp(velocities[particleIndex], velocity, aJob_VelocityLerp);
                velocities[particleIndex] = velocity;
                #endregion
                #region Size
                float distanceL = math.length(worldPositionL - particlePosition);
                float distanceR = math.length(worldPositionR - particlePosition);
                float leastDistance = math.min(distanceL, distanceR); 

                // Normalize the distance (0 at maxDistance or beyond, 1 at distance 0)
                float normalizedDistance = math.clamp(leastDistance / aJob_DistanceForMinSize, 0f, 1f);
                float inverseNormalizedDistance = 1 - normalizedDistance;
                float targetSize = math.lerp(aJob_ParticleSizeMinMax.x, aJob_ParticleSizeMinMax.y, inverseNormalizedDistance);

                sizes[i] = math.lerp(sizes[i], targetSize, aJob_SizeLerp);
                #endregion
                #region Color
                // Compute particle color
                Color color = ComputeParticleColor(velocity);
                colors[particleIndex] = Color.Lerp(colors[particleIndex], color, aJob_ColorLerp);
                // For Debugging, it can make sense to color the two groups of particles in distinct colors
                //colors[particleIndex] = particleIndex < particles.count / 2 ? Color.green : Color.red;
                //Color color = Color.white;
                //color.a = math.lerp(1, 0, inverseNormalizedDistance);
                //colors[particleIndex] = color;
                #endregion
                #endregion
            }
        }
        #region Functions
        #region Calculate Attraction Velocity
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Vector3 CalculateAttractionVelocity(Vector3 worldPosition, Vector3 particlePosition, float exponentAttractionDivisor, float attractionStrength)
        {
            // Calculate the direction and distance from the particle to the world position
            Vector3 direction = worldPosition - particlePosition;

            // Calculate the exponent for the Gaussian distribution based on the distance and attraction strength
            float exponent = -math.lengthsq(direction) /  exponentAttractionDivisor;

            // Apply the Gaussian distribution to calculate the attraction force
            float attraction = attractionStrength * math.exp(exponent);

            float distance = math.length(direction);
            attraction *= 1 - distance;

            Vector3 normDirection = math.normalize(direction);

            return normDirection * attraction;
        }
        #endregion
        #region Compute Particle Color
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Color ComputeParticleColor(Vector3 velocity)
        {
            Vector3 normalizedVelocity = math.normalize(velocity);

            // Create the final color
            Color finalColor = new Color(normalizedVelocity.x, normalizedVelocity.y, normalizedVelocity.z, 1);

            return finalColor;
        }
        #endregion
        #endregion
    }
    #endregion
    #region Compute Pseudo Mesh World Position Job
    /// <summary>
    /// Computes the world positions for each particle in a parallelized fashion using Burst compilation for efficiency.
    /// This job uses joint information, pseudo mesh relative positions, and a precomputed array of position offsets to calculate the final world positions of particles.
    /// It interpolates particle positions between their relative positions and corresponding joint positions based on noise values.
    /// The noise values cycle through an array with an offset index, allowing synchronized, controllable noise without real-time random computation. 
    /// This approach is crucial for multiplayer online synchronization and provides dynamic, natural-looking particle movement around the Hands.
    /// </summary>
    [BurstCompile]
    struct ComputeWorldPositionJob : IJobParallelFor
    {
        #region Joints
        // Read-only arrays for joint positions and rotations, and a map from joint to particle
        [ReadOnly] public NativeArray<Vector3> cwp_JointPositions;
        [ReadOnly] public NativeArray<quaternion> cwp_JointRotations;
        [ReadOnly] public NativeArray<int> cwp_JointToParticleMap;
        #endregion
        #region Pseudo Mesh
        // Pseudo mesh positions and output array for world positions of particles
        [ReadOnly] public NativeArray<Vector3> cwp_PseudoMeshPositions;
        public NativeArray<Vector3> cwp_WorldPositions; // Output array
        #endregion
        #region Position Offset
        // Noise array for position offsets and the current index in the noise array
        [ReadOnly] public NativeArray<float> cwp_PositionOffsets;
        [ReadOnly] public int cwp_PositionOffsetsIndex;
        #endregion
        public void Execute(int index)
        {
            // Calculate position offset for current particle based on noise array
            float positionOffset = cwp_PositionOffsets[(index + cwp_PositionOffsetsIndex) % cwp_PositionOffsets.Length];

            // Get the relative position, joint index, and joint data for the current particle
            Vector3 relativePosition = cwp_PseudoMeshPositions[index];
            int jointIndex = cwp_JointToParticleMap[index];
            Vector3 jointPosition = cwp_JointPositions[jointIndex];
            Quaternion jointRotation = cwp_JointRotations[jointIndex];

            // Compute the final world position for the particle and store it
            cwp_WorldPositions[index] = ComputeWorldPosition(jointPosition, jointRotation, relativePosition, positionOffset);
        }
        #region Compute World Position
        // Inline method for computing world position of a particle
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Vector3 ComputeWorldPosition(Vector3 jointPosition, Quaternion jointRotation, Vector3 relativePosition, float positionNoise)
        {
            // Calculate the world position based on joint data and relative position
            Vector3 worldPosition = jointPosition + (Vector3)math.mul(jointRotation, relativePosition);

            // Apply noise to the world position to interpolate between joint and pseudo mesh position
            worldPosition = worldPosition + (jointPosition - worldPosition) * positionNoise;

            return worldPosition;
        }
        #endregion
    }
    #endregion
    #region Complete Jobs On Disable
    void OnDisable()
    {
        #region Complete Jobs
        attractionJobHandle.Complete();
        positionJobHandleL.Complete();
        positionJobHandleR.Complete();
        #endregion
    }
    #endregion
    #endregion
}