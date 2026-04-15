using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(LineRenderer))]
public class LaserShooter : MonoBehaviour
{
    [Header("Controller")]
    public XRNode controllerNode = XRNode.RightHand;

    [Header("Kunai Settings")]
    public float cooldown = 0.05f;

    [Header("Sabre Laser Settings")]
    public float sabreLength = 500.0f; // Très long pour atteindre tous les fruits
    public Color sabreColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    [Tooltip("Son d'activation du sabre (ignition).")]
    public AudioClip sabreIgniteSound;
    [Tooltip("Son de bourdonnement continu du sabre (hum loop).")]
    public AudioClip sabreHumSound;
    [Tooltip("Son de mouvement rapide du sabre (swing).")]
    public AudioClip sabreSwingSound;

    private float lastThrowTime;
    private bool previousTriggerState = false;
    private GameObject crosshair;

    // Sabre Laser
    private LineRenderer sabreLine;
    private GameObject sabreGlow;
    private bool sabreActive = false;
    private float sabreSliceCooldown = 0.15f; // Empêche multi-slice du même fruit
    private HashSet<int> recentlySlicedIds = new HashSet<int>();
    private float lastSliceClearTime;

    // Audio sabre
    private AudioSource sabreSfxSource;
    private AudioSource sabreHumSource;
    private Vector3 lastSabrePosition;
    private float lastSwingTime;

    void Awake()
    {
        // Désactive l'ancien LineRenderer
        var lr = GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = false;

        // Crée le crosshair (visible uniquement en mode Couteaux)
        crosshair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crosshair.name = "Crosshair";
        Destroy(crosshair.GetComponent<Collider>());
        crosshair.transform.localScale = Vector3.one * 0.04f;
        var rend = crosshair.GetComponent<MeshRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = new Color(0.2f, 1f, 0.2f, 0.85f);

        // Prépare le LineRenderer pour le Sabre Laser
        SetupSabreLine();
    }

    void Start()
    {
        // Désactive le laser vert de XR Toolkit
        var lineVis = GetComponent("UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual");
        if (lineVis != null) ((MonoBehaviour)lineVis).enabled = false;

        var lr = GetComponent<LineRenderer>();
        if (lr != null && lr != sabreLine)
        {
            lr.enabled = false;
            lr.positionCount = 0;
        }
    }

    void SetupSabreLine()
    {
        // Crée un nouveau GameObject dédié au sabre pour ne pas interférer avec le LineRenderer existant
        var sabreObj = new GameObject("SabreLaserVisual");
        sabreObj.transform.SetParent(transform);
        sabreObj.transform.localPosition = Vector3.zero;
        sabreObj.transform.localRotation = Quaternion.identity;

        sabreLine = sabreObj.AddComponent<LineRenderer>();
        sabreLine.positionCount = 2;
        sabreLine.startWidth = 0.035f;
        sabreLine.endWidth = 0.02f;
        sabreLine.useWorldSpace = true;
        sabreLine.material = new Material(Shader.Find("Sprites/Default"));
        sabreLine.startColor = sabreColor;
        sabreLine.endColor = new Color(sabreColor.r, sabreColor.g, sabreColor.b, 0.4f);
        sabreLine.enabled = false;

        // Boule lumineuse au bout du sabre
        sabreGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sabreGlow.name = "SabreGlow";
        Destroy(sabreGlow.GetComponent<Collider>());
        sabreGlow.transform.localScale = Vector3.one * 0.06f;
        var glowRend = sabreGlow.GetComponent<MeshRenderer>();
        glowRend.material = new Material(Shader.Find("Sprites/Default"));
        glowRend.material.color = new Color(sabreColor.r, sabreColor.g, sabreColor.b, 0.7f);
        sabreGlow.SetActive(false);

        // Sources audio pour le sabre
        sabreSfxSource = gameObject.AddComponent<AudioSource>();
        sabreSfxSource.playOnAwake = false;
        sabreSfxSource.loop = false;
        sabreSfxSource.volume = 0.7f;

        sabreHumSource = gameObject.AddComponent<AudioSource>();
        sabreHumSource.playOnAwake = false;
        sabreHumSource.loop = true;
        sabreHumSource.volume = 0.35f;
    }

    WeaponType GetCurrentWeapon()
    {
        if (GameManager.Instance != null) return GameManager.Instance.weapon;
        return WeaponType.Couteaux;
    }

    void Update()
    {
        var weapon = GetCurrentWeapon();

        // Gère la visibilité du crosshair selon l'arme
        if (crosshair != null) crosshair.SetActive(weapon == WeaponType.Couteaux);

        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid) return;

