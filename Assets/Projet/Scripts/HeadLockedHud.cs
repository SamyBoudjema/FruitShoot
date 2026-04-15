using UnityEngine;

public class HeadLockedHud : MonoBehaviour
{
    [Tooltip("If null, uses Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Meters in front of the camera.")]
    public float distance = 1.0f;

    [Tooltip("Meters below camera center (negative = below).")]
    public float verticalOffset = -0.12f;

    [Tooltip("If true, follow only yaw (stay level).")]
    public bool yawOnly = true;

    [Tooltip("Optional smoothing (0 = instant).")]
    [Range(0f, 20f)]
    public float followLerp = 14f;

    private void LateUpdate()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        var forward = cam.transform.forward;
        if (yawOnly)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
        }

        var desiredPos = cam.transform.position + forward * Mathf.Max(0.2f, distance) + Vector3.up * verticalOffset;
        var desiredRot = yawOnly
            ? Quaternion.LookRotation(forward, Vector3.up)
            : cam.transform.rotation;

        if (followLerp <= 0f)
        {
            transform.SetPositionAndRotation(desiredPos, desiredRot);
            return;
        }

        var t = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
    }
}

