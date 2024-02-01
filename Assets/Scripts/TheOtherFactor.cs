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
    #region Gaussian Attraction
    [Header("Gaussian Attraction")]
    public float GaussianAttraction = 1f;
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
    #region Autostart
    [Header("Start")]
    [Tooltip("Determines whether script should start autoamtically at scene load, with a delay of StartAtPlayInvokeTime")]
    public bool StartAtPlay = true;
    public float StartAtPlayInvokeTime = 1f;
    #endregion
    #endregion
    #region Internal Variables to Communicate Inspector Variables to Jobs (often needs NativeArrays)
    #region Particle Attraction Job
    #region Job Handle
    private ParticleAttractionJob particleAttractionJob;
    private JobHandle particleAttractionJobHandle;
    #endregion
    #region Attraction Scaling Per Group/Hand
    private NativeArray<Vector2> paJob_ParticlesAttractionLR;
    private float[] paJob_PerParticleScaling;
    #endregion
    #region Pseudo Mesh
    #region Positions
    private NativeArray<Vector3> paJob_WorldPositionsL;
    private NativeArray<Vector3> paJob_WorldPositionsR;
    #endregion
    #region Indices
    private NativeArray<int> paJob_PseudoMeshIndicesL;
    private NativeArray<int> paJob_PseudoMeshIndicesR;
    private NativeArray<int> paJob_StepSizes;
    #endregion
    #endregion
    #region Memory
    private NativeArray<Vector3> paJob_PreviousVelocities;
    private NativeArray<Vector3> paJob_PreviousPositions;
    #endregion
    #endregion
    #region Pseudo mesh World Positions Job
    #region Job Handle
    private JobHandle positionJobHandleL;
    private ComputeWorldPositionJob positionJobL;

    private JobHandle positionJobHandleR;
    private ComputeWorldPositionJob positionJobR;
    #endregion
    #region Joints
    private NativeArray<Vector3> cwp_JointPositionsL;
    private NativeArray<quaternion> cwp_JointRotationsL;
    public NativeArray<int> cwp_JointToParticleMapL;

    private NativeArray<Vector3> cwp_JointPositionsR;
    private NativeArray<quaternion> cwp_JointRotationsR;
    public NativeArray<int> cwp_JointToParticleMapR;
    #endregion
    #region Pseudo Mesh
    public NativeArray<Vector3> cwp_PseudoMeshParticlePositionsL;
    private NativeArray<Vector3> cwp_WorldPositionsL;
    private NativeArray<Vector3> cwp_WorldPositionsLNeutral;

    public NativeArray<Vector3> cwp_PseudoMeshParticlePositionsR;
    private NativeArray<Vector3> cwp_WorldPositionsR;
    private NativeArray<Vector3> cwp_WorldPositionsRNeutral;

    #endregion
    #region Position Offsets
    private NativeArray<float> cwp_PositionOffsets;
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
        #region Hands
        LeftHandVisual = GameObject.Find("LeftHandVisual")?.GetComponent<HandVisual>();
        if (LeftHandVisual == null)
        {
            Debug.LogError("LeftHandVisual GameObject or HandVisual component not found.");
        }

        RightHandVisual = GameObject.Find("RightHandVisual")?.GetComponent<HandVisual>();
        if (RightHandVisual == null)
        {
            Debug.LogError("RightHandVisual GameObject or HandVisual component not found.");
        }

        hands = GameObject.Find("Hands");
        if (hands == null)
        {
            Debug.LogError("Hands GameObject not found.");
        }

        leftHandMesh = GameObject.Find("l_handMeshNode")?.GetComponent<SkinnedMeshRenderer>();
        if (leftHandMesh == null)
        {
            Debug.LogError("l_handMeshNode GameObject or SkinnedMeshRenderer component not found.");
        }

        rightHandMesh = GameObject.Find("r_handMeshNode")?.GetComponent<SkinnedMeshRenderer>();
        if (rightHandMesh == null)
        {
            Debug.LogError("r_handMeshNode GameObject or SkinnedMeshRenderer component not found.");
        }
        #endregion
        #endregion
        if (StartAtPlay) Invoke("StartTheOtherFactor", StartAtPlayInvokeTime);
    }
    #region Callable Start / Stop Functions triggered by Inspector Button created in Editor Script
    public async void DelayedStart()
    {
        await Task.Delay(TimeSpan.FromSeconds(StartAtPlayInvokeTime)); 
        StartTheOtherFactor();
    }
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
    public async Task InitializeParticlesCoroutine()
    {
        #region Disable Hand Tracking and Visuals for Initialization
        // Disable hand tracking at the start so hands wont move while we instantiate mesh points.
        hands.SetActive(false);
        leftHandMesh.gameObject.SetActive(false);
        rightHandMesh.gameObject.SetActive(false);
        #endregion

        #region Fetch Oculus Joints
        FetchOculusHandJoints();
        await Task.Yield();
        #endregion

        #region Create Pseudo Mesh
        // This could instead pre precomputed and stored in a file to read from in this step.
        // It results in two arrays, the relative positions and the associated joint indices.
        CreatePseudoMesh();
        await Task.Yield();
        #endregion

        #region Initialze Runtime Arrays
        InitializeRuntimeArrays();
        await Task.Yield();
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

        #region Enable Hand Tracking (and Visuals if DisplayOculusHands = true)
        hands.SetActive(true);
        leftHandMesh.gameObject.SetActive(DisplayOculusHands);
        rightHandMesh.gameObject.SetActive(DisplayOculusHands);
        #endregion
    }
    #region Fetch Oculus Joints
    /// <summary>
    /// Fetches joint transforms from Oculus HandVisual scripts for both the left and right hands. 
    /// This function is critical for setting up the hand tracking system. It ensures that each hand's joints 
    /// are properly identified and prepared for further processing.
    /// </summary>
    public void FetchOculusHandJoints()
    {
        InitializeHandJointsFromHandVisual(LeftHandVisual, "left");
        InitializeHandJointsFromHandVisual(RightHandVisual, "right");
    }
    /// <summary>
    /// Organizes hand joints for particle path traversal.
    /// This function filters and rearranges joints, notably positioning fingertip joints sequentially in the array for a natural hand representation.
    /// The default Oculus HandVisual joint array doesn't place fingertip joints consecutively, 
    /// which is essential for particle traversal to mimic a natural path from the base of each finger to its tip.
    /// Adjusting this order facilitates accurate and intuitive particle movement along the fingers.
    /// The LeftHandParticlePath and RightHandParticlePath can be set to public for visualization of this traversal in the Unity Inspector.
    /// </summary>
    /// <param name="handVisual">The Oculus script that holds a list of the relevant joints.</param>
    /// <param name="handType">Specifies whether the hand is left or right.</param>
    public void InitializeHandJointsFromHandVisual(HandVisual handVisual, string handType)
    {
        IList<Transform> jointTransforms = handVisual.Joints;
        // Create a set of joints to exclude (like wrists and forearms) to focus on finger joints
        HashSet<string> excludedJoints = new HashSet<string> { "b_l_wrist", "b_r_wrist", "b_l_forearm_stub", "b_r_forearm_stub" };
        List<Transform> fingertips = new List<Transform>(); // List to store fingertip joints

        Action<List<Transform>> processHandJoints = (handJoints) =>
        {
            handJoints.Clear(); // Clear existing hand joints to prepare for updated data

            // Iterate over all joints and filter based on naming conventions and relevance
            foreach (Transform joint in jointTransforms)
            {
                // Exclude joints like wrists and forearms
                if (!excludedJoints.Contains(joint.name))
                {
                    // Identify and add fingertip joints to a separate list for special handling
                    if (joint.name.Contains("_finger_tip_marker"))
                    {
                        fingertips.Add(joint);
                    }
                    else
                    {
                        handJoints.Add(joint); // Add non-fingertip joints to the main list
                    }
                }
            }

            // Process each fingertip and insert it back into the main joint list at the correct position
            foreach (var fingertip in fingertips)
            {
                string fingerBaseName = fingertip.name.Substring(0, fingertip.name.IndexOf("_finger_tip_marker"));
                // Find the index of the last joint of the same finger
                int lastJointIndex = handJoints.FindLastIndex(j => j.name.StartsWith(fingerBaseName));
                // Insert the fingertip right after the last joint of its corresponding finger
                if (lastJointIndex >= 0)
                {
                    handJoints.Insert(lastJointIndex + 1, fingertip);
                }
            }
        };

        // Process hand joints based on the specified hand type (left or right)
        if (handType == "right")
        {
            processHandJoints(RightHandJoints);
        }
        else if (handType == "left")
        {
            processHandJoints(LeftHandJoints);
        }
    }
    #endregion
    #region Create Pseudo Mesh
    /// <summary>
    /// Creates ParticlesPerHand number of points on both left and right hand meshes.
    /// Calculates the relative positions of these points to the closest hand joints, preparing them for dynamic tracking in VR.
    /// </summary>
    public void CreatePseudoMesh()
    {
        var leftHandPoints = InstantiateMeshPoints(leftHandMesh);
        var rightHandPoints = InstantiateMeshPoints(rightHandMesh);

        LeftHandRelativePositions.Clear();
        RightHandRelativePositions.Clear();
        LeftHandJointIndices.Clear();
        RightHandJointIndices.Clear();

        InitializeRelativePositions(leftHandPoints, LeftHandJoints, LeftHandRelativePositions, LeftHandJointIndices);
        InitializeRelativePositions(rightHandPoints, RightHandJoints, RightHandRelativePositions, RightHandJointIndices);

        CreateJointOnMeshMaps();
        GenerateParticlePathData();
    }
    /// <summary>
    /// Creates a set of points on a given skinned mesh renderer using a parallel job for efficiency.
    /// Bakes the mesh and generates random points, transforming them into world space for further processing.
    /// </summary>
    /// <param name="skinnedMeshRenderer">The skinned mesh renderer of the hand.</param>
    /// <returns>An array of Vector3 points on the mesh.</returns>
    private Vector3[] InstantiateMeshPoints(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        var meshObject = skinnedMeshRenderer.gameObject.transform;
        var bakedMesh = new Mesh();
        skinnedMeshRenderer.BakeMesh(bakedMesh);

        var vertices = new NativeArray<Vector3>(bakedMesh.vertices, Allocator.TempJob);
        var triangles = new NativeArray<int>(bakedMesh.triangles, Allocator.TempJob);
        var points = new NativeArray<Vector3>(ParticlesPerHand, Allocator.TempJob);
        var barycentricCoords = new NativeArray<Vector3>(ParticlesPerHand, Allocator.TempJob);
        var triangleIndices = new NativeArray<int>(ParticlesPerHand, Allocator.TempJob);

        var job = new InstantiateMeshPointsJob
        {
            vertices = vertices,
            triangles = triangles,
            points = points,
            barycentricCoords = barycentricCoords,
            triangleIndices = triangleIndices,
            random = new Random((uint)UnityEngine.Random.Range(1, int.MaxValue)) // Seed the random number generator
        };

        var jobHandle = job.Schedule(ParticlesPerHand, 64);
        jobHandle.Complete();

        var result = points.ToArray();

        // Transform the points to world space
        for (int i = 0; i < ParticlesPerHand; i++)
        {
            result[i] = meshObject.TransformPoint(result[i]);
        }

        vertices.Dispose();
        triangles.Dispose();
        points.Dispose();
        barycentricCoords.Dispose();
        triangleIndices.Dispose();

        return result;
    }
    /// <summary>
    /// A job for instantiating mesh points on a skinned mesh renderer in a parallelized fashion.
    /// It generates random points on the mesh surface by using barycentric coordinates and triangle areas for balanced distribution.
    /// This job is essential for creating realistic representations of hand meshes in VR by evenly distributing points across the mesh.
    /// </summary>
    private struct InstantiateMeshPointsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> vertices; // Mesh vertices
        [ReadOnly] public NativeArray<int> triangles;    // Mesh triangles
        public NativeArray<Vector3> points;              // Points to be instantiated on the mesh
        public NativeArray<Vector3> barycentricCoords;   // Barycentric coordinates for each point
        public NativeArray<int> triangleIndices;         // Triangle indices for each point
        public Random random;                            // Random number generator for point creation

        public void Execute(int index)
        {
            // Generate a random barycentric coordinate for even distribution
            float r1 = math.sqrt(random.NextFloat());
            float r2 = random.NextFloat();
            float u = 1 - r1;
            float v = r1 * (1 - r2);
            float w = r1 * r2;
            barycentricCoords[index] = new Vector3(u, v, w);

            // Initialize vectors for vertices of triangles
            Vector3 v1, v2, v3;

            // Calculate the areas of triangles to ensure even point distribution
            float[] areas = new float[triangles.Length / 3];
            float totalArea = 0;
            for (int i = 0; i < triangles.Length / 3; i++)
            {
                v1 = vertices[triangles[i * 3]];
                v2 = vertices[triangles[i * 3 + 1]];
                v3 = vertices[triangles[i * 3 + 2]];
                areas[i] = Vector3.Cross(v2 - v1, v3 - v1).magnitude / 2;
                totalArea += areas[i];
            }

            // Determine the size of each area segment or "stratum"
            float stratumSize = totalArea / points.Length;

            // Select a triangle within the determined stratum
            float cumulativeArea = 0;
            int triangleIndex = 0;
            for (; triangleIndex < areas.Length; triangleIndex++)
            {
                cumulativeArea += areas[triangleIndex];
                if (cumulativeArea >= stratumSize * index)
                {
                    break;
                }
            }
            triangleIndices[index] = triangleIndex * 3;

            // Compute the point within the selected triangle using barycentric coordinates
            Vector3 uvw = barycentricCoords[index];
            triangleIndex = triangleIndices[index];
            v1 = vertices[triangles[triangleIndex]];
            v2 = vertices[triangles[triangleIndex + 1]];
            v3 = vertices[triangles[triangleIndex + 2]];
            points[index] = uvw.x * v1 + uvw.y * v2 + uvw.z * v3;
        }
    }
    /// <summary>
    /// Calculates the relative positions of mesh points to their nearest hand joints in world space.
    /// Converts joint data to efficient formats and computes the closest joint for each point, storing their relative positions.
    /// Essential for mapping hand movement dynamically without baking the mesh renderer in real time.
    /// </summary>
    /// <param name="worldCoordinates">The mesh points in world coordinates.</param>
    /// <param name="joints">The hand joints.</param>
    /// <param name="targetRelativePositions">List to store relative positions.</param>
    /// <param name="targetJointIndices">List to store joint indices.</param>
    private void InitializeRelativePositions(Vector3[] worldCoordinates, List<Transform> joints, List<Vector3> targetRelativePositions, List<int> targetJointIndices)
    {
        // Convert joint positions to a NativeArray.
        var jointPositions = new NativeArray<Vector3>(joints.Count, Allocator.TempJob);
        // Convert joint rotations to a NativeArray.
        var jointRotations = new NativeArray<quaternion>(joints.Count, Allocator.TempJob);
        for (int i = 0; i < joints.Count; i++)
        {
            jointPositions[i] = joints[i].position;
            jointRotations[i] = joints[i].rotation;
        }
        // Create a job to compute the closest joint for each point.
        var job = new GetClosestJointJob
        {
            jointPositions = jointPositions,
            jointRotations = jointRotations,
            points = new NativeArray<Vector3>(worldCoordinates, Allocator.TempJob),
            relativePositions = new NativeArray<Vector3>(worldCoordinates.Length, Allocator.TempJob),
            jointIndices = new NativeArray<int>(worldCoordinates.Length, Allocator.TempJob),
        };

        // Schedule and complete the job.
        var jobHandle = job.Schedule(worldCoordinates.Length, 64);
        jobHandle.Complete();

        for (int i = 0; i < worldCoordinates.Length; i++)
        {
            targetRelativePositions.Add(job.relativePositions[i]);
            targetJointIndices.Add(job.jointIndices[i]);// + jointIndexOffset);
        }

        // Dispose of the NativeArrays.
        jointPositions.Dispose();
        jointRotations.Dispose();
        job.points.Dispose();
        job.relativePositions.Dispose();
        job.jointIndices.Dispose();
    }
    /// <summary>
    /// A parallel job structure to calculate the closest joint to each mesh point.
    /// Iterates through joints, finds the closest one to each point, and computes their relative positions.
    /// </summary>
    [BurstCompile]
    public struct GetClosestJointJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> jointPositions;
        [ReadOnly] public NativeArray<quaternion> jointRotations;
        [ReadOnly] public NativeArray<Vector3> points;
        public NativeArray<Vector3> relativePositions;
        public NativeArray<int> jointIndices;

        public void Execute(int index)
        {
            var point = points[index];
            var closestJointIndex = 0;
            var closestDistance = Vector3.Distance(jointPositions[0], point);

            // Find the closest joint.
            for (int i = 1; i < jointPositions.Length; i++)
            {
                var distance = Vector3.Distance(jointPositions[i], point);
                if (distance < closestDistance)
                {
                    closestJointIndex = i;
                    closestDistance = distance;
                }
            }

            // Compute the relative position.
            var relativePosition = point - jointPositions[closestJointIndex];

            // Rotate the relative position by the inverse of the joint's rotation.
            relativePosition = math.mul(math.inverse(jointRotations[closestJointIndex]), relativePosition);

            // Store the relative position and joint index.
            relativePositions[index] = relativePosition;
            jointIndices[index] = closestJointIndex;
        }
    }
    /// <summary>
    /// Creates mappings between joints and their corresponding mesh points.
    /// This function processes each hand's joints to associate them with related mesh points.
    /// It utilizes dictionaries for efficient lookups and storage. The process involves iterating over joint indices and relative positions,
    /// grouping mesh points by their closest joints. 
    /// </summary>
    public void CreateJointOnMeshMaps()
    {
        // Clear existing data
        JointOnMeshMap1.Clear();
        JointOnMeshMap2.Clear();

        // Create a temporary dictionary to store the joint data for efficient lookup
        Dictionary<int, JointData> jointDataDict1 = new Dictionary<int, JointData>();
        Dictionary<int, JointData> jointDataDict2 = new Dictionary<int, JointData>();

        // Method to process joint data for a hand
        void ProcessHandData(List<Transform> joints, List<int> jointIndices, List<Vector3> relativePositions, Dictionary<int, JointData> jointDataDict)
        {
            for (int i = 0; i < jointIndices.Count; i++)
            {
                int jointIndex = jointIndices[i];
                Vector3 relativePos = relativePositions[i];

                if (!jointDataDict.ContainsKey(jointIndex))
                {
                    JointData newJointData = new JointData
                    {
                        jointIndex = jointIndex,
                        jointName = joints[jointIndex].name,
                        relativePositions = new List<Vector3> { relativePos }
                    };

                    jointDataDict[jointIndex] = newJointData;
                }
                else
                {
                    jointDataDict[jointIndex].relativePositions.Add(relativePos);
                }
            }
        }

        // Process data for left and right hands
        ProcessHandData(LeftHandJoints, LeftHandJointIndices, LeftHandRelativePositions, jointDataDict1);
        ProcessHandData(RightHandJoints, RightHandJointIndices, RightHandRelativePositions, jointDataDict2);

        JointOnMeshMap1.AddRange(jointDataDict1.Values);
        JointOnMeshMap2.AddRange(jointDataDict2.Values);
    }
    /// <summary>
    /// Generates detailed particle paths for each hand's mesh based on joint information.
    /// It processes each hand's joints and the particles that where associated with it by distance to create a sequential traversal path for particles along the hand mesh.
    /// For each joint in the hand, the method retrieves the corresponding joint data (indices and relative positions) from the joint-to-mesh mapping.
    /// Each joint index is repeatedly added to the output list for every relative position associated with it, maintaining the correlation between the joint and its mesh points.
    /// This ordered list of joint indices and their relative positions ensures that particles follow a path that replicates natural hand movements, 
    /// moving sequentially from one joint to the next along the hand.
    /// </summary>
    public void GenerateParticlePathData()
    {
        // Clear existing output data for both hands
        LeftHandJointIndices.Clear();
        LeftHandRelativePositions.Clear();
        RightHandJointIndices.Clear();
        RightHandRelativePositions.Clear();

        void GenerateMeshTraversalPath(List<Transform> jointPath, List<int> jointIndices, List<Vector3> relativePos, List<JointData> JointOnMeshMap)
        {
            for (int pathIndex = 0; pathIndex < jointPath.Count; pathIndex++)
            {
                string jointName = jointPath[pathIndex].name;
                // Find the JointData corresponding to the current joint name
                JointData jointData = JointOnMeshMap.Find(jd => jd.jointName == jointName);

                if (jointData != null)
                {
                    // Add joint index for each relative position
                    foreach (var pos in jointData.relativePositions)
                    {
                        jointIndices.Add(jointData.jointIndex);
                    }

                    // Add relative positions
                    relativePos.AddRange(jointData.relativePositions);
                }
            }
        }

        GenerateMeshTraversalPath(LeftHandJoints, LeftHandJointIndices, LeftHandRelativePositions, JointOnMeshMap1);
        GenerateMeshTraversalPath(RightHandJoints, RightHandJointIndices, RightHandRelativePositions, JointOnMeshMap2);
    }
    #endregion
    #region Initialize Runtime Arrays
    public void InitializeRuntimeArrays()
    {
        int totalParticles = ParticlesPerHand * 2;
        #region Particle Attraction Job
        #region Attraction
        paJob_ParticlesAttractionLR = new NativeArray<Vector2>(totalParticles, Allocator.Persistent);
        paJob_PerParticleScaling = new float[totalParticles];
        for (int i = 0; i < totalParticles; i++)
        {
            float linearRandom = UnityEngine.Random.Range(PerParticleScalingMinMax.x, PerParticleScalingMinMax.y);
            paJob_PerParticleScaling[i] = Mathf.Pow(linearRandom, PerParticleScalingPowerFactor);
        }
        #endregion
        #region Pseudo Mesh
        #region Positions
        paJob_WorldPositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        paJob_WorldPositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region Indices
        paJob_PseudoMeshIndicesL = new NativeArray<int>(totalParticles, Allocator.Persistent);
        paJob_PseudoMeshIndicesR = new NativeArray<int>(totalParticles, Allocator.Persistent);
        for (int i = 0; i < totalParticles; i++)
        {
            paJob_PseudoMeshIndicesL[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
            paJob_PseudoMeshIndicesR[i] = Mathf.RoundToInt(UnityEngine.Random.Range(0, ParticlesPerHand));
        }
        paJob_StepSizes = new NativeArray<int>(totalParticles, Allocator.Persistent);
        for (int i = 0; i < totalParticles; i++)
        {
            paJob_StepSizes[i] = Mathf.RoundToInt(UnityEngine.Random.Range(IndexStepSizeMinMax.x, IndexStepSizeMinMax.y));
        }
        #endregion
        #endregion
        #region Memory
        paJob_PreviousVelocities = new NativeArray<Vector3>(totalParticles, Allocator.Persistent);
        paJob_PreviousPositions = new NativeArray<Vector3>(totalParticles, Allocator.Persistent);
        #endregion
        #endregion
        #region Compute World Positions Job
        #region Joints
        cwp_JointPositionsL = new NativeArray<Vector3>(LeftHandJoints.Count, Allocator.Persistent);
        cwp_JointRotationsL = new NativeArray<quaternion>(LeftHandJoints.Count, Allocator.Persistent);
        cwp_JointToParticleMapL = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < LeftHandJointIndices.Count; i++)
        {
            cwp_JointToParticleMapL[i] = LeftHandJointIndices[i];
        }

        cwp_JointPositionsR = new NativeArray<Vector3>(RightHandJoints.Count, Allocator.Persistent);
        cwp_JointRotationsR = new NativeArray<quaternion>(RightHandJoints.Count, Allocator.Persistent);
        cwp_JointToParticleMapR = new NativeArray<int>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < RightHandJointIndices.Count; i++)
        {
            cwp_JointToParticleMapR[i] = RightHandJointIndices[i];
        }
        #endregion
        #region Pseudo Mesh
        cwp_PseudoMeshParticlePositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < LeftHandJointIndices.Count; i++)
        {
            cwp_PseudoMeshParticlePositionsL[i] = LeftHandRelativePositions[i];
        }
        cwp_WorldPositionsL = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        cwp_WorldPositionsLNeutral = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);

        cwp_PseudoMeshParticlePositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < RightHandJointIndices.Count; i++)
        {
            cwp_PseudoMeshParticlePositionsR[i] = RightHandRelativePositions[i];
        }
        cwp_WorldPositionsR = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        cwp_WorldPositionsRNeutral = new NativeArray<Vector3>(ParticlesPerHand, Allocator.Persistent);
        #endregion
        #region World Position Offsets
        cwp_PositionOffsets = new NativeArray<float>(ParticlesPerHand, Allocator.Persistent);
        for (int i = 0; i < ParticlesPerHand; i++)
        {
            cwp_PositionOffsets[i] = Mathf.RoundToInt(UnityEngine.Random.Range(cwp_PositionOffsetMinMax.x, cwp_PositionOffsetMinMax.y));
        }
        #endregion
        #endregion
    }
    #endregion
    #region Emit Particles
    /// <summary>
    /// Emits particles roughly at the middle of each hand.
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
        particleAttractionJob = new ParticleAttractionJob
        {
            #region Gaussian Attraction
            paJob_GaussianAttraction = GaussianAttraction,
            paJob_GaussianAttractionExponent = 2 * GaussianAttraction * GaussianAttraction,
            #endregion
            #region Velocity
            paJob_VelocityLerp = VelocityLerp,
            #endregion
            #region Attraction Scaling Per Group/Hand
            paJob_PartilesAttractionLR = paJob_ParticlesAttractionLR,
            #endregion
            #region Pseudo Mesh
            #region Positions
            paJob_WorldPositionsL = paJob_WorldPositionsL,
            paJob_WorldPositionsR = paJob_WorldPositionsR,
            #endregion
            #region Indices
            paJob_PseudoMeshIndicesL = paJob_PseudoMeshIndicesL,
            paJob_PseudoMeshIndicesR = paJob_PseudoMeshIndicesR,
            paJob_IndexStepSizes = paJob_StepSizes,
            paJob_IndexStepsSizeIndex = 0,
            #endregion
            #endregion
            #region Memory
            paJob_PreviousVelocities = paJob_PreviousVelocities,
            paJob_PreviousPositions = paJob_PreviousPositions,
            #endregion
            #region Color
            paJob_ColorLerp = ColorLerp,
            #endregion
            #region Size
            paJob_ParticleSizeMinMax = ParticleSizeMinMax,
            paJob_DistanceForMinSize = DistanceForMinSize,
            paJob_SizeLerp = SizeLerp,
            #endregion
        };
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
    }
    #endregion
    #endregion
    #region Runtime Updates
    void OnParticleUpdateJobScheduled()
    {
        if (RunJobs && particleAttractionJobHandle.IsCompleted && positionJobHandleL.IsCompleted && positionJobHandleR.IsCompleted)
        {
            if (isEvenFrame)
            {
                #region World Position Jobs
                cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex < cwp_PositionOffsets.Length ? cwp_PositionOffsetsIndex + 1 : 0;
                #region Schedule PositionR Job
                positionJobHandleR.Complete();
                #region Update Joint Positions
                for (int i = 0; i < RightHandJoints.Count; i++)
                {
                    positionJobR.cwp_JointPositions[i] = RightHandJoints[i].position;
                    positionJobR.cwp_JointRotations[i] = RightHandJoints[i].rotation;
                }
                #endregion
                #region Update World Positions in Neutral Array for Attraction Job to Read
                for (int i = 0; i < positionJobR.cwp_WorldPositions.Length; i++)
                {
                    cwp_WorldPositionsRNeutral[i] = positionJobR.cwp_WorldPositions[i];
                }
                #endregion
                #region Update PositionOffsetsIndex
                positionJobR.cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex;
                #endregion
                positionJobHandleR = positionJobR.Schedule(particleSystem.particleCount / 2, 1024);
                #endregion
                #region Schedule PositionL Job
                positionJobHandleL.Complete();
                #region Update Joint Positions
                for (int i = 0; i < LeftHandJoints.Count; i++)
                {
                    positionJobL.cwp_JointPositions[i] = LeftHandJoints[i].position;
                    positionJobL.cwp_JointRotations[i] = LeftHandJoints[i].rotation;
                }
                #endregion
                #region Update World Positions in Neutral Array for Attraction Job to Read
                for (int i = 0; i < positionJobL.cwp_WorldPositions.Length; i++)
                {
                    cwp_WorldPositionsLNeutral[i] = positionJobL.cwp_WorldPositions[i];
                }
                #endregion
                #region Update PositionOffsetsIndex
                positionJobL.cwp_PositionOffsetsIndex = cwp_PositionOffsetsIndex;
                #endregion
                positionJobHandleL = positionJobL.Schedule(particleSystem.particleCount / 2, 1024);
                #endregion
                #endregion
            }
            else
            {
                #region Attraction Job
                particleAttractionJobHandle.Complete();
                UpdateAttractionJob();
                particleAttractionJobHandle = particleAttractionJob.ScheduleBatch(particleSystem, 1024);
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
        #region Gaussian Attraction
        particleAttractionJob.paJob_GaussianAttraction =  GaussianAttraction;
        particleAttractionJob.paJob_GaussianAttractionExponent = 2 * GaussianAttraction * GaussianAttraction; 
        #endregion
        #region Velocity
        particleAttractionJob.paJob_VelocityLerp = VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        UpdateParticlesAttractionLR();
        particleAttractionJob.paJob_PartilesAttractionLR = paJob_ParticlesAttractionLR;
        #endregion
        #region Pseudo Mesh
        #region World Positions
        for (int i = 0; i < particleAttractionJob.paJob_WorldPositionsR.Length; i++)
        {
            particleAttractionJob.paJob_WorldPositionsR[i] = cwp_WorldPositionsRNeutral[i];
        }
        for (int i = 0; i < particleAttractionJob.paJob_WorldPositionsL.Length; i++)
        {
            particleAttractionJob.paJob_WorldPositionsL[i] = cwp_WorldPositionsLNeutral[i];
        }
        #endregion
        #region Indices
        particleAttractionJob.paJob_IndexStepsSizeIndex = particleAttractionJob.paJob_IndexStepsSizeIndex < paJob_StepSizes.Length ? particleAttractionJob.paJob_IndexStepsSizeIndex + 1 : 0;
        #endregion
        #endregion
        #region Color
        particleAttractionJob.paJob_ColorLerp = ColorLerp;
        #endregion
        #region Size
        particleAttractionJob.paJob_ParticleSizeMinMax = ParticleSizeMinMax;
        particleAttractionJob.paJob_DistanceForMinSize = DistanceForMinSize;
        particleAttractionJob.paJob_SizeLerp = SizeLerp;
        #endregion
    }
    private void UpdateParticlesAttractionLR()
    {
        Vector2 attractionVector = Vector2.one;

        for (int i = 0; i < paJob_ParticlesAttractionLR.Length; i++)
        {
            if (i < paJob_ParticlesAttractionLR.Length / 2)
            {
                attractionVector.x = ParticlesAttractionGroup1.x;
                attractionVector.y = ParticlesAttractionGroup1.y * paJob_PerParticleScaling[i];
            }
            else
            {
                attractionVector.x = ParticlesAttractionGroup2.x * paJob_PerParticleScaling[i];
                attractionVector.y = ParticlesAttractionGroup2.y;
            }
            paJob_ParticlesAttractionLR[i] = attractionVector.normalized;
        }
    }
    #endregion
    #region Jobs
    #region Attraction Job
    /// <summary>
    /// This job uses GaussianAttraction parameters to control attraction strength
    /// and updates particle positions and velocities based on attraction to specific world positions. It handles particles for both left and right hands.
    /// The job also includes logic for color copmutation based on the particles velocity.
    /// </summary>
    [BurstCompile]
    struct ParticleAttractionJob : IJobParticleSystemParallelForBatch
    {
        #region Job Variables
        #region Gaussian Attraction
        [ReadOnly] public float paJob_GaussianAttraction;
        [ReadOnly] public float paJob_GaussianAttractionExponent;
        #endregion
        #region Veloctiy
        [ReadOnly] public float paJob_VelocityLerp;
        #endregion
        #region Attraction Scaling Per Group/Hand
        [ReadOnly] public NativeArray<Vector2> paJob_PartilesAttractionLR;
        #endregion
        #region Pseudo Mesh
        #region Positions
        [ReadOnly] public NativeArray<Vector3> paJob_WorldPositionsL;
        [ReadOnly] public NativeArray<Vector3> paJob_WorldPositionsR;
        #endregion
        #region Indices
        public NativeArray<int> paJob_PseudoMeshIndicesL;
        public NativeArray<int> paJob_PseudoMeshIndicesR;
        [ReadOnly] public NativeArray<int> paJob_IndexStepSizes;
        [ReadOnly] public int paJob_IndexStepsSizeIndex;
        #endregion
        #endregion
        #region Memory
        public NativeArray<Vector3> paJob_PreviousVelocities;
        public NativeArray<Vector3> paJob_PreviousPositions;
        #endregion
        #region Color
        [ReadOnly] public float paJob_ColorLerp;
        #endregion
        #region Size
        [ReadOnly] public Vector2 paJob_ParticleSizeMinMax;
        [ReadOnly] public float paJob_DistanceForMinSize;
        [ReadOnly] public float paJob_SizeLerp;
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
                int pseudoMeshPosIndexL = paJob_PseudoMeshIndicesL[particleIndex];
                Vector3 worldPositionL = paJob_WorldPositionsL[pseudoMeshPosIndexL];
                paJob_PseudoMeshIndicesL[particleIndex] = (pseudoMeshPosIndexL + paJob_IndexStepSizes[(particleIndex + paJob_IndexStepsSizeIndex) % paJob_WorldPositionsL.Length]) % paJob_WorldPositionsL.Length;
                Vector3 velocityL = CalculateAttractionVelocity(worldPositionL, particlePosition, paJob_GaussianAttractionExponent, paJob_GaussianAttraction);
                velocityL *= paJob_PartilesAttractionLR[particleIndex].x;
                #endregion
                #region Compute Attraction to Right Hand
                int pseudoMeshPosIndexR = paJob_PseudoMeshIndicesR[particleIndex];
                Vector3 worldPositionR = paJob_WorldPositionsR[pseudoMeshPosIndexR];
                paJob_PseudoMeshIndicesR[particleIndex] = (pseudoMeshPosIndexR + paJob_IndexStepSizes[(particleIndex + paJob_IndexStepsSizeIndex) % paJob_WorldPositionsR.Length]) % paJob_WorldPositionsR.Length;
                Vector3 velocityR = CalculateAttractionVelocity(worldPositionR, particlePosition, paJob_GaussianAttractionExponent, paJob_GaussianAttraction);
                velocityR *= paJob_PartilesAttractionLR[particleIndex].y;
                #endregion

                #region Update Particle Velocity, Size and Color
                #region Veloctiy
                Vector3 velocity = velocityL + velocityR;
                velocity = math.lerp(velocities[particleIndex], velocity, paJob_VelocityLerp);
                paJob_PreviousVelocities[particleIndex] = velocity;
                velocities[particleIndex] = velocity;
                #endregion
                #region Size
                float distanceL = math.length(worldPositionL - particlePosition);
                float distanceR = math.length(worldPositionR - particlePosition);
                float leastDistance = math.min(distanceL, distanceR); 

                // Normalize the distance (0 at maxDistance or beyond, 1 at distance 0)
                float normalizedDistance = math.clamp(leastDistance / paJob_DistanceForMinSize, 0f, 1f);
                float inverseNormalizedDistance = 1 - normalizedDistance;
                float targetSize = math.lerp(paJob_ParticleSizeMinMax.x, paJob_ParticleSizeMinMax.y, inverseNormalizedDistance);

                sizes[i] = math.lerp(sizes[i], targetSize, paJob_SizeLerp);
                #endregion
                #region Color
                // Compute particle color
                //Color color = ComputeParticleColor(velocity);
                //colors[particleIndex] = Color.Lerp(colors[particleIndex], color, paJob_ColorLerp);
                // For Debugging, it can make sense to color the two groups of particles in distinct colors
                //colors[particleIndex] = particleIndex < particles.count / 2 ? Color.green : Color.red;
                Color color = Color.white;
                color.a = math.lerp(1, 0, inverseNormalizedDistance);
                colors[particleIndex] = color;
                #endregion
                #endregion
            }
        }
        #region Functions
        #region Calculate Attraction Velocity
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Vector3 CalculateAttractionVelocity(Vector3 worldPosition, Vector3 particlePosition, float exponentAttraction, float gaussianAttractionFactor)
        {
            // Calculate the direction and distance from the particle to the world position
            Vector3 direction = worldPosition - particlePosition;

            // Calculate the exponent for the Gaussian distribution based on the distance and attraction strength
            float exponent = -math.lengthsq(direction) /  exponentAttraction;

            // Apply the Gaussian distribution to calculate the attraction force
            float attraction = gaussianAttractionFactor * math.exp(exponent);

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
    /// This approach is crucial for multiplayer online synchronization and provides dynamic, natural-looking particle movement around the hands.
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
    #region Dispose Native Arrays On Disable
    void OnDisable()
    {
        #region Complete Jobs
        particleAttractionJobHandle.Complete();
        positionJobHandleL.Complete();
        positionJobHandleR.Complete();
        #endregion
        #region Particle Attraction Job
        #region Attraction
        paJob_ParticlesAttractionLR.Dispose();
        #endregion
        #region Pseudo Mesh
        #region Positions
        paJob_WorldPositionsL.Dispose();
        paJob_WorldPositionsR.Dispose();
        #endregion
        #region Indices
        paJob_PseudoMeshIndicesL.Dispose();
        paJob_PseudoMeshIndicesR.Dispose();
        #endregion
        #endregion
        #region Memory
        paJob_PreviousVelocities.Dispose();
        paJob_PreviousPositions.Dispose();
        #endregion
        #endregion
        #region Pseudo Mesh World Position Job
        #region Joints
        cwp_JointPositionsL.Dispose();
        cwp_JointPositionsR.Dispose();
        cwp_JointRotationsL.Dispose();
        cwp_JointRotationsR.Dispose();
        cwp_JointToParticleMapL.Dispose();
        cwp_JointToParticleMapR.Dispose();
        #endregion
        #region Pseudo Mesh
        cwp_WorldPositionsL.Dispose();
        cwp_WorldPositionsR.Dispose();
        cwp_PseudoMeshParticlePositionsL.Dispose();
        cwp_PseudoMeshParticlePositionsR.Dispose();
        #endregion
        #endregion
    }
    #endregion
    #endregion
}