using UnityEngine;
using System.Collections;

public class EvenNumberCycler : MonoBehaviour
{
    public int minRange = 2;
    public int maxRange = 20;
    public float cycleSpeedSeconds = 1f;
    public float mutationRatio = 0.2f;

    private int currentValue;
    private bool isIncrementing = true; // Direction flag
    private System.Random random = new System.Random();
    private ScheduleTheOtherFactorStates Schedule; // Reference to TheOtherFactor script

    void Start()
    {
        currentValue = minRange;
        Schedule = GetComponent<ScheduleTheOtherFactorStates>();
        StartCoroutine(CycleEvenNumbersCoroutine());
    }

    IEnumerator CycleEvenNumbersCoroutine()
    {
        while (true)
        {
            CycleEvenNumber();
            yield return new WaitForSeconds(cycleSpeedSeconds);
        }
    }

    private void CycleEvenNumber()
    {
        int increment = 2;

        if (random.NextDouble() < mutationRatio)
        {
            increment = random.Next(minRange/2, maxRange/2) * 2;
        }

        // Use the direction flag to determine whether to add or subtract the increment
        currentValue += isIncrementing ? increment : -increment;

        // Check for range limits and toggle direction if necessary
        if (currentValue >= maxRange)
        {
            currentValue = maxRange; // Clamp to max to ensure we don't exceed it
            isIncrementing = false; // Change direction
        }
        else if (currentValue <= minRange)
        {
            currentValue = minRange; // Clamp to min to ensure we don't go below it
            isIncrementing = true; // Change direction
        }

        if (currentValue > maxRange) currentValue = minRange + ((currentValue - minRange) % (maxRange - minRange + 2));
        if (currentValue < minRange) currentValue = maxRange - ((minRange - currentValue) % (maxRange - minRange + 2));

        // Update the integer variable in TheOtherFactor with the new even value
        if (Schedule != null)
        {
            // Replace 'yourIntegerVariable' with the actual integer variable name in TheOtherFactor
            Schedule.TorusWraps = currentValue;
        }

        Debug.Log("Current Even Value: " + currentValue);
    }
}
