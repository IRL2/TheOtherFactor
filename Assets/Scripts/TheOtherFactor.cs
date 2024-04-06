using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

public class TheOtherFactor : MonoBehaviour
{
    #region Variables
    #region Inspector Variables
    #region Attraction
    [Header("Attraction")]
    [Tooltip("The general attraction strength used for all sources of attraction. It get scaled by the distance to the attraction point.")]
    public float AttractionStrength = 1f;
    [Tooltip("The linear interpolation factor for the velocity change in one update step.")]
    public float VelocityLerp = .1f;
    [Tooltip("The distance between a particle and its attraction source affects the attraction strength for this copmutation. If the distance is 0 the strength is 1, and for distance < DistForMinAtt it gets scaled between 1 and MinAtt, reaching MinAtt at distance == DistForMinAtt.")]
    public float DistForMinAtt = 1f;
    [Tooltip("The distance between a particle and its attraction source affects the attraction strength for this copmutation. If the distance is 0 the strength is 1, and for distance < DistForMinAtt it gets scaled between 1 and MinAtt, reaching MinAtt at distance == DistForMinAtt.")]
    public float MinAtt = .33f;
    [Tooltip("A float value that scales the attraction of all particles to the hands of the user.")]
    public float AttToOgHands = 1f;
    public float AttToPlaneMirror = 0f;
    public float AttToPointMirror = 0f;
    public float AttToReplay = 0f;
    public float AttToTorus = 1f;
    public float AttToTorusMirror = 1f;
    public Vector2 HandDistRange = Vector2.zero;
    public Vector2 HandSpeedRange = Vector2.zero;
    #endregion
    #region Particle Group Bias
    [Header("Particle Group Bias")]
    public Vector2 HandBiasRange = new Vector2(0f, 1f);
    public int HandBiasRangeExp = 2;
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Green when using debug color in attraciton job.")]
    public Vector4 AttGroup1 = Vector4.one;
    [Range(0, 3)]
    public int HandBiasG1 = 0;
    [Tooltip("x and y value determine the attraction for each particle in that group towards the left and right hand respectively. Red when using debug color in attraciton job.")]
    public Vector4 AttGroup2 = Vector4.one;
    [Range(0, 3)]
    public int HandBiasG2 = 1;
    public Vector4 AttGroup3 = Vector4.one;
    [Range(0, 3)]
    public int HandBiasG3 = 2;
    public Vector4 AttGroup4 = Vector4.one;
    [Range(0, 3)]
    public int HandBiasG4 = 3;
    #endregion
    #region Positions
    [Header("Positions")]
    [Tooltip("The min and max size step a particle can take in the array that holds all mesh positions. 0-0 = particles stick to one position. 1-1 = particles always progress 1 position each update. 0-2 = particles might stay in place, move ahead one position or 2 positions in one update.")]
    public Vector2 IndexStepSizeRange = Vector2.zero;
    [Tooltip("Determines the min and max interpolation between the relative positions on the mesh and the joint. 0 = full mesh, 1 = full joint")]
    public Vector2 PosOffsetRange = new Vector2(0f, .7f);
    [HideInInspector]
    public Vector2 StretchFactorRange = new Vector2(0f, 1f);
    [HideInInspector]
    public int StretchFactorExponent = 2;
    [HideInInspector]
    public float StretchMax = 1f;
    public float MirrorDistance = .5f; // Distance from the camera to the virtual mirror plane
    #endregion
    #region Torus
    public Vector3 Orientation = new Vector3(90, 0, 0);
    public float RotationSpeed = 45f;
    public Vector2 RadiusRange = Vector2.zero;
    public float MajorWraps = 152;
    #endregion
    #region Particles
    [Header("Particles")]
    [Tooltip("Restart of the runtime jobs (button in the inspector) required to apply a change here while in play mode.")]
    public int ParticlesPerHand = 35000;
    [Tooltip("The min and max values for aprticle size, max reached at 0 distance from joint, min reached at DistanceForMinSize.")]
    public Vector2 ParticleSizeRange = new Vector2(.003f, .008f);
    [Tooltip("The distance between particle and joint at which the particle size reaches ParticleSizeRange.x")]
    public float DistanceForMinSize = .008f;
    [Tooltip("The linear interpolation factor for size change in one update.")]
    public float SizeLerp = .05f;
    [Tooltip("Restart of the runtime jobs (button in the inspector) required to apply a change here while in play mode.")]
    public Material ParticleMaterial;
    #endregion
    #region Color
    [Header("Color")]
    public bool UseDebugColors = false;
    private Color BaseColor = Color.blue;
    public Gradient BaseColorTimeGradient;
    public float ColorTimeGradientUpdateSpeed = 3.14f;
    [Tooltip("The linear interpolation factor for color change in one opdate step.")]
    public float ColorLerp = .05f;
    public float Alpha = .7f;
    public bool DisplayOculusHands = false;
    #endregion
    #endregion

    #region Internal Variables
    #region Attraction Job
    private ParticleSystem particleSys;
    private ParticleSystemRenderer particleRenderer;
    #region Job Handle
    private AttractionJob attractJob;
    private JobHandle attractJobHandle;
    #endregion
    #region Attraction
    private Transform LSpeedJoint;
    private Vector3 PrevLSpeedJointPos;
    private Transform RSpeedJoint;
    private Vector3 PrevRSpeedJointPos;
    private Transform L2SpeedJoint;
    private Vector3 PrevL2SpeedJointPos;
    private Transform R2SpeedJoint;
    private Vector3 PrevR2SpeedJointPos;
    #endregion
    #region Particle Group Bias
    private float[] HandBias;
    private Vector2 LastGroupBiasRange = Vector2.zero;
    private float LastGroupBiasRangeExp = .1f;
    #endregion
    #region Positions
    private Vector2 LastIndexStepSizeRange = Vector2.zero;
    private Vector2 LastStretchFactorRange = Vector2.zero;
    public float LastStretchFactorExponent = 1f;
    private float[] StretchFactorIncrease;
    private Vector3 stretchPlaneNormal;
    private Vector3 stretchPlanePosition;
    private Vector3 mirrorPlaneNormalRotated;
    private Transform MirrorPoint;
    #endregion
    #region Torus
    private Vector3 currentOrientation;
    private Vector3 prevOrientation;
    #endregion
    #endregion
    #region Update Mesh Jobs
    private PseudoMeshCreator PseudoMeshCreator;
    #region Job Handle
    private JobHandle updateMeshJobHandleL;
    private UpdateMeshJob updateMeshJobL;

    private JobHandle updateMeshJobHandleR;
    private UpdateMeshJob updateMeshJobR;

    private JobHandle updateMeshJobHandleL2;
    private UpdateMeshJob updateMeshJobL2;

