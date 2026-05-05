using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Impulsions haptiques via l'API XR legacy (OpenXR / PICO).
/// </summary>
public static class XRHaptics
{
    public static void PulseBothHands(float amplitude, float duration)
    {
        amplitude = Mathf.Clamp01(amplitude);
        if (duration <= 0f) return;

        var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (left.isValid)
            left.SendHapticImpulse(0, amplitude, duration);
        if (right.isValid)
            right.SendHapticImpulse(0, amplitude, duration);
    }
}
