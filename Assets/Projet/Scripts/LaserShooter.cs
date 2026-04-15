using System.Collections;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(LineRenderer))]
public class LaserShooter : MonoBehaviour
{
    [Header("Laser Settings")]
    public XRNode controllerNode = XRNode.RightHand;
    [Header("Kunai Settings")]
    public float throwVelocity = 250f; // Vitesse extrême pour un impact instantané
    public float cooldown = 0.05f; // Cadence de tir presque immédiate

    private float lastThrowTime;
    private bool previousTriggerState = false;

    void Awake()
    {
        // Désactive l'ancien LineRenderer du laser s'il existe toujours sur l'objet
        var lr = GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = false;
    }

    void Update()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            // Vérifie si la gâchette principale ("Trigger") est pressée
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerValue))
            {
                if (triggerValue && !previousTriggerState && Time.time >= lastThrowTime + cooldown)
                {
                    ShootKunai();
                    lastThrowTime = Time.time;
                }
                previousTriggerState = triggerValue;
            }
        }
    }

    void ShootKunai()
    {
        // Le générateur de Kunai (Modèle procédural)
        GameObject kunai = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        kunai.name = "ThrowingKnife";
        
        // Un kunai est fin et allongé
        kunai.transform.localScale = new Vector3(0.04f, 0.2f, 0.04f); 
        
        // Oriente le cylindre pour qu'il pointe vers l'avant
        kunai.transform.position = transform.position + transform.forward * 0.1f;
        kunai.transform.rotation = transform.rotation;
        kunai.transform.Rotate(90f, 0, 0); // Le cylindre pointe vers le haut(Y) par défaut
        
        // Matériau métallique basique
        var rend = kunai.GetComponent<MeshRenderer>();
        rend.material.color = new Color(0.7f, 0.7f, 0.7f);

        // Ajout de la mécanique de projectile qu'on a codée
        var proj = kunai.AddComponent<KunaiProjectile>();
        proj.velocity = throwVelocity;
    }
}
