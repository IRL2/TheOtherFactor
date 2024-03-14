using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class ToroidalGameOfLife : MonoBehaviour
{
    public int cellsPerEdge = 10; // Number of cells along each circular path
    public float R = 15f; // Major radius (from the center of the hole to the center of the tube)
    public float r = 5f; // Minor radius (radius of the tube itself)
    public float updateSpeed;
    private float timeSinceLastUpdate = 0f;

    private NativeArray<float> grid;
    private NativeArray<float> nextGridState;

    private GameObject[] sphereObjects;
    public Material baseMaterial;
    public float alpha = 1.0f;

    void Start()
    {
        int totalCells = cellsPerEdge * cellsPerEdge;
        grid = new NativeArray<float>(totalCells, Allocator.Persistent);
        nextGridState = new NativeArray<float>(totalCells, Allocator.Persistent);
        InitializeGrid();
        StartVisualization();
    }

    void InitializeGrid()
    {
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = UnityEngine.Random.Range(0f, 1f); // Initialize with random float values
        }
    }

    void StartVisualization()
    {
        int totalCells = cellsPerEdge * cellsPerEdge;
        sphereObjects = new GameObject[totalCells];

        for (int i = 0; i < totalCells; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());

            float u = (i % cellsPerEdge) * 2 * Mathf.PI / cellsPerEdge; // Angle around the major radius
            float v = (i / cellsPerEdge) * 2 * Mathf.PI / cellsPerEdge; // Angle around the minor radius

            // Calculate position on the torus
            float x = (R + r * Mathf.Cos(v)) * Mathf.Cos(u);
            float y = (R + r * Mathf.Cos(v)) * Mathf.Sin(u);
            float z = r * Mathf.Sin(v);

            Vector3 position = new Vector3(x, y, z) + transform.position;

            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * (2 * Mathf.PI * r / cellsPerEdge); // Adjust scale based on minor radius and cell count

            Material instanceMat = new Material(baseMaterial);
            float value = grid[i];
            Color color = new Color(value, value, value, alpha);
            instanceMat.color = color;

            sphere.GetComponent<Renderer>().material = instanceMat;
            sphereObjects[i] = sphere;
        }
    }

    void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;

        if (timeSinceLastUpdate >= updateSpeed)
        {
            timeSinceLastUpdate = 0f;
            UpdateGrid();
            UpdateVisualization();
        }
    }

    void UpdateVisualization()
    {
        for (int i = 0; i < grid.Length; i++)
        {
            float value = grid[i];
            Color color = new Color(value, value, value, alpha); // Example: grayscale based on value
            sphereObjects[i].GetComponent<Renderer>().material.color = color;
        }
    }

    void UpdateGrid()
    {
        Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue));

        var updateJob = new UpdateGridJob
        {
            cellsPerEdge = cellsPerEdge,
            grid = grid,
            nextGridState = nextGridState,
            random = random
        };

        JobHandle jobHandle = updateJob.Schedule(grid.Length, 64);
        jobHandle.Complete();

        (grid, nextGridState) = (nextGridState, grid);
    }

    [BurstCompile]
    struct UpdateGridJob : IJobParallelFor
    {
        public int cellsPerEdge;
        [ReadOnly] public NativeArray<float> grid;
        public NativeArray<float> nextGridState;
        public Unity.Mathematics.Random random;

        public void Execute(int index)
        {
            int y = index / cellsPerEdge;
            int x = index % cellsPerEdge;

            float currentValue = grid[index];
            int aliveNeighbors = 0;

            // Count alive neighbors with toroidal wrapping
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;

                    int nx = (x + i + cellsPerEdge) % cellsPerEdge;
                    int ny = (y + j + cellsPerEdge) % cellsPerEdge;
                    int neighborIndex = ny * cellsPerEdge + nx;

                    if (grid[neighborIndex] > 0.1f) aliveNeighbors++;
                }
            }

            // Apply survival and birth rules
            if (currentValue > 0.1f && (aliveNeighbors >= 2 && aliveNeighbors <= 4))
            {
                nextGridState[index] = Mathf.Min(currentValue + 0.05f, 1.0f);
            }
            else if (currentValue <= 0.1f && (aliveNeighbors >= 3 && aliveNeighbors <= 4))
            {
                nextGridState[index] = 0.5f;
            }
            else
            {
                nextGridState[index] = Mathf.Max(currentValue - 0.02f, 0.0f);
            }
        }
    }



    void OnDestroy()
    {
        if (grid.IsCreated) grid.Dispose();
        if (nextGridState.IsCreated) nextGridState.Dispose();
    }
}
