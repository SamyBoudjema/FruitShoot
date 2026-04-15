using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    [Tooltip("If null, uses Camera.main.")]
    public Camera targetCamera;

    [Tooltip("If true, only rotate around Y to stay level (recommended for XR HUD).")]
    public bool yawOnly = true;

    [Tooltip("Optional smoothing (0 = instant).")]
    [Range(0f, 20f)]
    public float rotationLerp = 12f;

    private void LateUpdate()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        var toCam = cam.transform.position - transform.position;
        if (yawOnly) toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) return;

        var desired = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        if (rotationLerp <= 0f)
        {
            transform.rotation = desired;
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, desired, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
    }
}