    private JobHandle updateMeshJobHandleR2;
    private UpdateMeshJob updateMeshJobR2;
    #endregion
    #region Hands
    #region References
    private GameObject hands;
    private HandVisual LeftHandVisual;
    private HandVisual RightHandVisual;
    private ReplayCapture replay;
    private List<Transform> LJoints = new List<Transform>();
    private List<Transform> RJoints = new List<Transform>();
    private List<Transform> L2Joints = new List<Transform>();
    private List<Transform> R2Joints = new List<Transform>();
    private SkinnedMeshRenderer leftHandMesh;
    private SkinnedMeshRenderer rightHandMesh;
    #endregion
    #region Helper Array
    private List<Vector3> LeftHandRelativePositions = new List<Vector3>();
    private List<Vector3> RightHandRelativePositions = new List<Vector3>();
    private List<int> LeftHandJointIndices = new List<int>();
    private List<int> RightHandJointIndices = new List<int>();
    #endregion
    #endregion
    #region Position Offsets
    private int MeshPositionOffsetsIndex = 0;
    private Vector2 LastPositionOffsetRange = Vector2.zero;
    #endregion
    #endregion
    #region Utility
    // To make sure we can stop jobs from running before properly set up, has to do with the use of OnParticleUpdateJobScheduled
    [HideInInspector]
    public bool RunJobs = false;
    private bool isEvenFrame = true;
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
        #region Mirrors
        MirrorPoint = GameObject.Find("Mirror Point").transform;
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
        await InitializeCoroutine();
    }
    public void StopTheOtherFactor()
    {
        RunJobs = false;
        this.gameObject.SetActive(false);
    }
    #endregion
    private async Task InitializeCoroutine()
    {
        #region Fetch Pseudo Mesh
        FetchPseudoMesh();
        await Task.Yield();
        // somehow it needs this two times, no idea why
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
        //StartCoroutine(IncreaseIndexStepSizeY(4f));
        StartCoroutine(IncreaseAttractionStrength(.01f));
    }
    #region Fetch Pseudo Mesh
    private void FetchPseudoMesh()
    {
        LJoints = PseudoMeshCreator.LeftHandJoints;
        RJoints = PseudoMeshCreator.RightHandJoints;
        L2Joints = replay.leftHandJointsOutput;
        R2Joints = replay.rightHandJointsOutput;
        LSpeedJoint = LJoints[4];
        RSpeedJoint = RJoints[4];
        L2SpeedJoint = L2Joints[4];
        R2SpeedJoint = R2Joints[4];
        (LeftHandJointIndices, LeftHandRelativePositions) = PseudoMeshCreator.ResizeListsPreservingPercentages(ParticlesPerHand, "left");
        (RightHandJointIndices, RightHandRelativePositions) = PseudoMeshCreator.ResizeListsPreservingPercentages(ParticlesPerHand, "right");    
    }
    #endregion
    # region Wait For Hands Close To HMD
    public async Task WaitForHandsCloseToHMD(float targetDistance)
    {
        var leftMiddle1Joint = LJoints.FirstOrDefault(j => j.name == "b_l_middle1");
        var rightMiddle1Joint = RJoints.FirstOrDefault(j => j.name == "b_r_middle1");
        Camera mainCamera = Camera.main;
        
        // Initially assume both hands are not within the target distance
        bool isLeftHandClose = false;
        bool isRightHandClose = false;
        
        // Continue checking until both hands are within the target distance
        while (!isLeftHandClose || !isRightHandClose)
        {
            // Calculate distances from the HMD to each hand's middle1 joint
            float distanceToLeftHand = Vector3.Distance(leftMiddle1Joint.position, mainCamera.transform.position);
            float distanceToRightHand = Vector3.Distance(rightMiddle1Joint.position, mainCamera.transform.position);

            // Update flags based on whether each hand is within the target distance
            isLeftHandClose = distanceToLeftHand <= targetDistance;
            isRightHandClose = distanceToRightHand <= targetDistance;

            // Wait for a short interval before checking again to avoid tight looping
            await Task.Delay(100); // Adjust the delay as needed
        }
        
        // Both hands are now within the target distance from the HMD

        Vector3 forwardHorizontal = new Vector3(mainCamera.transform.forward.x, 0, mainCamera.transform.forward.z).normalized;
        stretchPlaneNormal = forwardHorizontal;

        // Rotate the forwardHorizontal vector by 90 degrees around the Y axis to split the view into left and right
        Quaternion rotation = Quaternion.Euler(0, 90, 0); // Create a 90-degree rotation around the Y axis
        Vector3 rotatedForward = rotation * forwardHorizontal; // Apply the rotation to the forwardHorizontal vector

        stretchPlanePosition = mainCamera.transform.position + mainCamera.transform.forward * MirrorDistance;
        mirrorPlaneNormalRotated = rotatedForward;
        Vector3  MirrorPointPosition = stretchPlanePosition;
        MirrorPointPosition.y = 1.5f;
        MirrorPoint.position = MirrorPointPosition;
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
            startSize = ParticleSizeRange.x,
            startLifetime = 3600f, // 1 hour
        };

        Camera mainCamera = Camera.main;
        var leftMiddle1Joint = MirrorPoint;// LJoints.FirstOrDefault(j => j.name == "b_l_middle1");
        var rightMiddle1Joint = MirrorPoint;// RJoints.FirstOrDefault(j => j.name == "b_r_middle1");

        if (leftMiddle1Joint != null && rightMiddle1Joint != null)
        {
            float emissionRadius = .5f; // Radius of the sphere around the hand joints

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
        //SetMerkaba(MirrorPoint.position);
    }
    private void SetMerkaba(Vector3 centerPoint)
    {
        // Get the current particles from the particle system
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[particleSys.particleCount];
        particleSys.GetParticles(particles);

        int halfParticles = particleSys.particleCount / 2;
        float radius = .075f;

        // Directions to the centers of the six surrounding spheres in Cartesian coordinates
        Vector3[] directions = {
        new Vector3(1, 0, 0),
        new Vector3(-1, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, -1, 0),
        new Vector3(0, 0, 1),
        new Vector3(0, 0, -1)
    };

        int idx = 0;

        // Center sphere at the centerPoint
        for (; idx < halfParticles / 7; idx++)
        {
            Vector3 randomPosition = UnityEngine.Random.onUnitSphere * radius;
            Vector3 finalPosition = randomPosition + centerPoint;  // Offset by the center point

            particles[idx].position = finalPosition;
            particles[idx].velocity = Vector3.zero;
        }

        // Six surrounding spheres
        for (int d = 0; d < directions.Length; d++)
        {
            Vector3 dir = directions[d];
            Vector3 sphereCenter = centerPoint + dir * radius * 2;  // Offset by the center point

            for (int i = 0; i < halfParticles / 7; i++, idx++)
            {
                Vector3 randomPosition = UnityEngine.Random.onUnitSphere * radius;
                Vector3 finalPosition = randomPosition + sphereCenter;

                particles[idx].position = finalPosition;
                particles[idx].velocity = Vector3.zero;
            }
        }

        // Merkaba
        float merkabaScale = 0.2f;  // Scale for the Merkaba

        // Coordinates for a single tetrahedron, scaled by the merkabaScale and offset by the center point
        Vector3[] tetrahedronVertices = new Vector3[]
        {
        new Vector3(1 * merkabaScale, 0, -1 / Mathf.Sqrt(2) * merkabaScale) + centerPoint,
        new Vector3(-1 * merkabaScale, 0, -1 / Mathf.Sqrt(2) * merkabaScale) + centerPoint,
        new Vector3(0, 1 * merkabaScale, 1 / Mathf.Sqrt(2) * merkabaScale) + centerPoint,
        new Vector3(0, -1 * merkabaScale, 1 / Mathf.Sqrt(2) * merkabaScale) + centerPoint
        };

        for (; idx < particles.Length; idx++)
        {
            bool isUpward = UnityEngine.Random.Range(0f, 1f) > 0.5f;
            int indexA = UnityEngine.Random.Range(0, tetrahedronVertices.Length);
            int indexB = (indexA + UnityEngine.Random.Range(1, tetrahedronVertices.Length)) % tetrahedronVertices.Length;
            int indexC = (indexB + UnityEngine.Random.Range(1, tetrahedronVertices.Length)) % tetrahedronVertices.Length;

            Vector3 pointA = tetrahedronVertices[indexA];
            Vector3 pointB = tetrahedronVertices[indexB];
            Vector3 pointC = tetrahedronVertices[indexC];

            float u = UnityEngine.Random.Range(0f, 1f);
            float v = UnityEngine.Random.Range(0f, 1f);

            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }

            float w = 1f - u - v;

            Vector3 position = pointA * u + pointB * v + pointC * w;
            if (!isUpward)
            {
                position = Quaternion.Euler(180, 0, 0) * (position - centerPoint) + centerPoint; // Rotate around the center point
            }

            particles[idx].position = position;
            particles[idx].velocity = Vector3.zero;
        }

        // Apply the changes to the particle system
        particleSys.SetParticles(particles);
    }
    #endregion
    #region Initialize Jobs
    private void InitializeRuntimeJobs()
    {
        int totalParticles = ParticlesPerHand * 2;
        #region AttractionJob
        #region Initialize Arrays
        #region Particle Group Bias
        NativeArray<Vector4> AttGroups = new NativeArray<Vector4>(totalParticles, Allocator.Persistent);
        NativeArray<Vector4> HandBias = new NativeArray<Vector4>(totalParticles, Allocator.Persistent);
        #endregion
        #region Positions
        #region Mesh Positions
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
        #endregion
        #endregion
        #endregion
        attractJob = new AttractionJob
        {
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
            AttractionExponentDivisor = 2 * AttractionStrength * AttractionStrength,
            HandLSpeed = HandSpeedRange.x,
            HandRSpeed = HandSpeedRange.x,
            #endregion
            #region Particle Group Bias
            AttGroups = AttGroups,
            HandBias = HandBias,
            #endregion
            #region Positions
            #region Mesh
            #region Mesh Positions
            MeshPositionsL = MeshPositionsL,
            MeshPositionsR = MeshPositionsR,
            MeshPositionsL2 = MeshPositionsRL,
            MeshPositionsR2 = MeshPositionsRR,
            #endregion
            #region Indices
            MeshIndicesL = MeshIndicesL,
            MeshIndicesR = MeshIndicesR,
            MeshIndicesL2 = MeshIndicesRL,
            MeshIndicesR2 = MeshIndicesRR,
            IndexStepSizes = IndexStepSizes,
            IndexStepsSizeIndex = 0,
            #endregion
            #endregion
            #region Mirrors
            MirrorPoint = MirrorPoint.position,
            MirrorPlaneNormal = mirrorPlaneNormalRotated,
            #endregion
            #region Torus
            TorusIndex = 0,
            Rotation = quaternion.Euler(math.radians(Orientation.x), math.radians(Orientation.y), math.radians(Orientation.z)),
            RadiusRange = RadiusRange,
            MajorWraps = MajorWraps,
            #endregion
            #endregion
            #region Size
            ParticleSizeRange = ParticleSizeRange,
            DistanceForMinSize = DistanceForMinSize,
            SizeLerp = SizeLerp,
            #endregion
            #region Color
            UseDebugColors = UseDebugColors == true ? 1 : 0,
            BaseColor = BaseColor,
            ColorLerp = ColorLerp,
            Alpha = 0f,
            #endregion
        };
        #endregion
        #region Update Mesh Jobs
        #region World Position Offsets
        NativeArray<float> MeshPositionOffsets = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region Update Mesh Job L
        #region Joints
        NativeArray<Vector3> JointPositionsL = new NativeArray<Vector3>(LJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> JointRotationsL = new NativeArray<quaternion>(LJoints.Count, Allocator.Persistent);
        NativeArray<int> JointToParticleMapL = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < LeftHandJointIndices.Count; i++)
        {
            JointToParticleMapL[i] = LeftHandJointIndices[i];
        }
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
            JointRotations = JointRotationsL,
            JointToParticleMap = JointToParticleMapL,
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
            StretchPlaneNormal = stretchPlaneNormal,
            #endregion
        };
        #endregion
        #region Update Mesh Job R
        #region Joints
        NativeArray<Vector3> JointPositionsR = new NativeArray<Vector3>(RJoints.Count, Allocator.Persistent);
        NativeArray<quaternion> JointRotationsR = new NativeArray<quaternion>(RJoints.Count, Allocator.Persistent);
        NativeArray<int> JointToParticleMapR = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < RightHandJointIndices.Count; i++)
        {
            JointToParticleMapR[i] = RightHandJointIndices[i];
        }
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
            JointRotations = JointRotationsR,
            JointToParticleMap = JointToParticleMapR,
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
            StretchPlaneNormal = stretchPlaneNormal,
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
        #region StretchLM
        NativeArray<float> StretchFactorLM = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < StretchFactorLM.Length; i++)
        {
            StretchFactorLM[i] = 0;
        }
        #endregion
        updateMeshJobL2 = new UpdateMeshJob
        {
            #region Joints
            JointPositions = JointPositionsLM,
            JointRotations = JointRotationsLM,
            JointToParticleMap = JointToParticleMapL,
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
            StretchPlaneNormal = stretchPlaneNormal,
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
        updateMeshJobR2 = new UpdateMeshJob
        {
            #region Joints
            JointPositions = JointPositionsRM,
            JointRotations = JointRotationsRM,
            JointToParticleMap = JointToParticleMapR,
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
            StretchPlaneNormal = stretchPlaneNormal,
            #endregion
        };
        #endregion
        #endregion
        for (int i = 0; i < ParticlesPerHand; i++)
        {
            // array is shared by all mesh jobs but needs to accessed by one
            updateMeshJobL.MeshPositionOffsets[i] = Mathf.RoundToInt(UnityEngine.Random.Range(PosOffsetRange.x, PosOffsetRange.y));
        }
    }
    #endregion
    #region Slow Start Coroutines (deactivated)
    IEnumerator IncreaseIndexStepSizeY(float interval)
    {
        yield return new WaitForSeconds(30);

        float increment = 1f; // Start with an increment of 1

        while (IndexStepSizeRange.y < ParticlesPerHand) // Continue until the max value is reached
        {
            yield return new WaitForSeconds(interval); // Wait for the specified interval

            // Increase the y value of the IndexStepSizeRange vector by the current increment
            IndexStepSizeRange.y += increment;

            // Clamp the y value to ensure it does not exceed the maximum value
            IndexStepSizeRange.y = Mathf.Min(IndexStepSizeRange.y, ParticlesPerHand);

            // Double the increment for the next iteration
            increment *= 2;
        }
    }
    IEnumerator IncreaseAttractionStrength(float interval)
    {
        float increment = .001f; // Start with an increment of 1
        float FinalAttStr = AttractionStrength;
        AttractionStrength = 0;

        while (AttractionStrength < FinalAttStr) // Continue until the max value is reached
        {
            yield return new WaitForSeconds(interval); // Wait for the specified interval

            AttractionStrength += increment;

            AttractionStrength = Mathf.Min(FinalAttStr, AttractionStrength);
        }
    }
    #endregion
    #endregion
    #region Runtime Updates
    void OnParticleUpdateJobScheduled()
    {
        if (RunJobs && attractJobHandle.IsCompleted && updateMeshJobHandleL.IsCompleted && updateMeshJobHandleR.IsCompleted && updateMeshJobHandleL2.IsCompleted && updateMeshJobHandleR2.IsCompleted)
        {
            #region Complete Jobs
            attractJobHandle.Complete();
            updateMeshJobHandleL.Complete();
            updateMeshJobHandleR.Complete();
            updateMeshJobHandleL2.Complete();
            updateMeshJobHandleR2.Complete();
            #endregion
            if (isEvenFrame)
            {
                #region Update Mesh Jobs
                if (LastPositionOffsetRange != PosOffsetRange)
                {
                    for (int i = 0; i < updateMeshJobL.MeshPositionOffsets.Length; i++)
                    {
                        // array is shared by all mesh jobs but needs to accessed by one
                        updateMeshJobL.MeshPositionOffsets[i] = Mathf.RoundToInt(UnityEngine.Random.Range(PosOffsetRange.x, PosOffsetRange.y));
                    }
                }
                LastPositionOffsetRange = PosOffsetRange;
                MeshPositionOffsetsIndex = MeshPositionOffsetsIndex < updateMeshJobL.MeshPositionOffsets.Length ? MeshPositionOffsetsIndex + 1 : 0;
                #region Stretch Factor Min Max
                if (LastStretchFactorRange != StretchFactorRange || StretchFactorExponent != LastStretchFactorExponent)
                {
                    StretchFactorIncrease = new float[ParticlesPerHand];
                    for (int i = 0; i < updateMeshJobL.StretchFactor.Length; i++)
                    {
                        float linearRandom = UnityEngine.Random.Range(StretchFactorRange.x, StretchFactorRange.y);
                        StretchFactorIncrease[i] = Mathf.Pow(linearRandom, StretchFactorExponent);
                        updateMeshJobL.StretchFactor[i] = 0f;
                        updateMeshJobR.StretchFactor[i] = 0f;
                    }
                }
                LastStretchFactorRange = StretchFactorRange;
                LastStretchFactorExponent = StretchFactorExponent;
                #endregion
                #region Update Mesh Job L
                #region Update Joint Positions
                for (int i = 0; i < updateMeshJobL.JointPositions.Length; i++)
                {
                    updateMeshJobL.JointPositions[i] = LJoints[i].position;
                    updateMeshJobL.JointRotations[i] = LJoints[i].rotation;
                }
                #endregion
                #region Update PositionOffsetsIndex
                updateMeshJobL.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobL.StretchFactor.Length; i++)
                {
                    Vector3 toTransform = updateMeshJobL.DynamicMeshPositions[i] - stretchPlanePosition;
                    float distanceToPlane = math.length(toTransform);
                    updateMeshJobL.StretchFactor[i] = updateMeshJobL.StretchFactor[i] < distanceToPlane ? updateMeshJobL.StretchFactor[i] + StretchFactorIncrease[i] : distanceToPlane;
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
                #region Update PositionOffsetsIndex
                updateMeshJobR.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobR.StretchFactor.Length; i++)
                {
                    Vector3 toTransform = updateMeshJobR.DynamicMeshPositions[i] - stretchPlanePosition;
                    float distanceToPlane = math.length(toTransform);
                    updateMeshJobR.StretchFactor[i] = updateMeshJobR.StretchFactor[i] < distanceToPlane ? updateMeshJobR.StretchFactor[i] + StretchFactorIncrease[i] : distanceToPlane;
                }
                #endregion
                // This should not be necessary but somehow the first timing is weird so without it the job tries to execute before the arrays are assigned and that produces a null reference.
                if (updateMeshJobR.JointPositions.Length > 0) updateMeshJobHandleR = updateMeshJobR.Schedule(updateMeshJobR.BaseMeshPositions.Length, 1024);
                #endregion
                #region Update Mesh Job L2
                #region Update Joint Positions
                for (int i = 0; i < updateMeshJobL2.JointPositions.Length; i++)
                {
                    updateMeshJobL2.JointPositions[i] = L2Joints[i].position;
                    updateMeshJobL2.JointRotations[i] = L2Joints[i].rotation;
                }
                #endregion
                #region Update PositionOffsetsIndex
                updateMeshJobL2.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobL2.StretchFactor.Length; i++)
                {
                        Vector3 toTransform = updateMeshJobL2.DynamicMeshPositions[i] - stretchPlanePosition;
                        float distanceToPlane = math.length(toTransform);
                        updateMeshJobL2.StretchFactor[i] = updateMeshJobL2.StretchFactor[i] > -distanceToPlane ? updateMeshJobL2.StretchFactor[i] - StretchFactorIncrease[i] : -distanceToPlane;
                }
                #endregion
                if (updateMeshJobL2.JointPositions.Length > 0) updateMeshJobHandleL2 = updateMeshJobL2.Schedule(updateMeshJobL2.BaseMeshPositions.Length, 1024);
                #endregion
                #region Update Mesh Job R2
                #region Update Joint Positions
                for (int i = 0; i < updateMeshJobR2.JointPositions.Length; i++)
                {
                    updateMeshJobR2.JointPositions[i] = R2Joints[i].position;
                    updateMeshJobR2.JointRotations[i] = R2Joints[i].rotation;
                }
                #endregion
                #region Update PositionOffsetsIndex
                updateMeshJobR2.MeshPositionOffsetsIndex = MeshPositionOffsetsIndex;
                #endregion
                #region Update Stretch
                for (int i = 0; i < updateMeshJobR2.StretchFactor.Length; i++)
                {
                    Vector3 toTransform = updateMeshJobR2.DynamicMeshPositions[i] - stretchPlanePosition;
                    float distanceToPlane = math.length(toTransform);
                    updateMeshJobR2.StretchFactor[i] = updateMeshJobR2.StretchFactor[i] > -distanceToPlane ? updateMeshJobR2.StretchFactor[i] - StretchFactorIncrease[i] : -distanceToPlane;
                }
                #endregion
                // This should not be necessary but somehow the first timing is weird so without it the job tries to execute before the arrays are assigned and that produces a null reference.
                if (updateMeshJobR2.JointPositions.Length > 0) updateMeshJobHandleR2 = updateMeshJobR2.Schedule(updateMeshJobR2.BaseMeshPositions.Length, 1024);
                #endregion            
                #endregion
            }
            else
            {
                #region Attraction Job
                UpdateAttractionJob();
                attractJobHandle = attractJob.ScheduleBatch(particleSys, 1024);
                #endregion
            }
            UpdateParticleMaterialAndHandVisual();
            isEvenFrame = !isEvenFrame;
        }
    }
    private void UpdateAttractionJob()
    {
        #region Attraction
        attractJob.AttractionStrength = AttractionStrength;
        attractJob.VelocityLerp = VelocityLerp;
        attractJob.DistForMinAtt = DistForMinAtt;
        attractJob.MinAtt = MinAtt;
        attractJob.AttToOgHands = AttToOgHands;
        attractJob.AttToPlaneMirror = AttToPlaneMirror;
        attractJob.AttToPointMirror = AttToPointMirror;
        attractJob.AttToReplay = AttToReplay;
        attractJob.AttToTorus = AttToTorus;
        attractJob.AttToTorusMirror = AttToTorusMirror;
        attractJob.AttractionExponentDivisor = 2 * AttractionStrength * AttractionStrength;

        #region Hand Speed
        // Calculate HandLSpeed based on distance and scale it
        float LDistance = math.length(PrevLSpeedJointPos - LSpeedJoint.position);
        LDistance = math.clamp(LDistance, HandDistRange.x, HandDistRange.y); // Clamp the distance within min and max
        float normalizedLDistance = (LDistance - HandDistRange.x) / (HandDistRange.y - HandDistRange.x); // Normalize
        attractJob.HandLSpeed = math.lerp(attractJob.HandLSpeed, math.lerp(HandSpeedRange.x, HandSpeedRange.y, normalizedLDistance), .1f); // Scale speed
        PrevLSpeedJointPos = LSpeedJoint.position; // Update previous position
        //Debug.LogWarning("attractJob.HandLSpeed : " + attractJob.HandLSpeed);

        // Calculate HandRSpeed based on distance and scale it
        float RDistance = math.length(PrevRSpeedJointPos - RSpeedJoint.position);
        RDistance = math.clamp(RDistance, HandDistRange.x, HandDistRange.y); // Clamp the distance within min and max
        float normalizedRDistance = (RDistance - HandDistRange.x) / (HandDistRange.y - HandDistRange.x); // Normalize
        attractJob.HandRSpeed = math.lerp(attractJob.HandRSpeed, math.lerp(HandSpeedRange.x, HandSpeedRange.y, normalizedRDistance), .1f); // Scale speed
        PrevRSpeedJointPos = RSpeedJoint.position; // Update previous position
        //Debug.LogWarning("attractJob.HandRSpeed: " + attractJob.HandRSpeed);

        // Calculate HandL2Speed based on distance and scale it
        float L2Distance = math.length(PrevL2SpeedJointPos - L2SpeedJoint.position);
        L2Distance = math.clamp(L2Distance, HandDistRange.x, HandDistRange.y); // Clamp the distance within min and max
        float normalizedL2Distance = (L2Distance - HandDistRange.x) / (HandDistRange.y - HandDistRange.x); // Normalize
        attractJob.HandL2Speed = math.lerp(attractJob.HandL2Speed, math.lerp(HandSpeedRange.x, HandSpeedRange.y, normalizedL2Distance), .1f); // Scale speed
        PrevL2SpeedJointPos = L2SpeedJoint.position; // Update previous position

        // Calculate HandR2Speed based on distance and scale it
        float R2Distance = math.length(PrevR2SpeedJointPos - R2SpeedJoint.position);
        R2Distance = math.clamp(R2Distance, HandDistRange.x, HandDistRange.y); // Clamp the distance within min and max
        float normalizedR2Distance = (R2Distance - HandDistRange.x) / (HandDistRange.y - HandDistRange.x); // Normalize
        attractJob.HandR2Speed = math.lerp(attractJob.HandR2Speed, math.lerp(HandSpeedRange.x, HandSpeedRange.y, normalizedR2Distance), .1f); // Scale speed
        PrevR2SpeedJointPos = R2SpeedJoint.position; // Update previous position
        #endregion
        #endregion
        #region Particle Group Bias
        UpdateHandBias();
        #endregion
        #region Positions
        #region Mesh
        #region Mesh Positions
        attractJob.MeshPositionsL = updateMeshJobL.DynamicMeshPositions;
        attractJob.MeshPositionsR = updateMeshJobR.DynamicMeshPositions;
        attractJob.MeshPositionsL2 = updateMeshJobL2.DynamicMeshPositions;
        attractJob.MeshPositionsR2 = updateMeshJobR2.DynamicMeshPositions;
        #endregion
        #region Indices
        if (LastIndexStepSizeRange != IndexStepSizeRange)
        {
            for (int i = 0; i < attractJob.IndexStepSizes.Length; i++)
            {
                attractJob.IndexStepSizes[i] = Mathf.RoundToInt(UnityEngine.Random.Range(IndexStepSizeRange.x, IndexStepSizeRange.y));
            }
        }
        LastIndexStepSizeRange = IndexStepSizeRange;
        attractJob.IndexStepsSizeIndex = attractJob.IndexStepsSizeIndex + attractJob.IndexStepSizes[attractJob.IndexStepsSizeIndex] < attractJob.IndexStepSizes.Length ? attractJob.IndexStepsSizeIndex + attractJob.IndexStepSizes[attractJob.IndexStepsSizeIndex] : 0;
        #endregion
        #endregion
        #region Mirrors
        attractJob.MirrorPoint = MirrorPoint.position;
        #endregion
        #region Torus
        attractJob.TorusIndex = (attractJob.TorusIndex + 1) % particleSys.particleCount;

        if (prevOrientation != Orientation)
        {
            currentOrientation = Orientation;
        }
        prevOrientation = Orientation;
        currentOrientation.y += RotationSpeed * Time.deltaTime;
        currentOrientation.y %= 360f;
        quaternion orientation = quaternion.Euler(math.radians(currentOrientation.x), math.radians(currentOrientation.y), math.radians(currentOrientation.z));
        attractJob.Rotation = orientation;

        attractJob.RadiusRange = RadiusRange;
        attractJob.MajorWraps = MajorWraps;
        #endregion
        #endregion
        #region Size
        attractJob.ParticleSizeRange = ParticleSizeRange;
        attractJob.DistanceForMinSize = DistanceForMinSize;
        attractJob.SizeLerp = SizeLerp;
        #endregion
        #region Color
        // Use Time.time multiplied by changeSpeed to get a value that increases over time
        // The sine function will oscillate this value between -1 and 1
        float sineValue = Mathf.Sin(Time.time * ColorTimeGradientUpdateSpeed);

        // Map the sine value to a 0-1 range
        float gradientTime = (sineValue + 1) / 2;

        // Set the base color based on the current gradientTime and the gradient
        BaseColor = BaseColorTimeGradient.Evaluate(gradientTime);

        attractJob.UseDebugColors = UseDebugColors == true ? 1 : 0;
        attractJob.BaseColor = BaseColor;
        attractJob.ColorLerp = ColorLerp;
        attractJob.Alpha = Alpha;
        #endregion
    }
    private void UpdateParticleMaterialAndHandVisual()
    {
        particleRenderer.material = ParticleMaterial;
        leftHandMesh.gameObject.SetActive(DisplayOculusHands);
        rightHandMesh.gameObject.SetActive(DisplayOculusHands);
    }
    private void UpdateHandBias()
    {
        if (LastGroupBiasRange != HandBiasRange || LastGroupBiasRangeExp != HandBiasRangeExp)
        {
            int totalParticles = ParticlesPerHand * 2;
            HandBias = new float[totalParticles];
            for (int i = 0; i < totalParticles; i++)
            {
                float linearRandom = UnityEngine.Random.Range(HandBiasRange.x, HandBiasRange.y);
                HandBias[i] = Mathf.Pow(linearRandom, HandBiasRangeExp);
            }
        }
        LastGroupBiasRange = HandBiasRange;
        LastGroupBiasRangeExp = HandBiasRangeExp;
        
        Vector4 attractionVector = Vector4.one;
        Vector4 perParticleScalingVector = Vector4.one;

        int quarterLength = attractJob.AttGroups.Length / 4;

        for (int i = 0; i < attractJob.AttGroups.Length; i++)
        {
            // Reset perParticleScalingVector for each particle group
            perParticleScalingVector = new Vector4(HandBias[i], HandBias[i], HandBias[i], HandBias[i]);

            // Determine the index for this quarter
            int index = i / quarterLength;

            // Set the attractionVector based on the current quarter
            if (i < quarterLength)
            {
                // First quarter
                attractionVector = AttGroup1;
                perParticleScalingVector[HandBiasG1] = 1;
            }
            else if (i < quarterLength * 2)
            {
                // Second quarter
                attractionVector = AttGroup2;
                perParticleScalingVector[HandBiasG2] = 1;
            }
            else if (i < quarterLength * 3)
            {
                // Third quarter
                attractionVector = AttGroup3;
                perParticleScalingVector[HandBiasG3] = 1;
            }
            else
            {
                // Fourth quarter
                attractionVector = AttGroup4;
                perParticleScalingVector[HandBiasG4] = 1;
            }

            // Assign the calculated vectors to the job
            attractJob.AttGroups[i] = attractionVector;
            attractJob.HandBias[i] = perParticleScalingVector;
        }
    }
    #endregion
    #region Jobs
    #region Attraction Job
    [BurstCompile]
    struct AttractionJob : IJobParticleSystemParallelForBatch
    {
        #region Job Variables
        #region Attraction
        [ReadOnly] public float AttractionStrength;
        [ReadOnly] public float VelocityLerp;
        [ReadOnly] public float DistForMinAtt;
        [ReadOnly] public float MinAtt;
        [ReadOnly] public float AttToOgHands;
        [ReadOnly] public float AttToPlaneMirror;
        [ReadOnly] public float AttToPointMirror;
        [ReadOnly] public float AttToReplay;
        [ReadOnly] public float AttToTorus;
        [ReadOnly] public float AttToTorusMirror;
        [ReadOnly] public float AttractionExponentDivisor;
        [ReadOnly] public float HandLSpeed;
        [ReadOnly] public float HandRSpeed;
        [ReadOnly] public float HandL2Speed;
        [ReadOnly] public float HandR2Speed;
        #endregion
        #region Particle Group Bias
        [ReadOnly] public NativeArray<Vector4> AttGroups;
        [ReadOnly] public NativeArray<Vector4> HandBias;
        #endregion
        #region Positions
        #region Mesh
        #region Mesh Positions
        [ReadOnly] public NativeArray<Vector3> MeshPositionsL;
        [ReadOnly] public NativeArray<Vector3> MeshPositionsR;
        [ReadOnly] public NativeArray<Vector3> MeshPositionsL2;
        [ReadOnly] public NativeArray<Vector3> MeshPositionsR2;
        #endregion
        #region Indices
        public NativeArray<int> MeshIndicesL;
        public NativeArray<int> MeshIndicesR;
        public NativeArray<int> MeshIndicesL2;
        public NativeArray<int> MeshIndicesR2;
        [ReadOnly] public NativeArray<int> IndexStepSizes;
        [ReadOnly] public int IndexStepsSizeIndex;
        #endregion
        #endregion
        #region Mirrors
        [ReadOnly] public Vector3 MirrorPoint;
        [ReadOnly] public Vector3 MirrorPlaneNormal;
        #endregion
        #region Torus
        [ReadOnly] public int TorusIndex;
        [ReadOnly] public quaternion Rotation;
        [ReadOnly] public Vector2 RadiusRange;
        [ReadOnly] public float MajorWraps;
        #endregion
        #endregion
        #region Size
        [ReadOnly] public Vector2 ParticleSizeRange;
        [ReadOnly] public float DistanceForMinSize;
        [ReadOnly] public float SizeLerp;
        #endregion
        #region Color
        [ReadOnly] public int UseDebugColors;
        [ReadOnly] public Color BaseColor;
        [ReadOnly] public float ColorLerp;
        [ReadOnly] public float Alpha;
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
            int totalParticles = particles.count;
            float totalAngle = 2 * math.PI * MajorWraps;
            float minorWraps = RadiusRange.y / RadiusRange.x;
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

                Vector3 velocityL = CalculateAttractionVelocity(directionToMeshL, distanceToMeshL, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].x, HandLSpeed, HandBias[particleIndex].y);
                #endregion
                #region Compute Attraction to Right Hand
                int meshPosIndexR = MeshIndicesR[particleIndex];
                MeshIndicesR[particleIndex] = (meshPosIndexR + IndexStepSizes[(particleIndex + IndexStepsSizeIndex) % MeshPositionsR.Length]) % MeshPositionsR.Length;

                Vector3 meshPosR = MeshPositionsR[meshPosIndexR];
                Vector3 directionToMeshR = meshPosR - particlePos;
                float distanceToMeshR = math.length(directionToMeshR);

                Vector3 velocityR = CalculateAttractionVelocity(directionToMeshR, distanceToMeshR, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].y, HandRSpeed, HandBias[particleIndex].x);
                #endregion

                #region Compute Attraction to Left Replay
                int meshPosIndexL2 = MeshIndicesL2[particleIndex];
                MeshIndicesL2[particleIndex] = (meshPosIndexL2 + IndexStepSizes[(particleIndex + IndexStepsSizeIndex) % MeshPositionsL2.Length]) % MeshPositionsL2.Length;

                Vector3 meshPosL2 = MeshPositionsL2[meshPosIndexL2];

                Vector3 directionToMeshL2 = meshPosL2 - particlePos;
                float distanceToMeshL2 = math.length(directionToMeshL2);

                Vector3 velocityL2 = CalculateAttractionVelocity(directionToMeshL2, distanceToMeshL2, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].z, HandL2Speed, HandBias[particleIndex].w);
                #endregion
                #region Compute Attraction to Right Replay
                int meshPosIndexR2 = MeshIndicesR2[particleIndex];
                MeshIndicesR2[particleIndex] = (meshPosIndexR2 + IndexStepSizes[(particleIndex + IndexStepsSizeIndex) % MeshPositionsR2.Length]) % MeshPositionsR2.Length;

                Vector3 meshPosR2 = MeshPositionsR2[meshPosIndexR2];

                Vector3 directionToMeshR2 = meshPosR2 - particlePos;
                float distanceToMeshR2 = math.length(directionToMeshR2);

                Vector3 velocityR2 = CalculateAttractionVelocity(directionToMeshR2, distanceToMeshR2, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].w, HandR2Speed, HandBias[particleIndex].z);
                #endregion

                #region Compute Attraction To Mirrors
                #region Mirror Plane
                #region Compute Attraction to Left Mirror Hand
                // Compute the vector from the point on the plane to the original transform's position
                Vector3 toTransform = meshPosL - MirrorPoint;

                // Project this vector onto the plane's normal to find the distance from the plane
                float distanceToPlane = Vector3.Dot(toTransform, MirrorPlaneNormal);

                // The mirrored position is the original position moved by twice the distance to the plane, along the plane's normal
                Vector3 meshPosLM = meshPosL - 2 * distanceToPlane * MirrorPlaneNormal;

                Vector3 directionToMeshLM = meshPosLM - particlePos;
                float distanceToMeshLM = math.length(directionToMeshLM);

                Vector3 velocityLM = CalculateAttractionVelocity(directionToMeshLM, distanceToMeshLM, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                AttGroups[particleIndex].x, HandLSpeed, HandBias[particleIndex].y);
                #endregion
                #region Compute Attraction to Right Mirror Hand
                toTransform = meshPosR - MirrorPoint;

                // Project this vector onto the plane's normal to find the distance from the plane
                distanceToPlane = Vector3.Dot(toTransform, MirrorPlaneNormal);

                // The mirrored position is the original position moved by twice the distance to the plane, along the plane's normal
                Vector3 meshPosRM = meshPosR - 2 * distanceToPlane * MirrorPlaneNormal;

                Vector3 directionToMeshRM = meshPosRM - particlePos;
                float distanceToMeshRM = math.length(directionToMeshRM);

                Vector3 velocityRM = CalculateAttractionVelocity(directionToMeshRM, distanceToMeshRM, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                AttGroups[particleIndex].y, HandRSpeed, HandBias[particleIndex].x);
                #endregion
                #endregion
                #region Mirror Point
                #region Compute Mirror Attraction to Left Hand
                Vector3 meshPosLPm = 2 * MirrorPoint - meshPosL;
                Vector3 directionToMeshLPm = meshPosLPm - particlePos;
                float distanceToMeshLPm = math.length(directionToMeshLPm);

                Vector3 velocityLPm = CalculateAttractionVelocity(directionToMeshLPm, distanceToMeshLPm, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].x, HandLSpeed, HandBias[particleIndex].y);
                #endregion
                #region Compute Mirror Attraction to Right Hand
                Vector3 meshPosRPm = 2 * MirrorPoint - meshPosR;
                Vector3 directionToMeshRPm = meshPosRPm - particlePos;
                float distanceToMeshRPm = math.length(directionToMeshRPm);

                Vector3 velocityRPm = CalculateAttractionVelocity(directionToMeshRPm, distanceToMeshRPm, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].y, HandRSpeed, HandBias[particleIndex].x);
                #endregion
                #region Compute Mirror Attraction to Left Mirror Hand
                Vector3 meshPosLMPm = 2 * MirrorPoint - meshPosLM;
                Vector3 directionToMeshLMPm = meshPosLMPm - particlePos;
                float distanceToMeshLMPm = math.length(directionToMeshLMPm);

                Vector3 velocityLMPm = CalculateAttractionVelocity(directionToMeshLMPm, distanceToMeshLMPm, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].x, HandLSpeed, HandBias[particleIndex].y);
                #endregion
                #region Compute Mirror Attraction to Right Hand
                Vector3 meshPosRMPm = 2 * MirrorPoint - meshPosRM;
                Vector3 directionToMeshRMPm = meshPosRMPm - particlePos;
                float distanceToMeshRMPm = math.length(directionToMeshRMPm);

                Vector3 velocityRMPm = CalculateAttractionVelocity(directionToMeshRMPm, distanceToMeshRMPm, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                AttGroups[particleIndex].y, HandRSpeed, HandBias[particleIndex].x);
                #endregion
                #endregion
                #endregion
                #region Compute Attraction to Torus
                int torusIndex = (particleIndex + TorusIndex) % totalParticles;

                float theta = torusIndex * totalAngle / totalParticles;
                float phi = torusIndex * totalAngle * minorWraps / totalParticles;

                float x = (RadiusRange.y + RadiusRange.x * math.cos(phi)) * math.cos(theta);
                float y = (RadiusRange.y + RadiusRange.x * math.cos(phi)) * math.sin(theta);
                float z = RadiusRange.x * math.sin(phi);

                Vector3 torusPos = new Vector3(x, y, z);
                torusPos = math.rotate(Rotation, torusPos);
                torusPos += MirrorPoint;
                
                Vector3 directionToTorus = torusPos - particlePos;
                float distanceToTorus = math.length(directionToTorus);

                Vector3 velocityTorus = CalculateAttractionVelocity(directionToTorus, distanceToTorus, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                1, 1.2f, 1);

                #region Compute Mirror Attraction to Torus
                Vector3 torusPosPm = 2 * MirrorPoint - torusPos;

                Vector3 directionToTorusPm = torusPosPm - particlePos;
                float distanceToTorusPm = math.length(directionToTorusPm);

                Vector3 velocityTorusPm = CalculateAttractionVelocity(directionToTorusPm, distanceToTorusPm, AttractionExponentDivisor, AttractionStrength, DistForMinAtt, MinAtt,
                                                                1, 1.2f, 1);
                #endregion
                #endregion
                #region Update Particle Velocity, Size and Color
                #region Veloctiy
                velocities[particleIndex] = math.lerp(velocities[particleIndex], (AttToOgHands        * (velocityL + velocityR)) +
                                                                                 (AttToReplay         * (velocityL2 + velocityR2)) +
                                                                                 (AttToPlaneMirror    * (velocityLM + velocityRM)) +
                                                                                 (AttToPointMirror    * (velocityLPm + velocityRPm)) + 
                                                                                 (AttToPointMirror    * AttToPlaneMirror * (velocityLMPm + velocityRMPm)) +
                                                                                 (AttToTorus          * velocityTorus) + 
                                                                                 (AttToTorusMirror    * velocityTorusPm), 
                                                                                  VelocityLerp);

                #endregion
                #region Size
                sizes[i] = ComputeParticleSize(distanceToMeshL, distanceToMeshR, 
                                               distanceToMeshLM, distanceToMeshRM, 
                                               distanceToMeshLPm, distanceToMeshRPm, 
                                               distanceToMeshLMPm, distanceToMeshRMPm, 
                                               distanceToTorus, distanceToTorusPm,
                                               DistanceForMinSize, ParticleSizeRange, SizeLerp, sizes[particleIndex]);

                #endregion
                #region Color
                //colors[particleIndex] = Color.white * ((float)particleIndex / (float)particles.count);// particleIndex < 100 ? Color.white : Color.white * .1f ;// ComputeParticleColor(velocities[particleIndex], BaseColor, particleIndex, particles.count, UseDebugColors, Alpha, ColorLerp, colors[particleIndex]);
                colors[particleIndex] = ComputeParticleColor(velocities[particleIndex], BaseColor, particleIndex, particles.count, UseDebugColors, Alpha, ColorLerp, colors[particleIndex]);
                #endregion
                #endregion
            }
        }
        #region Functions
        #region Calculate Attraction Velocity
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Vector3 CalculateAttractionVelocity(Vector3 direction, float distance, float attractionExponentDivisor, float attractionStrength, float distForMinAtt, float minAtt,
                                                           float perGroupHandScalor, float HandSpeed, float perParticleScaling)
        {
            float exponent = -math.lengthsq(direction) / attractionExponentDivisor;
            float attraction = attractionStrength * math.exp(exponent);

            // Clamp distance at distForMinAtt to ensure it does not exceed the threshold for minAtt
            float clampedDistance = math.min(distance, distForMinAtt);

            // Normalize the clamped distance between distForMaxAtt and distForMinAtt
            float distForMaxAtt = 0f;
            float normalizedDistance = math.clamp((clampedDistance - distForMaxAtt) / (distForMinAtt - distForMaxAtt), 0f, 1f);

            // Linearly interpolate between 1 (no attenuation) and minAtt based on normalized distance
            float distanceFactor = math.lerp(1f, minAtt, normalizedDistance);

            attraction *= distanceFactor;

            attraction *= HandSpeed;

            attraction *= perGroupHandScalor;

            attraction *= perParticleScaling;

            Vector3 normDirection = math.normalize(direction);
            Vector3 velocity = normDirection * attraction;
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

            //newColor = new Color(.1f,.1f,.1f);
            newColor.a = alpha;

            Color finalColor = Color.Lerp(currentColor, newColor, colorLerp);

            return finalColor;
        }
        #endregion
        #region Compute Particle Size
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static float ComputeParticleSize(
            float distanceToMeshL, float distanceToMeshR,
            float distanceToMeshLM, float distanceToMeshRM,
            float distanceToMeshLPm, float distanceToMeshRPm,
            float distanceToMeshLMPm, float distanceToMeshRMPm,
            float distanceToTorus, float distanceToTorusPM,
            float distanceForMinSize, Vector2 particleSizeMinMax,
            float sizeLerp, float currentSize)
        {
            // Find the least distance among all the given distances efficiently
            float leastDistance = math.min(
                math.min(
                    math.min(math.min(distanceToMeshL, distanceToMeshR), math.min(distanceToMeshLM, distanceToMeshRM)),
                    math.min(math.min(distanceToMeshLPm, distanceToMeshRPm), math.min(distanceToMeshLMPm, distanceToMeshRMPm))
                ),
                    math.min(distanceToTorus, distanceToTorusPM)
            );

            // Normalize the distance (0 at distanceForMinSize or beyond, 1 at distance 0)
            float normalizedDistance = math.clamp(1f - (leastDistance / distanceForMinSize), 0f, 1f);

            // Lerp between min and max particle size based on the normalized distance
            float targetSize = math.lerp(particleSizeMinMax.x, particleSizeMinMax.y, normalizedDistance);

            // Lerp between the current size and the target size
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
        [ReadOnly] public NativeArray<quaternion> JointRotations;
        [ReadOnly] public NativeArray<int> JointToParticleMap;
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
        #region Stretch
        [ReadOnly] public NativeArray<float> StretchFactor;
        [ReadOnly] public Vector3 StretchPlaneNormal;
        #endregion
        #endregion
        public void Execute(int index)
        {
            // Calculate position offset for current particle based on noise array
            float positionOffset = MeshPositionOffsets[(index + MeshPositionOffsetsIndex) % MeshPositionOffsets.Length];

            // Get the relative position, joint index, and joint data for the current particle
            Vector3 baseMeshPosition = BaseMeshPositions[index];
            int jointIndex = JointToParticleMap[index];
            Vector3 jointPosition = JointPositions[jointIndex];
            Quaternion jointRotation = JointRotations[jointIndex];

            // Compute the final world position for the particle and store it
            Vector3 DynamicMeshPosition = ComputeWorldPosition(jointPosition, jointRotation, baseMeshPosition, positionOffset);

            DynamicMeshPositions[index] = DynamicMeshPosition + StretchFactor[index] * StretchPlaneNormal;
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
    #region Complete Jobs And Dispose Native Arrays On Disable
    void OnDisable()
    {
        #region Complete Jobs
        // Ensure all jobs are completed before disposing of the NativeArrays
        attractJobHandle.Complete();
        updateMeshJobHandleL.Complete();
        updateMeshJobHandleR.Complete();
        updateMeshJobHandleL2.Complete(); // Assuming there's a handle for this
        updateMeshJobHandleR2.Complete(); // Assuming there's a handle for this
        #endregion
        #region Dispose Job Arrays
        #region Attract Job
        #region Particle Group Bias
        if (attractJob.AttGroups.IsCreated) attractJob.AttGroups.Dispose();
        if (attractJob.HandBias.IsCreated) attractJob.HandBias.Dispose();
        #endregion
        #region Positions
        #region Mesh
        #region Mesh Positions Arrays
        if (attractJob.MeshPositionsL.IsCreated) attractJob.MeshPositionsL.Dispose();
        if (attractJob.MeshPositionsR.IsCreated) attractJob.MeshPositionsR.Dispose();
        if (attractJob.MeshPositionsL2.IsCreated) attractJob.MeshPositionsL2.Dispose();
        if (attractJob.MeshPositionsR2.IsCreated) attractJob.MeshPositionsR2.Dispose();
        #endregion
        #region Indices Arrays
        // Assuming MeshIndices are part of attractJob or another job, update accordingly
        if (attractJob.MeshIndicesL.IsCreated) attractJob.MeshIndicesL.Dispose();
        if (attractJob.MeshIndicesR.IsCreated) attractJob.MeshIndicesR.Dispose();
        if (attractJob.MeshIndicesL2.IsCreated) attractJob.MeshIndicesL2.Dispose();
        if (attractJob.MeshIndicesR2.IsCreated) attractJob.MeshIndicesR2.Dispose();
        #endregion
        #endregion
        #endregion

        #endregion
        #region Dispose UpdateMeshJobs
        // Dispose World Position Offsets Array (assuming this is shared and needs to be disposed separately)
        if (updateMeshJobL.MeshPositionOffsets.IsCreated) updateMeshJobL.MeshPositionOffsets.Dispose();

        #region L
        if (updateMeshJobL.JointPositions.IsCreated) updateMeshJobL.JointPositions.Dispose();
        if (updateMeshJobL.JointRotations.IsCreated) updateMeshJobL.JointRotations.Dispose();
        if (updateMeshJobL.JointToParticleMap.IsCreated) updateMeshJobL.JointToParticleMap.Dispose();
        if (updateMeshJobL.BaseMeshPositions.IsCreated) updateMeshJobL.BaseMeshPositions.Dispose();
        if (updateMeshJobL.StretchFactor.IsCreated) updateMeshJobL.StretchFactor.Dispose();
        #endregion
        #region R
        if (updateMeshJobR.JointPositions.IsCreated) updateMeshJobR.JointPositions.Dispose();
        if (updateMeshJobR.JointRotations.IsCreated) updateMeshJobR.JointRotations.Dispose();
        if (updateMeshJobR.JointToParticleMap.IsCreated) updateMeshJobR.JointToParticleMap.Dispose();
        if (updateMeshJobR.BaseMeshPositions.IsCreated) updateMeshJobR.BaseMeshPositions.Dispose();
        if (updateMeshJobR.StretchFactor.IsCreated) updateMeshJobR.StretchFactor.Dispose();
        #endregion
        #region LM
        if (updateMeshJobL2.JointPositions.IsCreated) updateMeshJobL2.JointPositions.Dispose();
        if (updateMeshJobL2.JointRotations.IsCreated) updateMeshJobL2.JointRotations.Dispose();
        if (updateMeshJobL2.StretchFactor.IsCreated) updateMeshJobL2.StretchFactor.Dispose();
        #endregion
        #region RM
        if (updateMeshJobR2.JointPositions.IsCreated) updateMeshJobR2.JointPositions.Dispose();
        if (updateMeshJobR2.JointRotations.IsCreated) updateMeshJobR2.JointRotations.Dispose();
        if (updateMeshJobR2.StretchFactor.IsCreated) updateMeshJobR2.StretchFactor.Dispose();
        #endregion
        #endregion
        #endregion
    }
    #endregion
    #endregion
}