        if (!device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerValue))
            return;

        if (weapon == WeaponType.Couteaux)
        {
            HandleKunaiMode(triggerValue);
        }
        else
        {
            HandleSabreMode(triggerValue);
        }

        previousTriggerState = triggerValue;
    }

    void LateUpdate()
    {
        if (GetCurrentWeapon() == WeaponType.Couteaux && crosshair != null && crosshair.activeSelf)
        {
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 30f))
                crosshair.transform.position = hit.point;
            else
                crosshair.transform.position = transform.position + transform.forward * 4f;
        }
    }

    // ======================== MODE COUTEAUX ========================
    void HandleKunaiMode(bool triggerValue)
    {
        // Éteindre le sabre s'il était actif
        SetSabreActive(false);

        if (triggerValue && !previousTriggerState && Time.time >= lastThrowTime + cooldown)
        {
            ShootKunai();
            lastThrowTime = Time.time;
        }
    }

    void ShootKunai()
    {
        Vector3 rayStart = transform.position;
        Vector3 rayDir = transform.forward;
        Vector3 hitPosition = rayStart + rayDir * 30f;

        if (Physics.Raycast(rayStart, rayDir, out RaycastHit hit, 30f))
        {
            hitPosition = hit.point;

            FruitTarget fruit = hit.collider.GetComponent<FruitTarget>();
            if (fruit != null) fruit.Slice(rayDir, hitPosition, rayDir * 15f);

            Bomb bomb = hit.collider.GetComponent<Bomb>();
            if (bomb != null) bomb.Slice();
        }

        SpawnVisualKnife(rayStart + rayDir * 0.1f, hitPosition);
    }

    void SpawnVisualKnife(Vector3 from, Vector3 to)
    {
        GameObject knife = new GameObject("VisualKnife");
        knife.transform.position = from;
        knife.transform.rotation = Quaternion.LookRotation(to - from);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(visual.GetComponent<Collider>());
        visual.transform.SetParent(knife.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        visual.transform.localScale = new Vector3(0.03f, 0.15f, 0.03f);
        var rend = visual.GetComponent<MeshRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = new Color(0.8f, 0.8f, 0.9f);

        var trail = knife.AddComponent<TrailRenderer>();
        trail.time = 0.1f;
        trail.startWidth = 0.03f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(0.8f, 0.9f, 1f, 0.6f);
        trail.endColor = new Color(0.8f, 0.9f, 1f, 0f);

        StartCoroutine(AnimateKnife(knife, from, to));
    }

    private IEnumerator AnimateKnife(GameObject knife, Vector3 from, Vector3 to)
    {
        float duration = 0.06f;
        float elapsed = 0f;

        while (elapsed < duration && knife != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            knife.transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        if (knife != null)
        {
            yield return new WaitForSeconds(0.3f);
            Destroy(knife);
        }
    }

    // ======================== MODE SABRE LASER ========================
    void HandleSabreMode(bool triggerValue)
    {
        if (triggerValue)
        {
            SetSabreActive(true);

            // Rayon continu qui coupe tout sur son passage
            Vector3 origin = transform.position;
            Vector3 tip = origin + transform.forward * sabreLength;

            sabreLine.SetPosition(0, origin);
            sabreLine.SetPosition(1, tip);

            if (sabreGlow != null)
            {
                sabreGlow.transform.position = tip;
            }

            // === SON DE MOUVEMENT ===
            float moveSpeed = (transform.position - lastSabrePosition).magnitude / Time.deltaTime;
            lastSabrePosition = transform.position;

            // Varier le pitch du bourdonnement selon la vitesse de mouvement
            if (sabreHumSource != null && sabreHumSource.isPlaying)
            {
                sabreHumSource.pitch = Mathf.Lerp(0.9f, 1.4f, Mathf.Clamp01(moveSpeed / 5f));
            }

            // Jouer un son de swing quand on bouge vite
            if (moveSpeed > 1.5f && Time.time - lastSwingTime > 0.4f)
            {
                if (sabreSwingSound != null && sabreSfxSource != null)
                {
                    sabreSfxSource.pitch = Random.Range(0.85f, 1.15f);
                    sabreSfxSource.PlayOneShot(sabreSwingSound, 0.6f);
                }
                lastSwingTime = Time.time;
            }

            // Nettoyer les IDs de fruits coupés périodiquement
            if (Time.time - lastSliceClearTime > sabreSliceCooldown)
            {
                recentlySlicedIds.Clear();
                lastSliceClearTime = Time.time;
            }

            // Détection par SphereCast le long du sabre
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.15f, transform.forward, sabreLength);
            foreach (var hit in hits)
            {
                int id = hit.collider.gameObject.GetInstanceID();
                if (recentlySlicedIds.Contains(id)) continue;

                FruitTarget fruit = hit.collider.GetComponent<FruitTarget>();
                if (fruit != null)
                {
                    fruit.Slice(transform.forward, hit.point, transform.forward * 10f);
                    recentlySlicedIds.Add(id);
                }

                Bomb bomb = hit.collider.GetComponent<Bomb>();
                if (bomb != null)
                {
                    bomb.Slice();
                    recentlySlicedIds.Add(id);
                }
            }
        }
        else
        {
            SetSabreActive(false);
        }
    }

    void SetSabreActive(bool active)
    {
        if (sabreActive == active) return;
        sabreActive = active;

        if (sabreLine != null) sabreLine.enabled = active;
        if (sabreGlow != null) sabreGlow.SetActive(active);

        if (active)
        {
            // Son d'activation (ignition)
            if (sabreIgniteSound != null && sabreSfxSource != null)
                sabreSfxSource.PlayOneShot(sabreIgniteSound);

            // Démarre le bourdonnement continu
            if (sabreHumSound != null && sabreHumSource != null)
            {
                sabreHumSource.clip = sabreHumSound;
                sabreHumSource.Play();
            }
        }
        else
        {
            // Arrête le bourdonnement
            if (sabreHumSource != null && sabreHumSource.isPlaying)
                sabreHumSource.Stop();

            recentlySlicedIds.Clear();
        }
    }
}
