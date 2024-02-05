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
using Unity.VisualScripting;

public class PseudoMeshCreator : MonoBehaviour
{
    #region Variables
    public bool Initialize = false;
    public int ParticlesPerHand = 35000;
    #region Internal Utility
    #region Hands
    #region Transform References
    public GameObject Hands;
    #region Joints
    #region Oculus Script References to fetch Joints 
    public HandVisual LeftHandVisual;
    public HandVisual RightHandVisual;
    #endregion
    public List<Transform> LeftHandJoints = new List<Transform>();
    public List<Transform> RightHandJoints = new List<Transform>();
    #endregion
    #region Skinned Mesh Renderer
    public SkinnedMeshRenderer leftHandMesh;
    public SkinnedMeshRenderer rightHandMesh;
    #endregion
    #endregion
    #region Pseudo-Mesh
    #region Setup Variables
    [System.Serializable]
    public class JointData
    {
        public int jointIndex;
        public string jointName;
        public List<Vector3> relativePositions = new List<Vector3>();
    }
    private List<JointData> JointOnMeshMapL = new List<JointData>();
    private List<JointData> JointOnMeshMapR = new List<JointData>();
    #endregion
    #region Relative Positions and Joint Indices
    public List<Vector3> LeftHandRelativePositions = new List<Vector3>();
    public List<Vector3> RightHandRelativePositions = new List<Vector3>();
    public List<int> LeftHandJointIndices = new List<int>();
    public List<int> RightHandJointIndices = new List<int>();
    #endregion
    #region Percentages
    public List<float> LeftHandPercentages = new List<float>();
    public List<float> RightHandPercentages = new List<float>();
    #endregion
    #endregion
    #endregion

    #endregion
    #endregion
    #region Start
    private void Start()
    {
        if(Initialize) InitializeHandPseudoMeshCoroutine();
    }
    #endregion
    #region Initialize Hand Pseudo Mesh Coroutine
    public async Task InitializeHandPseudoMeshCoroutine()
    {
        #region Find Oculus Hand References
        FindOculusHandReferences();
        await Task.Yield();
        #endregion
        #region Wait for User to Hold Hands in Vision and Spread Fingers
        // since we fetch closest joints, we want the finger to be spread
        await Task.Delay(TimeSpan.FromSeconds(4));
        #endregion
        #region Disable Hand Tracking and Visuals for Initialization
        // Disable hand tracking at the start so Hands wont move while we instantiate mesh points.
        Hands.SetActive(false);
        #endregion
        #region Fetch Oculus Joints
        FetchOculusHandJoints();
        await Task.Yield();
        #endregion
        #region Create Pseudo Mesh
        CreatePseudoMesh();
        await Task.Yield();
        #endregion
        #region Calculate Percentage Of Entries
        LeftHandPercentages = CalculatePercentageOfEntries(LeftHandJointIndices);
        RightHandPercentages = CalculatePercentageOfEntries(RightHandJointIndices);
        #endregion
        #region Enable Hand Tracking
        Hands.SetActive(true);
        #endregion
    }
    #region Find Oculus Hand References
    private void FindOculusHandReferences()
    {
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

        Hands = GameObject.Find("Hands");
        if (Hands == null)
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
    }
    #endregion
    #region Fetch Oculus Joints
    /// <summary>
    /// Fetches joint transforms from Oculus HandVisual scripts for both the left and right Hands. 
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
        JointOnMeshMapL.Clear();
        JointOnMeshMapR.Clear();

        // Create a temporary dictionary to store the joint data for efficient lookup
        Dictionary<int, JointData> jointDataDictL = new Dictionary<int, JointData>();
        Dictionary<int, JointData> jointDataDictR = new Dictionary<int, JointData>();

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

        // Process data for left and right Hands
        ProcessHandData(LeftHandJoints, LeftHandJointIndices, LeftHandRelativePositions, jointDataDictL);
        ProcessHandData(RightHandJoints, RightHandJointIndices, RightHandRelativePositions, jointDataDictR);

        JointOnMeshMapL.AddRange(jointDataDictL.Values);
        JointOnMeshMapR.AddRange(jointDataDictR.Values);
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
        // Clear existing output data for both Hands
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
                JointData jointDataList = JointOnMeshMap.Find(jd => jd.jointName == jointName);

