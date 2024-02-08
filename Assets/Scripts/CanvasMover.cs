using UnityEngine;

public class CanvasMover : MonoBehaviour
{
    public Camera targetCamera;
    public float distanceFromCamera = 2.0f; // Distance in front of the camera

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main; // Fallback to main camera if none specified
        }
        Invoke("SetCanvasPosition", 2);
    }
    public void SetCanvasPosition()
    {
        if (targetCamera != null)
        {
            // Position the canvas in front of the camera
            transform.position = targetCamera.transform.position + targetCamera.transform.forward * distanceFromCamera;
            // Optionally, make the canvas face the camera
            transform.LookAt(transform.position + targetCamera.transform.rotation * Vector3.forward, targetCamera.transform.rotation * Vector3.up);
        }
    }
    void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main; // Fallback to main camera if none specified
        }

        if (targetCamera != null)
        {
            transform.LookAt(transform.position + targetCamera.transform.rotation * Vector3.forward, targetCamera.transform.rotation * Vector3.up);
        }
    }
}
