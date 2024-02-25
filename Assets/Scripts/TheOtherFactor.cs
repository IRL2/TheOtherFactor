using Oculus.Interaction;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

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
    public Vector4 ParticlesAttractionGroup1 = Vector4.one;
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Red when using debug color in attraciton job.")]
    public Vector4 ParticlesAttractionGroup2 = Vector4.one;
    public Vector4 ParticlesAttractionGroup3 = Vector4.one;
    public Vector4 ParticlesAttractionGroup4 = Vector4.one;
    #endregion
    #region Per Particle Scaling
    [Header("Per Particle Scaling")]
    [Tooltip("Restart of the runtime jobs (button in the inspector) required to apply a change here while in play mode.")]
    public Vector2 PerParticleScalingMinMax = new Vector2(0f, 1f);
    [Tooltip("Restart of the runtime jobs (button in the inspector) required to apply a change here while in play mode.")]
    public float PerParticleScalingExponent = .1f;
    #endregion
    #region Position Offsets
    // should not really be here because it belongs to positions job, but looks better in inspector. need create custom inspector
    [Header("Position Offsets")]
    [Tooltip("Determines the min and max interpolation between the relative positions on the mesh and the joint. 0 = full mesh, 1 = full joint")]
    public Vector2 PositionOffsetMinMax = new Vector2(0f, .7f);
    #endregion
    #region Index Step Size
    [Header("Index Step Size")]
    [Tooltip("The min and max size step a particle can take in the array that holds all mesh positions. 0-0 = particles stick to one position. 1-1 = particles always progress 1 position each update. 0-2 = particles might stay in place, move ahead one position or 2 positions in one update.")]
    public Vector2 IndexStepSizeMinMax = new Vector2(0, 2);
    #endregion
    #region Color
    [Header("Color")]
    public bool UseDebugColors = false;
    private Color BaseColor = Color.blue;
    public Gradient BaseColorTimeGradient;
    public float ColorTimeGradientUpdateSpeed = 3.14f;
    [Tooltip("The linear interpolation factor for color change in one opdate step.")]
    public float ColorLerp = .05f;
    public float HeartbeatSpeed = 1f;
    public bool UseHeartbeat = true;
    public Vector2 AlphaMinMax = new Vector2(.2f, .7f);
    #endregion
    #region Stretch
    [Header("Stretch")]
    public Vector2 StretchFactorMinMax = new Vector2(0f, 1f);
    public float StretchFactorExponent = 1f;
    private float[] StretchFactorIncrease;
    #endregion
    #endregion
    #region Internal Variables
    private ParticleSystem particleSys;
    private ParticleSystemRenderer particleRenderer;
    #region Pseudo Mesh
    private PseudoMeshCreator PseudoMeshCreator;
    #endregion
    #region Attraction Job
    #region Job Handle
    private AttractionJob attractJob;
    private JobHandle attractionJobHandle;
    #endregion
    #region Per Particle Scaling
    private float[] PerParticleScaling;
    #endregion
    #endregion
    #region World Positions Jobs
    #region Job Handle
    private JobHandle updateMeshJobHandleL;
    private UpdateMeshJob updateMeshJobL;

    private JobHandle updateMeshJobHandleR;
    private UpdateMeshJob updateMeshJobR;

    private JobHandle updateMeshJobHandleLM;
    private UpdateMeshJob updateMeshJobLM;

    private JobHandle updateMeshJobHandleRM;
    private UpdateMeshJob updateMeshJobRM;
    #endregion
    #region Position Offsets
    private int MeshPositionOffsetsIndex = 0;
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
    private ReplayCapture replay;
    private List<Transform> LJoints = new List<Transform>();
    private List<Transform> RJoints = new List<Transform>();
    private List<Transform> LMJoints = new List<Transform>();
    private List<Transform> RMJoints = new List<Transform>();
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
        particleSys = GetComponent<ParticleSystem>();

        ParticleSystem.MainModule mainModule = particleSys.main;
        mainModule.maxParticles = 1000000;
        mainModule.startColor = Color.black;
        mainModule.playOnAwake = false;

        // Get the ParticleSystemRenderer component
        particleRenderer = particleSys.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = ParticleMaterial;
        #endregion
        #region Pseudo Mesh Creator
        PseudoMeshCreator = GameObject.Find("PseudoMeshCreator").GetComponent<PseudoMeshCreator>();
        #endregion
        #region Hands
        LeftHandVisual = PseudoMeshCreator.LeftHandVisual;
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
        replay = GameObject.Find("ReplayJoints").GetComponent<ReplayCapture>();
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
        #region Stretch
        // Compute the position in front of the camera
        //CenterPosition = Camera.main.transform.position + Camera.main.transform.forward * CenterPositionDistance;
        StretchFactorIncrease = new float[ParticlesPerHand];
        for (int i = 0; i < StretchFactorIncrease.Length; i++)
        {
            float linearRandom = UnityEngine.Random.Range(StretchFactorMinMax.x, StretchFactorMinMax.y);
            StretchFactorIncrease[i] = Mathf.Pow(linearRandom, StretchFactorExponent);
            //StretchFactorIncrease[i] = UnityEngine.Random.Range(StretchFactorMinMax.x, StretchFactorMinMax.y);
        }
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
        LJoints = PseudoMeshCreator.LeftHandJoints;
        RJoints = PseudoMeshCreator.RightHandJoints;
        LMJoints = replay.leftHandJointsOutput;
        RMJoints = replay.rightHandJointsOutput;
        (LeftHandJointIndices, LeftHandRelativePositions) = PseudoMeshCreator.ResizeListsPreservingPercentages(ParticlesPerHand, "left");// PseudoMeshCreator.LeftHandRelativePositions;
        (RightHandJointIndices, RightHandRelativePositions) = PseudoMeshCreator.ResizeListsPreservingPercentages(ParticlesPerHand, "right");// PseudoMeshCreator.LeftHandRelativePositions;       
    }
    #endregion
    #region Initialize Per Particle Scaling Array
    private void InitializePerParticleScalingArray()
    {
        int totalParticles = ParticlesPerHand * 2;
        PerParticleScaling = new float[totalParticles];
        for (int i = 0; i < totalParticles; i++)
        {
            float linearRandom = UnityEngine.Random.Range(PerParticleScalingMinMax.x, PerParticleScalingMinMax.y);
            PerParticleScaling[i] = Mathf.Pow(linearRandom, PerParticleScalingExponent);
        }
    }
    #endregion
    # region Wait For Hands Close To HMD
    public async Task WaitForHandsCloseToHMD(float targetDistance)
    {
        var leftMiddle1Joint = LJoints.FirstOrDefault(j => j.name == "b_l_middle1");
        var rightMiddle1Joint = RJoints.FirstOrDefault(j => j.name == "b_r_middle1");

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
        var leftMiddle1Joint = LJoints.FirstOrDefault(j => j.name == "b_l_middle1");
        var rightMiddle1Joint = RJoints.FirstOrDefault(j => j.name == "b_r_middle1");

        if (leftMiddle1Joint != null && rightMiddle1Joint != null)
        {
            float emissionRadius = .05f; // Radius of the sphere around the hand joints

            // Emit particles around the left middle1 joint
            for (int i = 0; i < totalParticles / 2; i++)
            {
                emitParams.position = leftMiddle1Joint.position + UnityEngine.Random.insideUnitSphere * emissionRadius;
                particleSys.Emit(emitParams, 1);
            }

            // Emit particles around the right middle1 joint
            for (int i = 0; i < totalParticles / 2; i++)
            {
                emitParams.position = rightMiddle1Joint.position + UnityEngine.Random.insideUnitSphere * emissionRadius;
                particleSys.Emit(emitParams, 1);
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
        NativeArray<Vector4> ParticlesAttractionLR = new NativeArray<Vector4>(totalParticles, Allocator.Persistent);
        NativeArray<Vector4> PerParticleScaling = new NativeArray<Vector4>(totalParticles, Allocator.Persistent);
        #endregion
        #region Pseudo Mesh
        #region Positions
        NativeArray<Vector3> MeshPositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        NativeArray<Vector3> MeshPositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        NativeArray<Vector3> MeshPositionsRL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        NativeArray<Vector3> MeshPositionsRR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region Indices
        NativeArray<int> MeshIndicesL = new NativeArray<int>(totalParticles, Allocator.Persistent);
        NativeArray<int> MeshIndicesR = new NativeArray<int>(totalParticles, Allocator.Persistent);
        NativeArray<int> MeshIndicesRL = new NativeArray<int>(totalParticles, Allocator.Persistent);
        NativeArray<int> MeshIndicesRR = new NativeArray<int>(totalParticles, Allocator.Persistent);
        for (int i = 0; i < totalParticles; i++)
        {
            MeshIndicesL[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
            MeshIndicesR[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
            MeshIndicesRL[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
            MeshIndicesRR[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
        }
        NativeArray<int> IndexStepSizes = new NativeArray<int>(totalParticles, Allocator.Persistent);
        for (int i = 0; i < totalParticles; i++)
        {
            IndexStepSizes[i] = Mathf.RoundToInt(UnityEngine.Random.Range(IndexStepSizeMinMax.x, IndexStepSizeMinMax.y));
        }
        #endregion
        #region Joint Distance Moved
        NativeArray<float> a_JointDistanceMovedL = new NativeArray<float>(totalParticles, Allocator.Persistent);
        NativeArray<float> a_JointDistanceMovedR = new NativeArray<float>(totalParticles, Allocator.Persistent);
        NativeArray<float> a_JointDistanceMovedRL = new NativeArray<float>(totalParticles, Allocator.Persistent);
        NativeArray<float> a_JointDistanceMovedRR = new NativeArray<float>(totalParticles, Allocator.Persistent);
        #endregion
        #endregion
        #endregion
        attractJob = new AttractionJob
        {
            #region Attraction
            AttractionStrength = AttractionStrength,
            AttractionExponentDivisor = 2 * AttractionStrength * AttractionStrength,
            #endregion
            #region Velocity
            VelocityLerp = VelocityLerp,
            #endregion
            #region Attraction Scaling Per Group/Hand
            ParticlesAttractionGroups = ParticlesAttractionLR,
            PerParticleScaling = PerParticleScaling,
            #endregion
            #region Pseudo Mesh
            #region Positions
            MeshPositionsL = MeshPositionsL,
            MeshPositionsR = MeshPositionsR,
            MeshPositionsLM = MeshPositionsRL,
            MeshPositionsRM = MeshPositionsRR,
            #endregion
            #region Indices
            MeshIndicesL = MeshIndicesL,
            MeshIndicesR = MeshIndicesR,
            MeshIndicesLM = MeshIndicesRL,
            MeshIndicesRM = MeshIndicesRR,
            IndexStepSizes = IndexStepSizes,
            IndexStepsSizeIndex = 0,
            #endregion
            #region Joint Distance Moved
            JointDistanceMovedL = a_JointDistanceMovedL,
            JointDistanceMovedR = a_JointDistanceMovedR,
            JointDistanceMovedLM = a_JointDistanceMovedRL,
            JointDistanceMovedRM = a_JointDistanceMovedRR,
            #endregion
            #endregion
            #region Color
            UseDebugColors = UseDebugColors == true ? 1 : 0,
            BaseColor = BaseColor,
            ColorLerp = ColorLerp,
            Alpha = 0f,
            #endregion
            #region Size
            ParticleSizeMinMax = ParticleSizeMinMax,
            DistanceForMinSize = DistanceForMinSize,
            SizeLerp = SizeLerp,
            #endregion
        };
        #endregion
        #region Update Mesh Jobs
        #region World Position Offsets
        NativeArray<float> MeshPositionOffsets = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < ParticlesPerHand; i++)
        {
            MeshPositionOffsets[i] = Mathf.RoundToInt(UnityEngine.Random.Range(PositionOffsetMinMax.x, PositionOffsetMinMax.y));
        }
        #endregion
        #region Update Mesh Job L
        #region Joints
        NativeArray<Vector3> JointPositionsL = new NativeArray<Vector3>(LJoints.Count, Allocator.Persistent);
        NativeArray<Vector3> PrevJointPositionsL = new NativeArray<Vector3>(LJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> JointRotationsL = new NativeArray<quaternion>(LJoints.Count, Allocator.Persistent);
        NativeArray<int> JointToParticleMapL = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < LeftHandJointIndices.Count; i++)
        {
            JointToParticleMapL[i] = LeftHandJointIndices[i];
        }
        NativeArray<float> JointDistanceMovedL = new NativeArray<float>(totalParticles, Allocator.Persistent);
        //NativeArray<Vector3> JointPositionsPerParticleL = new NativeArray<Vector3>(totalParticles, Allocator.Persistent);
        #endregion
        #region Pseudo Mesh
        NativeArray<Vector3> BaseMeshPositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < LeftHandJointIndices.Count; i++)
        {
            BaseMeshPositionsL[i] = LeftHandRelativePositions[i];
        }
        NativeArray<Vector3> DynamicMeshPositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region StretchL
        NativeArray<float> StretchFactorL = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < StretchFactorL.Length; i++)
        {
            StretchFactorL[i] = 0;
        }

        #endregion
        updateMeshJobL = new UpdateMeshJob
        {
            #region Joints
            JointPositions = JointPositionsL,
            PrevJointPositions = PrevJointPositionsL,
            JointRotations = JointRotationsL,
            JointToParticleMap = JointToParticleMapL,
            JointDistanceMoved = JointDistanceMovedL,
            //JointPositionsPerParticle = JointPositionsPerParticleL,
            #endregion
            #region Pseudo Mesh
            BaseMeshPositions = BaseMeshPositionsL,
            DynamicMeshPositions = DynamicMeshPositionsL,
            #endregion
            #region Position Offsets
            MeshPositionOffsets = MeshPositionOffsets,
            MeshPositionOffsetsIndex = MeshPositionOffsetsIndex,
            #endregion
            #region Stretch
            StretchFactor = StretchFactorL,
            #endregion
        };
        #endregion
        #region Update Mesh Job R
        #region Joints
        NativeArray<Vector3> JointPositionsR = new NativeArray<Vector3>(RJoints.Count, Allocator.Persistent);
        NativeArray<Vector3> PrevJointPositionsR = new NativeArray<Vector3>(LJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> JointRotationsR = new NativeArray<quaternion>(RJoints.Count, Allocator.Persistent);
        NativeArray<int> JointToParticleMapR = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < RightHandJointIndices.Count; i++)
        {
            JointToParticleMapR[i] = RightHandJointIndices[i];
        }
        NativeArray<float> JointDistanceMovedR = new NativeArray<float>(totalParticles, Allocator.Persistent);
        #endregion
        #region Pseudo Mesh
        NativeArray<Vector3> BaseMeshPositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < RightHandJointIndices.Count; i++)
        {
            BaseMeshPositionsR[i] = RightHandRelativePositions[i];
        }
        NativeArray<Vector3> DynamicMeshPositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region StretchR
        NativeArray<float> StretchFactorR = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < StretchFactorR.Length; i++)
        {
            StretchFactorR[i] = 0;
        }
        #endregion
        updateMeshJobR = new UpdateMeshJob
        {
            #region Joints
            JointPositions = JointPositionsR,
            PrevJointPositions = PrevJointPositionsR,
            JointRotations = JointRotationsR,
            JointToParticleMap = JointToParticleMapR,
            JointDistanceMoved = JointDistanceMovedR,
            #endregion
            #region Pseudo Mesh
            BaseMeshPositions = BaseMeshPositionsR,
            DynamicMeshPositions = DynamicMeshPositionsR,
            #endregion
            #region Position Offsets
            MeshPositionOffsets = MeshPositionOffsets,
            MeshPositionOffsetsIndex = MeshPositionOffsetsIndex,
            #endregion
            #region Stretch
            StretchFactor = StretchFactorR,
            #endregion
        };
        #endregion
        #region Update Mesh Job LM
        #region Joints
        NativeArray<Vector3> JointPositionsLM = new NativeArray<Vector3>(LJoints.Count, Allocator.Persistent);
        NativeArray<Vector3> PrevJointPositionsLM = new NativeArray<Vector3>(LJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> JointRotationsLM = new NativeArray<quaternion>(LJoints.Count, Allocator.Persistent);
        NativeArray<float> JointDistanceMovedLM = new NativeArray<float>(totalParticles, Allocator.Persistent);
        #endregion
        #region Pseudo Mesh
        NativeArray<Vector3> DynamicMeshPositionsML = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region StretchRL
        NativeArray<float> StretchFactorLM = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < StretchFactorLM.Length; i++)
        {
            StretchFactorLM[i] = 0;
        }

        #endregion
        updateMeshJobLM = new UpdateMeshJob
        {
            #region Joints
            JointPositions = JointPositionsLM,
            PrevJointPositions = PrevJointPositionsLM,
            JointRotations = JointRotationsLM,
            JointToParticleMap = JointToParticleMapL,
            JointDistanceMoved = JointDistanceMovedLM,
            #endregion
            #region Pseudo Mesh
            BaseMeshPositions = BaseMeshPositionsL,
            DynamicMeshPositions = DynamicMeshPositionsML,
            #endregion
            #region Position Offsets
            MeshPositionOffsets = MeshPositionOffsets,
            MeshPositionOffsetsIndex = MeshPositionOffsetsIndex,
            #endregion
            #region Stretch
            StretchFactor = StretchFactorLM,
            #endregion
        };
        #endregion
        #region Update Mesh Job RM
        #region Joints
        NativeArray<Vector3> JointPositionsRM = new NativeArray<Vector3>(RJoints.Count, Allocator.Persistent);
        NativeArray<Vector3> PrevJointPositionsRM = new NativeArray<Vector3>(LJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> JointRotationsRM = new NativeArray<quaternion>(RJoints.Count, Allocator.Persistent);
        NativeArray<float> JointDistanceMovedRM = new NativeArray<float>(totalParticles, Allocator.Persistent);
        #endregion
        #region Pseudo Mesh
        NativeArray<Vector3> DynamicMeshPositionsRM = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region StretchRM
        NativeArray<float> StretchFactorRM = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < StretchFactorRM.Length; i++)
        {
            StretchFactorRM[i] = 0;
        }
        #endregion
        updateMeshJobRM = new UpdateMeshJob
        {
            #region Joints
            JointPositions = JointPositionsRM,
            PrevJointPositions = PrevJointPositionsRM,
            JointRotations = JointRotationsRM,
            JointToParticleMap = JointToParticleMapR,
            JointDistanceMoved = JointDistanceMovedRM,
            #endregion
            #region Pseudo Mesh
            BaseMeshPositions = BaseMeshPositionsR,
            DynamicMeshPositions = DynamicMeshPositionsRM,
            #endregion
            #region Position Offsets
            MeshPositionOffsets = MeshPositionOffsets,
            MeshPositionOffsetsIndex = MeshPositionOffsetsIndex,
            #endregion
            #region Stretch
            StretchFactor = StretchFactorRM,
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
        if (RunJobs && attractionJobHandle.IsCompleted && updateMeshJobHandleL.IsCompleted && updateMeshJobHandleR.IsCompleted && updateMeshJobHandleLM.IsCompleted && updateMeshJobHandleRM.IsCompleted)
        {
            #region Complete Jobs
            updateMeshJobHandleL.Complete();
            updateMeshJobHandleR.Complete();
            attractionJobHandle.Complete();
            updateMeshJobHandleLM.Complete();
            updateMeshJobHandleRM.Complete();
            #endregion
            if (isEvenFrame)
            {
                #region Update Mesh Jobs
                MeshPositionOffsetsIndex = MeshPositionOffsetsIndex < updateMeshJobL.MeshPositionOffsets.Length ? MeshPositionOffsetsIndex + 1 : 0;
                #region Update Mesh Job L
                #region Update Joint Positions
                for (int i = 0; i < updateMeshJobL.JointPositions.Length; i++)
                {
                    updateMeshJobL.JointPositions[i] = LJoints[i].position;
                    updateMeshJobL.JointRotations[i] = LJoints[i].rotation;
                }
                #endregion
                #region Update Joint Distance Moved in Attraction Job
                attractJob.JointDistanceMovedL = updateMeshJobL.JointDistanceMoved;
                #endregion
                #region Update Mesh Positions in Attraction Job
                attractJob.MeshPositionsL = updateMeshJobL.DynamicMeshPositions;
                #endregion
                #region Update PositionOffsetsIndex
                updateMeshJobL.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobL.StretchFactor.Length; i++)
                {
                    updateMeshJobL.StretchFactor[i] = updateMeshJobL.StretchFactor[i] < 1 ? updateMeshJobL.StretchFactor[i] + StretchFactorIncrease[i] : 1;
                }
                #endregion
                if (updateMeshJobL.JointPositions.Length > 0) updateMeshJobHandleL = updateMeshJobL.Schedule(updateMeshJobL.BaseMeshPositions.Length, 1024);
                #endregion
                #region Update Mesh Job R
                #region Update Joint Positions
                for (int i = 0; i < updateMeshJobR.JointPositions.Length; i++)
                {
                    updateMeshJobR.JointPositions[i] = RJoints[i].position;
                    updateMeshJobR.JointRotations[i] = RJoints[i].rotation;
                }
                #endregion
                #region Update Joint Distance Moved
                attractJob.JointDistanceMovedR = updateMeshJobR.JointDistanceMoved;
                #endregion
                #region Update Mesh Positions in Attraction Job
                attractJob.MeshPositionsR = updateMeshJobR.DynamicMeshPositions;
                #endregion
                #region Update PositionOffsetsIndex
                updateMeshJobR.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobR.StretchFactor.Length; i++)
                {
                    updateMeshJobR.StretchFactor[i] = updateMeshJobR.StretchFactor[i] < 1 ? updateMeshJobR.StretchFactor[i] + StretchFactorIncrease[i] : 1;
                }
                #endregion
                // This should not be necessary but somehow the first timing is weird so without it the job tries to execute before the arrays are assigned and that produces a null reference.
                if (updateMeshJobR.JointPositions.Length > 0) updateMeshJobHandleR = updateMeshJobR.Schedule(updateMeshJobR.BaseMeshPositions.Length, 1024);
                #endregion
                #region Update Mesh Job LM
                #region Update Joint Positions
                for (int i = 0; i < updateMeshJobLM.JointPositions.Length; i++)
                {
                    updateMeshJobLM.JointPositions[i] = LMJoints[i].position;
                    updateMeshJobLM.JointRotations[i] = LMJoints[i].rotation;
                }
                #endregion
                #region Update Joint Distance Moved in Attraction Job
                attractJob.JointDistanceMovedLM = updateMeshJobLM.JointDistanceMoved;
                #endregion
                #region Update Mesh Positions in Attraction Job
                attractJob.MeshPositionsLM = updateMeshJobLM.DynamicMeshPositions;
                #endregion
                #region Update PositionOffsetsIndex
                updateMeshJobLM.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobLM.StretchFactor.Length; i++)
                {
                    //updateMeshJobLM.StretchFactor[i] = MirrorHandsPositionDistance;// updateMeshJobLM.StretchFactor[i] < 1 ? updateMeshJobLM.StretchFactor[i] + StretchFactorIncrease[i] : 1;
                }
                #endregion
                if (updateMeshJobLM.JointPositions.Length > 0) updateMeshJobHandleLM = updateMeshJobLM.Schedule(updateMeshJobLM.BaseMeshPositions.Length, 1024);
                #endregion
                #region Update Mesh Job RM
                #region Update Joint Positions
                for (int i = 0; i < updateMeshJobRM.JointPositions.Length; i++)
                {
                    updateMeshJobRM.JointPositions[i] = RMJoints[i].position;
                    updateMeshJobRM.JointRotations[i] = RMJoints[i].rotation;
                }
                #endregion
                #region Update Joint Distance Moved
                attractJob.JointDistanceMovedRM = updateMeshJobRM.JointDistanceMoved;
                #endregion
                #region Update Mesh Positions in Attraction Job
                attractJob.MeshPositionsRM = updateMeshJobRM.DynamicMeshPositions;
                #endregion
                #region Update PositionOffsetsIndex
                updateMeshJobRM.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobRM.StretchFactor.Length; i++)
                {
                    //updateMeshJobRM.StretchFactor[i] = MirrorHandsPositionDistance;// updateMeshJobRM.StretchFactor[i] < 1 ? updateMeshJobRM.StretchFactor[i] + StretchFactorIncrease[i] : 1;
                }
                #endregion
                // This should not be necessary but somehow the first timing is weird so without it the job tries to execute before the arrays are assigned and that produces a null reference.
                if (updateMeshJobRM.JointPositions.Length > 0) updateMeshJobHandleRM = updateMeshJobRM.Schedule(updateMeshJobRM.BaseMeshPositions.Length, 1024);
                #endregion
                #endregion
            }
            else
            {
                #region Attraction Job
                UpdateAttractionJob();
                attractionJobHandle = attractJob.ScheduleBatch(particleSys, 1024);
                #endregion
                #region Update Previous Joint Positions in Update Mesh Job
                for (int i = 0; i < updateMeshJobL.JointPositions.Length; i++)
                {
                    updateMeshJobL.PrevJointPositions[i] = LJoints[i].position;
                    updateMeshJobR.PrevJointPositions[i] = RJoints[i].position;
                }
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
        attractJob.AttractionStrength = AttractionStrength;
        attractJob.AttractionExponentDivisor = 2 * AttractionStrength * AttractionStrength;
        #endregion
        #region Velocity
        attractJob.VelocityLerp = VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        UpdateParticlesAttractionLR();
        #endregion
        #region Pseudo Mesh
        #region Indices
        attractJob.IndexStepsSizeIndex = attractJob.IndexStepsSizeIndex < attractJob.IndexStepSizes.Length ? attractJob.IndexStepsSizeIndex + 1 : 0;
        #endregion
        #endregion
        #region Color
        #region Update Base Color
        // Use Time.time multiplied by changeSpeed to get a value that increases over time
        // The sine function will oscillate this value between -1 and 1
        float sineValue = Mathf.Sin(Time.time * ColorTimeGradientUpdateSpeed);

        // Map the sine value to a 0-1 range
        float gradientTime = (sineValue + 1) / 2;

        // Set the base color based on the current gradientTime and the gradient
        BaseColor = BaseColorTimeGradient.Evaluate(gradientTime);
        #endregion
        attractJob.UseDebugColors = UseDebugColors == true ? 1 : 0;
        attractJob.BaseColor = BaseColor;
        attractJob.ColorLerp = ColorLerp;
        #region Alpha
        #region Heartbeat
        // Time factor adjusted for speed
        float timeFactor = Time.time * HeartbeatSpeed;

        // Calculate the phase of the cycle [0, 1]
        float cyclePhase = timeFactor - Mathf.Floor(timeFactor);

        // Define phases for "lub", "dub", and pause
        float lubPhase = 0.2f; // Duration of the first beat
        float gapPhase = 0.05f; // Short gap between "lub" and "dub"
        float dubPhase = 0.2f; // Duration of the second beat

        // Calculate alpha based on the phase
        float alpha;
        if (cyclePhase <= lubPhase) // "Lub" beat
        {
            alpha = Mathf.Sin(cyclePhase / lubPhase * Mathf.PI) * AlphaMinMax.y;
        }
        else if (cyclePhase <= lubPhase + gapPhase + dubPhase) // "Dub" beat
        {
            alpha = Mathf.Sin((cyclePhase - lubPhase - gapPhase) / dubPhase * Mathf.PI) * AlphaMinMax.y * 0.8f; // Slightly less intense than "lub"
        }
        else // Pause
        {
            alpha = AlphaMinMax.x;
        }
        #endregion
        attractJob.Alpha = UseHeartbeat == true ? Mathf.Clamp(alpha, 0, 1) : AlphaMinMax.y;
        #endregion
        #endregion
        #region Size
        attractJob.ParticleSizeMinMax = ParticleSizeMinMax;
        attractJob.DistanceForMinSize = DistanceForMinSize;
        attractJob.SizeLerp = SizeLerp;
        #endregion
    }
    private void UpdateParticlesAttractionLR()
    {
        Vector4 attractionVector = Vector4.one;
        Vector4 perParticleScalingVector = Vector4.one;

        int quarterLength = attractJob.ParticlesAttractionGroups.Length / 4;

        for (int i = 0; i < attractJob.ParticlesAttractionGroups.Length; i++)
        {
            if (i < quarterLength)
            {
                // First quarter
                attractionVector = ParticlesAttractionGroup1;
                perParticleScalingVector = new Vector4(1, PerParticleScaling[i], PerParticleScaling[i], PerParticleScaling[i]);
            }
            else if (i < quarterLength * 2)
            {
                // Second quarter
                attractionVector = ParticlesAttractionGroup2;
                perParticleScalingVector = new Vector4(PerParticleScaling[i], 1, PerParticleScaling[i], PerParticleScaling[i]);
            }
            else if (i < quarterLength * 3)
            {
                // Third quarter
                attractionVector = ParticlesAttractionGroup3;
                perParticleScalingVector = new Vector4(PerParticleScaling[i], PerParticleScaling[i], 1, PerParticleScaling[i]);
            }
            else
            {
                // Fourth quarter
                attractionVector = ParticlesAttractionGroup4;
                perParticleScalingVector = new Vector4(PerParticleScaling[i], PerParticleScaling[i], PerParticleScaling[i], 1);
            }
            attractJob.ParticlesAttractionGroups[i] = attractionVector;
            attractJob.PerParticleScaling[i] = perParticleScalingVector;
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
        [ReadOnly] public float AttractionStrength;
        [ReadOnly] public float AttractionExponentDivisor;
        #endregion
        #region Veloctiy
        [ReadOnly] public float VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        [ReadOnly] public NativeArray<Vector4> ParticlesAttractionGroups;
        [ReadOnly] public NativeArray<Vector4> PerParticleScaling;
        #endregion
        #region Mesh
        #region Positions
        [ReadOnly] public NativeArray<Vector3> MeshPositionsL;
        [ReadOnly] public NativeArray<Vector3> MeshPositionsR;
        [ReadOnly] public NativeArray<Vector3> MeshPositionsLM;
        [ReadOnly] public NativeArray<Vector3> MeshPositionsRM;
        #endregion
        #region Indices
        public NativeArray<int> MeshIndicesL;
        public NativeArray<int> MeshIndicesR;
        public NativeArray<int> MeshIndicesLM;
        public NativeArray<int> MeshIndicesRM;
        [ReadOnly] public NativeArray<int> IndexStepSizes;
        [ReadOnly] public int IndexStepsSizeIndex;
        #endregion
        #region Joint Distance Moved
        [ReadOnly] public NativeArray<float> JointDistanceMovedL;
        [ReadOnly] public NativeArray<float> JointDistanceMovedR;
        [ReadOnly] public NativeArray<float> JointDistanceMovedLM;
        [ReadOnly] public NativeArray<float> JointDistanceMovedRM;
        #endregion
        #endregion
        #region Color
        [ReadOnly] public int UseDebugColors;
        [ReadOnly] public Color BaseColor;
        [ReadOnly] public float ColorLerp;
        [ReadOnly] public float Alpha;
        #endregion
        #region Size
        [ReadOnly] public Vector2 ParticleSizeMinMax;
        [ReadOnly] public float DistanceForMinSize;
        [ReadOnly] public float SizeLerp;
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
                Vector3 particlePos = positions[particleIndex];

                #region Compute Attraction to Left Hand
                int meshPosIndexL = MeshIndicesL[particleIndex];
                MeshIndicesL[particleIndex] = (meshPosIndexL + IndexStepSizes[(particleIndex + IndexStepsSizeIndex) % MeshPositionsL.Length]) % MeshPositionsL.Length;

                Vector3 meshPosL = MeshPositionsL[meshPosIndexL];
                Vector3 directionToMeshL = meshPosL - particlePos;
                float distanceToMeshL = math.length(directionToMeshL);

                Vector3 velocityL = CalculateAttractionVelocity(directionToMeshL, distanceToMeshL, AttractionExponentDivisor, AttractionStrength,
                                                                ParticlesAttractionGroups[particleIndex].x, JointDistanceMovedL[meshPosIndexL], PerParticleScaling[particleIndex].y);
                #endregion
                #region Compute Attraction to Right Hand
                int meshPosIndexR = MeshIndicesR[particleIndex];
                MeshIndicesR[particleIndex] = (meshPosIndexR + IndexStepSizes[(particleIndex + IndexStepsSizeIndex) % MeshPositionsR.Length]) % MeshPositionsR.Length;

                Vector3 meshPosR = MeshPositionsR[meshPosIndexR];
                Vector3 directionToMeshR = meshPosR - particlePos;
                float distanceToMeshR = math.length(directionToMeshR);

                Vector3 velocityR = CalculateAttractionVelocity(directionToMeshR, distanceToMeshR, AttractionExponentDivisor, AttractionStrength,
                                                                ParticlesAttractionGroups[particleIndex].y, JointDistanceMovedR[meshPosIndexR], PerParticleScaling[particleIndex].x);
                #endregion
                #region Compute Attraction to RLeft Hand
                int meshPosIndexLM = MeshIndicesLM[particleIndex];
                MeshIndicesLM[particleIndex] = (meshPosIndexLM + IndexStepSizes[(particleIndex + IndexStepsSizeIndex) % MeshPositionsLM.Length]) % MeshPositionsLM.Length;

                Vector3 meshPosLM = MeshPositionsLM[meshPosIndexLM];
                Vector3 directionToMeshLM = meshPosLM - particlePos;
                float distanceToMeshLM = math.length(directionToMeshLM);

                Vector3 velocityLM = CalculateAttractionVelocity(directionToMeshLM, distanceToMeshLM, AttractionExponentDivisor, AttractionStrength,
                                                                ParticlesAttractionGroups[particleIndex].z, JointDistanceMovedLM[meshPosIndexLM], PerParticleScaling[particleIndex].w);
                #endregion
                #region Compute Attraction to RRight Hand
                int meshPosIndexRM = MeshIndicesRM[particleIndex];
                MeshIndicesRM[particleIndex] = (meshPosIndexRM + IndexStepSizes[(particleIndex + IndexStepsSizeIndex) % MeshPositionsRM.Length]) % MeshPositionsRM.Length;

                Vector3 meshPosRM = MeshPositionsRM[meshPosIndexRM];
                Vector3 directionToMeshRM = meshPosRM - particlePos;
                float distanceToMeshRM = math.length(directionToMeshRM);

                Vector3 velocityRM = CalculateAttractionVelocity(directionToMeshRM, distanceToMeshRM, AttractionExponentDivisor, AttractionStrength,
                                                                ParticlesAttractionGroups[particleIndex].w, JointDistanceMovedRM[meshPosIndexRM], PerParticleScaling[particleIndex].z);
                #endregion

                #region Update Particle Velocity, Size and Color
                #region Veloctiy
                velocities[particleIndex] = math.lerp(velocities[particleIndex], velocityL + velocityR + velocityLM + velocityRM, VelocityLerp);
                #endregion
                #region Size
                sizes[i] = ComputeParticleSize(distanceToMeshL, distanceToMeshR, distanceToMeshLM, distanceToMeshRM, DistanceForMinSize, ParticleSizeMinMax, SizeLerp, sizes[particleIndex]);// math.lerp(sizes[i], targetSize, SizeLerp);
                #endregion
                #region Color
                colors[particleIndex] = ComputeParticleColor(velocities[particleIndex], BaseColor, particleIndex, particles.count, UseDebugColors, Alpha, ColorLerp, colors[particleIndex]);
                #endregion
                #endregion
            }
        }
        #region Functions
        #region Calculate Attraction Velocity
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Vector3 CalculateAttractionVelocity(Vector3 direction, float distance, float attractionExponentDivisor, float attractionStrength,
                                                           float perGroupHandScalor, float jointDistanceMoved, float perParticleScaling)
        {
            // Calculate the exponent for the Gaussian distribution based on the distance and attraction strength
            float exponent = -math.lengthsq(direction) / attractionExponentDivisor;

            // Apply the Gaussian distribution to calculate the attraction force
            float attraction = attractionStrength * math.exp(exponent);
            //attraction *= math.max(.1f, 1 - distance);
            attraction *= math.lerp(1, 0f, distance);

            Vector3 normDirection = math.normalize(direction);

            Vector3 velocity = normDirection * attraction;

            //velocity *= math.max(distance * 5, math.min(jointDistanceMoved * 1000 * distance * perParticleScaling, 1));
            //velocity *= math.max(.01f, math.min(jointDistanceMoved * 1000, 1));

            velocity *= perGroupHandScalor;

            velocity *= perParticleScaling;

            return velocity;
        }
        #endregion
        #region Compute Particle Color
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Color ComputeParticleColor(Vector3 velocity, Vector4 baseColor, int particleIndex, int totalParticles, int useDebugColors, float alpha, float colorLerp, Color currentColor)
        {
            Color newColor = new Color();

            if (useDebugColors != 0)
            {
                // Calculate the group size
                int groupSize = totalParticles / 4;

                // Determine the particle group by dividing the particle index by the group size
                int particleGroup = particleIndex / groupSize;

                // Clamp the particleGroup to max value of 3, in case totalParticles is not perfectly divisible by 4
                particleGroup = math.min(particleGroup, 3);

                // Assign color based on the particle group
                switch (particleGroup)
                {
                    case 0:
                        newColor = Color.green;
                        break;
                    case 1:
                        newColor = Color.red;
                        break;
                    case 2:
                        newColor = Color.blue;
                        break;
                    case 3:
                        newColor = Color.yellow;
                        break;
                    default:
                        newColor = Color.white; // Fallback color, should not be reached
                        break;
                }
            }
            else
            {
                float minColorValue = .1f;

                // Scale color noise by the non-normalized velocity components
                float rNoise = math.lerp(velocity.x, baseColor.x, baseColor.w) + minColorValue;
                float gNoise = math.lerp(velocity.y, baseColor.y, baseColor.w) + minColorValue;
                float bNoise = math.lerp(velocity.z, baseColor.z, baseColor.w) + minColorValue;

                // Create the final color
                newColor = new Color(rNoise, gNoise, bNoise, 1);
            }
            //int particleGroup = particleIndex < totalParticles / 2 ? 1 : 2;
            //Color debugColor = particleGroup == 1 ? Color.green : Color.red;

            newColor.a = alpha;

            Color finalColor = Color.Lerp(currentColor, newColor, colorLerp);

            return finalColor;
        }
        #endregion
        #region Compute Particle Size
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static float ComputeParticleSize(float distanceToMeshL, float distanceToMeshR, float distanceToMeshLM, float distanceToMeshRM, float distanceForMinSize, Vector2 particleSizeMinMax, float sizeLerp, float currentSize)
        {
            float leastDistance = math.min(distanceToMeshL, distanceToMeshR);
            leastDistance = math.min(leastDistance, distanceToMeshLM);
            leastDistance = math.min(leastDistance, distanceToMeshRM);

            // Normalize the distance (0 at maxDistance or beyond, 1 at distance 0)
            float normalizedDistance = math.clamp(leastDistance / distanceForMinSize, 0f, 1f);
            float inverseNormalizedDistance = 1 - normalizedDistance;
            float targetSize = math.lerp(particleSizeMinMax.x, particleSizeMinMax.y, inverseNormalizedDistance);

            float finalSize = math.lerp(currentSize, targetSize, sizeLerp);

            return finalSize;
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
    struct UpdateMeshJob : IJobParallelFor
    {
        #region Variables
        #region Joints
        // Read-only arrays for joint positions and rotations, and a map from joint to particle
        [ReadOnly] public NativeArray<Vector3> JointPositions;
        [ReadOnly] public NativeArray<Vector3> PrevJointPositions;
        [ReadOnly] public NativeArray<quaternion> JointRotations;
        [ReadOnly] public NativeArray<int> JointToParticleMap;
        public NativeArray<float> JointDistanceMoved;
        //public NativeArray<Vector3> JointPositionsPerParticle;
        #endregion
        #region Pseudo Mesh
        // Pseudo mesh positions and output array for world positions of particles
        [ReadOnly] public NativeArray<Vector3> BaseMeshPositions;
        public NativeArray<Vector3> DynamicMeshPositions; // Output array
        #endregion
        #region Position Offset
        // Noise array for position offsets and the current index in the noise array
        [ReadOnly] public NativeArray<float> MeshPositionOffsets;
        [ReadOnly] public int MeshPositionOffsetsIndex;
        #endregion
        #endregion
        #region Stretch
        [ReadOnly] public NativeArray<float> StretchFactor;
        #endregion
        public void Execute(int index)
        {
            // Calculate position offset for current particle based on noise array
            float positionOffset = MeshPositionOffsets[(index + MeshPositionOffsetsIndex) % MeshPositionOffsets.Length];

            // Get the relative position, joint index, and joint data for the current particle
            Vector3 baseMeshPosition = BaseMeshPositions[index];
            int jointIndex = JointToParticleMap[index];
            Vector3 jointPosition = JointPositions[jointIndex];
            Vector3 prevJointPosition = PrevJointPositions[jointIndex];
            Quaternion jointRotation = JointRotations[jointIndex];

            // Compute the final world position for the particle and store it
            Vector3 DynamicMeshPosition = ComputeWorldPosition(jointPosition, jointRotation, baseMeshPosition, positionOffset);
            // Stretch the dynamic pseudo mesh
            //DynamicMeshPositions[index] += (TranslationAxis * math.max(0f, StretchFactor[index]));
            float z = math.lerp(DynamicMeshPosition.z, -DynamicMeshPosition.z, StretchFactor[index]);
            DynamicMeshPositions[index] = new Vector3(DynamicMeshPosition.x, DynamicMeshPosition.y, z);
            JointDistanceMoved[index] = math.length(prevJointPosition - jointPosition);
            //JointPositionsPerParticle[index] = jointPosition;
        }
        #region Compute World Position
        // Inline method for computing world position of a particle
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Vector3 ComputeWorldPosition(Vector3 jointPosition, Quaternion jointRotation, Vector3 baseMeshPosition, float positionOffset)
        {
            // Calculate the world position based on joint data and relative position
            Vector3 dynamicMeshPosition = jointPosition + (Vector3)math.mul(jointRotation, baseMeshPosition);

            // Apply noise to the world position to interpolate between joint and pseudo mesh position
            dynamicMeshPosition = dynamicMeshPosition + (jointPosition - dynamicMeshPosition) * positionOffset;

            return dynamicMeshPosition;
        }
        #endregion
    }
    #endregion
    #region Complete Jobs On Disable
    void OnDisable()
    {
        #region Complete Jobs
        attractionJobHandle.Complete();
        updateMeshJobHandleL.Complete();
        updateMeshJobHandleR.Complete();
        #endregion
    }
    #endregion
    #endregion
}