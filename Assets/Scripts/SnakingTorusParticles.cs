using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;
using Unity.Mathematics;


public class SnakingTorusParticles : MonoBehaviour
{
    public ParticleSystem particleSystem;
    [HideInInspector]
    public bool RunSystem = false;
    public int numParticles = 100;
    public Vector2 RadiusRange = new Vector2(.0000001f, 1f); //(.2,.22222); 
    public float MajorWraps = 1.0f;//12
    public Vector2 SizeRange = new Vector2(0.01f, 0.02f); 
    public float alpha = 1.0f;
    public float GradientWidth = 1.0f;//10
    public float GradientSpeed = 1.0f;//100
    public Vector2 GradientRange = new Vector2(0f, 1f); // (.01,.21);
    public Color BaseColor = Color.blue;
    public float ColorLerp = .05f;
    private JobHandle handle;
    SnakingTorusJob stp_Job;

    void Start()
    {
        //particleSystem = GetComponent<ParticleSystem>();
        // Emit particles based on the Particle System's parameters
        particleSystem.Emit(numParticles);
        //RadiusRange.y = RadiusRange.x * 2.3333f;
        stp_Job = new SnakingTorusJob
        {
            RadiusRange = RadiusRange,
            Position = transform.position,
            MajorWraps = MajorWraps,
            SizeRange = SizeRange,
            Alpha = alpha,
            NumParticles = numParticles,
            BaseColor = BaseColor,
            ColorLerp = ColorLerp,
            Time = Time.time, // Pass the current Unity time
            GradientWidth = GradientWidth,
            GradientSpeed = GradientSpeed,
            GradientRange = GradientRange, 
        };
    }

    void Update()
    {
        if (RunSystem)
        {
            stp_Job.RadiusRange = RadiusRange;
            stp_Job.Position = transform.position;
            stp_Job.MajorWraps = MajorWraps;
            stp_Job.SizeRange = SizeRange;
            stp_Job.Alpha = alpha;
            stp_Job.NumParticles = numParticles;
            stp_Job.BaseColor = BaseColor;
            stp_Job.ColorLerp = ColorLerp;
            stp_Job.Time = Time.time; // Update the time each frame
            stp_Job.GradientWidth = GradientWidth;
            stp_Job.GradientSpeed = GradientSpeed;
            stp_Job.GradientRange = GradientRange;

            handle.Complete(); // Ensure the previous job is complete before scheduling a new one
            handle = stp_Job.ScheduleBatch(particleSystem, 64); // Reuse the job with updated properties
        }
    }

    [BurstCompile]
    struct SnakingTorusJob : IJobParticleSystemParallelForBatch
    {
        [ReadOnly] public Vector2 RadiusRange;
        [ReadOnly] public Vector3 Position;
        [ReadOnly] public float MajorWraps;
        [ReadOnly] public int NumParticles;
        [ReadOnly] public Vector2 SizeRange;
        [ReadOnly] public Color BaseColor;
        [ReadOnly] public float ColorLerp;
        [ReadOnly] public float Alpha;
        [ReadOnly] public float Time;
        [ReadOnly] public float GradientWidth;
        [ReadOnly] public float GradientSpeed;
        [ReadOnly] public Vector2 GradientRange;

        public void Execute(ParticleSystemJobData particles, int startIndex, int count)
        {
            var positions = particles.positions;
            var colors = particles.startColors;
            var sizes = particles.sizes.x;
            int endIndex = startIndex + count;

            float totalAngle = 2 * Mathf.PI * MajorWraps;

            for (int i = startIndex; i < endIndex; i++)
            {
                float theta = i * totalAngle / NumParticles;
                float minorWraps = RadiusRange.y / RadiusRange.x;
                float phi = i * totalAngle * minorWraps / NumParticles;

                float x = (RadiusRange.y + RadiusRange.x * Mathf.Cos(phi)) * Mathf.Cos(theta);
                float y = (RadiusRange.y + RadiusRange.x * Mathf.Cos(phi)) * Mathf.Sin(theta);
                float z = RadiusRange.x * Mathf.Sin(phi);

                Vector3 position = new Vector3(x, y, z);
                positions[i] = position;

                // Apply a sinusoidal function with configurable width, speed, min, and max
                float gradientValue = Mathf.Sin((i / GradientWidth) + (Time * GradientSpeed));
                gradientValue = (gradientValue * 0.5f + 0.5f) * (GradientRange.y - GradientRange.x) + GradientRange.x; // Map sine wave to GradientMin to GradientMax

                // Set the color and other properties as needed
                Color gradientColor = new Color(gradientValue, gradientValue, gradientValue, gradientValue * Alpha);
                colors[i] = Color.Lerp(colors[i], gradientColor, ColorLerp);

                // Compute particle size based on the gradient value
                float size = Mathf.Lerp(SizeRange.x, SizeRange.y, gradientValue); // Linearly interpolate size based on gradient value
                sizes[i] = size; // Assuming uniform size for simplicity
            }
        }
    }

}