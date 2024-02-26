using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ReplayCapture : MonoBehaviour
{
    #region Variables
    public PseudoMeshCreator meshCreator; // Reference to the PseudoMeshCreator script
    public TheOtherFactor tof;
    public float recordRate = 0.1f; // Rate at which to record positions, in seconds
    public float replayDuration = 5f; // Duration of the replay memory, in seconds

    private List<Transform> leftHandJointsInput = new List<Transform>();
    private List<Transform> rightHandJointsInput = new List<Transform>();
    public List<Transform> leftHandJointsOutput = new List<Transform>();
    public List<Transform> rightHandJointsOutput = new List<Transform>();

    private Dictionary<Transform, List<Vector3>> positionsMemory = new Dictionary<Transform, List<Vector3>>();
    private Dictionary<Transform, List<Quaternion>> rotationsMemory = new Dictionary<Transform, List<Quaternion>>();

    private int memoryCapacity; // How many positions/rotations we can store based on recordRate and replayDuration
    private bool memoryIsFull = false; // Flag to indicate when the memory is initially populated
    #endregion

    void Start()
    {
        // Initialize input lists from PseudoMeshCreator
        leftHandJointsInput.AddRange(meshCreator.LeftHandJoints);
        rightHandJointsInput.AddRange(meshCreator.RightHandJoints);

        // Calculate how many memory entries we can store
        memoryCapacity = Mathf.FloorToInt(replayDuration / recordRate);

        // Initialize output lists and dictionaries based on the children of this GameObject
        InitializeOutputListsAndMemory();

        // Start recording input transforms
        StartCoroutine(RecordTransformData());
    }
    void InitializeOutputListsAndMemory()
    {
        foreach (Transform child in transform)
        {
            // Assuming the first half of children are for the left hand and the second half for the right hand
            if (leftHandJointsOutput.Count < leftHandJointsInput.Count)
            {
                leftHandJointsOutput.Add(child);
            }
            else if (rightHandJointsOutput.Count < rightHandJointsInput.Count)
            {
                rightHandJointsOutput.Add(child);
            }

            // Initialize memory for each output transform
            positionsMemory[child] = new List<Vector3>(memoryCapacity);
            rotationsMemory[child] = new List<Quaternion>(memoryCapacity);
        }
    }
    IEnumerator RecordTransformData()
    {
        while (true)
        {
            for (int i = 0; i < leftHandJointsInput.Count; i++)
            {
                UpdateMemory(leftHandJointsOutput[i], leftHandJointsInput[i]);
            }

            for (int i = 0; i < rightHandJointsInput.Count; i++)
            {
                UpdateMemory(rightHandJointsOutput[i], rightHandJointsInput[i]);
            }

            yield return new WaitForSeconds(recordRate);
        }
    }
    void UpdateMemory(Transform outputTransform, Transform jointTransform)
    {
        Vector3 position = jointTransform.position;
        Quaternion rotation = jointTransform.rotation;

        List<Vector3> positionList = positionsMemory[outputTransform];
        List<Quaternion> rotationList = rotationsMemory[outputTransform];

        if (positionList.Count < memoryCapacity)
        {
            positionList.Add(position);
            rotationList.Add(rotation);

            // Check if memory is full for the first time
            if (positionList.Count == memoryCapacity && !memoryIsFull)
            {
                memoryIsFull = true;
            }
        }

        if (memoryIsFull)
        {
            positionList.RemoveAt(0);
            rotationList.RemoveAt(0);
            positionList.Add(position);
            rotationList.Add(rotation);
            if (!tof.RealTimeMirror)
            {
                ReplayMovement(leftHandJointsOutput, 0);
                ReplayMovement(rightHandJointsOutput, 0);
            }
        }
    }
    void ReplayMovement(List<Transform> outputTransforms, int index)
    {
        foreach (var outputTransform in outputTransforms)
        {
            var positionList = positionsMemory[outputTransform];
            var rotationList = rotationsMemory[outputTransform];

            if (positionList.Count > index && rotationList.Count > index)
            {
                // Mirror the position by negating the Z-coordinate
                Vector3 mirroredPosition = positionList[index];
                mirroredPosition.z = -mirroredPosition.z; // Adjust Z based on mirror's position in the world

                // Mirror the rotation by inverting the rotation around the Y-axis
                Quaternion mirroredRotation = MirrorRotation(rotationList[index], Vector3.up);

                outputTransform.position = mirroredPosition;
                outputTransform.rotation = rotationList[index];// mirroredRotation;
            }
        }
    }
    // Function to mirror a rotation around a given axis
    Quaternion MirrorRotation(Quaternion rotation, Vector3 axis)
    {
        return Quaternion.AngleAxis(180, axis) * rotation;
    }
}