                if (jointDataList != null)
                {
                    // Add joint index for each relative position
                    foreach (var pos in jointDataList.relativePositions)
                    {
                        jointIndices.Add(jointDataList.jointIndex);
                    }

                    // Add relative positions
                    relativePos.AddRange(jointDataList.relativePositions);
                }
            }
        }

        GenerateMeshTraversalPath(LeftHandJoints, LeftHandJointIndices, LeftHandRelativePositions, JointOnMeshMapL);
        GenerateMeshTraversalPath(RightHandJoints, RightHandJointIndices, RightHandRelativePositions, JointOnMeshMapR);
    }
    #endregion
    
    #region Calculate Percentage Of Entries
    public List<float> CalculatePercentageOfEntries(List<int> inputList)
    {
        // If the input list is empty, return an empty list to avoid division by zero
        if (inputList == null || inputList.Count == 0)
            return new List<float>();

        // Determine the maximum value in the list to know the size of the percentages list
        int maxValue = inputList.Max();

        // Initialize a list to count occurrences of each integer, with a size of maxValue + 1
        List<int> counts = new List<int>(new int[maxValue + 1]);

        // Iterate through the input list and count occurrences of each integer
        foreach (int value in inputList)
        {
            counts[value]++;
        }

        // Initialize a list to hold the percentages
        List<float> percentages = new List<float>(new float[maxValue + 1]);

        // Calculate the percentage of entries for each integer
        for (int i = 0; i <= maxValue; i++)
        {
            percentages[i] = (float)counts[i] / inputList.Count * 100;
        }

        return percentages;
    }
    #endregion
    #endregion
    #region Resize Lists Preserving Percentages (called in TheOtherFactor)
    /// <summary>
    /// Gets a request from TheOtherFactor to provide JointIndices and RealtivePosition lists for a hand with a given size.
    /// Provides it by using precomputed percentage of points for each joint.
    /// </summary>
    public (List<int>, List<Vector3>) ResizeListsPreservingPercentages(int targetLength, string hand)
    {
        if (hand != "left" && hand != "right")
        {
            throw new ArgumentException("Invalid hand string provided.");
        }

        List<int> originalIndices = new List<int>();
        List<Vector3> originalRelativePositions = new List<Vector3>();
        List<float> percentages = new List<float>();

        if (hand == "left")
        {
            originalIndices = LeftHandJointIndices;
            originalRelativePositions = LeftHandRelativePositions;
            percentages = LeftHandPercentages;
        }
        else if (hand == "right")
        {
            originalIndices = RightHandJointIndices;
            originalRelativePositions = RightHandRelativePositions;
            percentages = RightHandPercentages;
        }

        if (originalIndices.Count != originalRelativePositions.Count || originalIndices.Count == 0 || targetLength <= 0 || targetLength > originalIndices.Count)
        {
            throw new ArgumentException("Invalid arguments provided.");
        }

        List<int> resizedIndices = new List<int>();
        List<Vector3> resizedRelativePositions = new List<Vector3>();
        Dictionary<int, List<int>> indexPositions = new Dictionary<int, List<int>>();

        // Group positions by index
        for (int i = 0; i < originalIndices.Count; i++)
        {
            int index = originalIndices[i];
            if (!indexPositions.ContainsKey(index))
            {
                indexPositions[index] = new List<int>();
            }
            indexPositions[index].Add(i);
        }

        foreach (var kvp in indexPositions)
        {
            int index = kvp.Key;
            List<int> positions = kvp.Value;
            int countForThisIndex = (int)Math.Round(percentages[index] * targetLength / 100.0);
            // Calculate the interval for selecting positions to ensure they are spread out
            double interval = positions.Count / (double)countForThisIndex;

            for (int i = 0; i < countForThisIndex && i * interval < positions.Count; i++)
            {
                int posIndex = (int)Math.Round(i * interval);
                resizedIndices.Add(index);
                resizedRelativePositions.Add(originalRelativePositions[positions[posIndex]]);
            }
        }

        // Ensure the resized lists are trimmed to the target length in case of rounding issues
        if (resizedIndices.Count > targetLength)
        {
            resizedIndices = resizedIndices.GetRange(0, targetLength);
            resizedRelativePositions = resizedRelativePositions.GetRange(0, targetLength);
        }

        return (resizedIndices, resizedRelativePositions);
    }
    #endregion
}
