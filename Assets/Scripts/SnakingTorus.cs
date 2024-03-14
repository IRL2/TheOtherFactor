using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class SnakingTorus : MonoBehaviour
{
    public int numSpheres = 100; // Total number of spheres
    public Vector2 RadiusRange = new Vector2(.0000001f, 1f);
    private Vector2 lastRadiusRange = new Vector2(0f, 1f);
    public Vector2 OscillationRange = new Vector2(0.1f, 0.2f); // Range of oscillation for inner and outer radius
    public float OscillationSpeed = 1.0f; // Speed of oscillation

    public float wraps = 1.0f; // Controls the density of the snaking line
    public Material baseMaterial;
    public float alpha = 1.0f;

    private GameObject[] sphereObjects;
    private float[] gradientValues; // Array to hold the gradient values
    public float updateSpeed;
    private float timeSinceLastUpdate = 0f;
    private float phase = 0f; // Phase shift for the moving gradient

    void Start()
    {
        sphereObjects = new GameObject[numSpheres];
        gradientValues = new float[numSpheres];
        InitializeGradientValues();
        RadiusRange.x = RadiusRange.x == 0 ? .00001f : RadiusRange.x;
        RadiusRange.y = RadiusRange.y == 0 ? .00001f : RadiusRange.y;
        lastRadiusRange = RadiusRange;
        StartVisualization(true);
    }

    void InitializeGradientValues()
    {
        float minValue = 0.1f; // Specify the minimum value of the gradient at the outer radius
        float maxDistance = RadiusRange.y + RadiusRange.x; // Maximum distance from the center line of the torus tube
        float minDistance = RadiusRange.x; // Minimum distance (at the inner radius)

        for (int i = 0; i < numSpheres; i++)
        {
            // Calculate the position along the path
            float t = (float)i / (numSpheres - 1);

            // Map the position to angles on the torus
            float theta = t * 2 * Mathf.PI * wraps; // Angle around the major radius
            float phi = theta * (RadiusRange.y / RadiusRange.x); // Angle around the minor radius, adjusted for the number of Wraps

            // Calculate radial distance from the center line of the torus tube
            float radialDistance = Mathf.Abs(RadiusRange.y + RadiusRange.x * Mathf.Cos(phi)) - RadiusRange.y;

            // Normalize the radial distance to [0, 1]
            float normalizedDistance = (radialDistance - minDistance) / (maxDistance - minDistance);

            // Compute the gradient value based on the radial distance
            gradientValues[i] = minValue + (1 - minValue) * (1 - normalizedDistance);
        }
    }

    void StartVisualization(bool createNewSpheres)
    {
        float totalAngle = 2 * Mathf.PI * wraps; // Total angle for a complete wrap
        for (int i = 0; i < numSpheres; i++)
        {
            GameObject sphere = createNewSpheres || sphereObjects[i] == null
                ? GameObject.CreatePrimitive(PrimitiveType.Sphere)
                : sphereObjects[i];

            if (createNewSpheres || sphereObjects[i] == null)
            {
                sphere.transform.localScale = transform.localScale; // Adjust as needed
                Destroy(sphere.GetComponent<Collider>());
                sphereObjects[i] = sphere;
                sphere.GetComponent<Renderer>().material = new Material(baseMaterial);
            }

            // Calculate angular positions with an adjustment to ensure closure
            float theta = i * totalAngle / (numSpheres - 1); // Ensures the last sphere aligns with the first
            float minorWraps = RadiusRange.y / RadiusRange.x;
            float phi = i * totalAngle * minorWraps / (numSpheres - 1); // Synchronized with major Wraps

            // Calculate position on the torus
            float x = (RadiusRange.y + RadiusRange.x * Mathf.Cos(phi)) * Mathf.Cos(theta);
            float y = (RadiusRange.y + RadiusRange.x * Mathf.Cos(phi)) * Mathf.Sin(theta);
            float z = RadiusRange.x * Mathf.Sin(phi);

            Vector3 position = new Vector3(x, y, z) + transform.position;
            sphere.transform.position = position;

            // Set color based on gradient values
            Color color = new Color(gradientValues[i], gradientValues[i], gradientValues[i], alpha);
            sphere.GetComponent<Renderer>().material.color = color;

            sphereObjects[i] = sphere;
        }
    }


    // The Update and OnDestroy methods can remain largely the same.
    // You may remove or modify the UpdateGrid and UpdateGridJob methods as the Game of Life logic is no longer needed.

    void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;

        // Oscillate the RadiusRange
        //float oscillation = Mathf.Sin(Time.time * Random.Range(00001,1f) * OscillationSpeed) * 0.5f + 0.5f; // Normalized sine wave oscillation
        //RadiusRange.x = Mathf.Lerp(OscillationRange.x, OscillationRange.y, oscillation); // Oscillate inner radius
        //oscillation = Mathf.Sin((Time.time + .5f) * OscillationSpeed) * 0.5f + 0.5f; // Normalized sine wave oscillation
        //RadiusRange.y = Mathf.Lerp(OscillationRange.y, OscillationRange.x, oscillation); // Oscillate outer radius


        if (timeSinceLastUpdate >= updateSpeed)
        {
            if (lastRadiusRange != RadiusRange)
            {
                RadiusRange.x = RadiusRange.x == 0 ? .00001f : RadiusRange.x;
                RadiusRange.y = RadiusRange.y == 0 ? .00001f : RadiusRange.y;
                lastRadiusRange = RadiusRange;
                InitializeGradientValues();
                StartVisualization(false);
            }
            else
            {
                timeSinceLastUpdate = 0f;
                RotateGradientValues();
                ApplyGradient();
            }
        }
    }

    void RotateGradientValues()
    {
        // Rotate the gradient values array by one position
        float lastValue = gradientValues[numSpheres - 1];
        for (int i = numSpheres - 1; i > 0; i--)
        {
            gradientValues[i] = gradientValues[i - 1];
        }
        gradientValues[0] = lastValue;
    }

    void ApplyGradient()
    {
        for (int i = 0; i < numSpheres; i++)
        {
            // Apply the gradient value to each sphere
            Color color = new Color(gradientValues[i], gradientValues[i], gradientValues[i], alpha);
            sphereObjects[i].GetComponent<Renderer>().material.color = color;
        }
    }

    void OnDestroy()
    {
        // Clean up the resources
        foreach (var sphere in sphereObjects)
        {
            if (sphere != null) Destroy(sphere);
        }
    }
}
