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
    public float sabreLength = 2000.0f; // Longueur quasi infinie pour tout toucher
    public float sabreWidth = 0.05f;    // Largeur du sabre (un peu plus fin)
    [Tooltip("Rayon de slice en mode sabre (hitbox). Plus petit = plus précis.")]
    public float sabreSliceRadius = 0.07f;
    [Tooltip("Longueur utile de détection (en mètres). Garder raisonnable pour les perfs.")]
    public float sabreHitLength = 8f;
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
    private GameObject sabreVisual; // Conteneur
    private Transform sabreMesh;    // Référence vers le cube visuel
    private GameObject sabreGlow;
    private bool sabreActive = false;
    private float sabreSliceCooldown = 0.15f; // Empêche multi-slice du même fruit
    private HashSet<int> recentlySlicedIds = new HashSet<int>();
    private float lastSliceClearTime;

    public LayerMask sliceLayerMask = -1; // Masque par défaut pour tout trancher
    
    // Audio sabre
    private AudioSource sabreSfxSource;
    private AudioSource sabreHumSource;
    private Vector3 lastSabrePosition;
    private float lastSwingTime;

    // Reuse buffer to avoid allocations every frame
    private readonly Collider[] sabreOverlapBuffer = new Collider[64];

    void Awake()
    {
        // Désactive et configure l'ancien LineRenderer
        var lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.enabled = false;
            // On lui donne un matériau pour éviter le violet
            lr.material = new Material(Shader.Find("Sprites/Default"));
        }

        // Crée le crosshair (visible uniquement en mode Couteaux)
        crosshair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crosshair.name = "Crosshair";
        Destroy(crosshair.GetComponent<Collider>());
        crosshair.transform.localScale = Vector3.one * 0.04f;
        var rend = crosshair.GetComponent<MeshRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = new Color(0.2f, 1f, 0.2f, 0.85f);

        // Prépare le Visuel du Sabre Laser (Cylindre)
        SetupSabreVisual();
    }

    void Start()
    {
        // Désactive le laser vert de XR Toolkit
        var lineVis = GetComponent("UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual");
        if (lineVis != null) ((MonoBehaviour)lineVis).enabled = false;

        var lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.enabled = false;
        }
    }

    void SetupSabreVisual()
    {
        // Conteneur (Pivot à (0,0,0) au niveau de la main)
        sabreVisual = new GameObject("SabreLaserContainer");
        sabreVisual.transform.SetParent(transform);
        sabreVisual.transform.localPosition = Vector3.zero;
        sabreVisual.transform.localRotation = Quaternion.identity;

        // Le Cube est plus simple que le Cylindre car Scale 1 = 1 mètre.
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "SabreMesh";
        Destroy(cube.GetComponent<Collider>());
        sabreMesh = cube.transform;
        sabreMesh.SetParent(sabreVisual.transform);
        
        // On règle le pivot pour que le cube s'étende vers l'avant sur Z.
        // Un cube de scale 1 s'étend de -0.5 à 0.5. 
        // On le décale de +0.5 pour que sa base commence à 0.
        sabreMesh.localPosition = new Vector3(0, 0, 0.5f);
        sabreMesh.localRotation = Quaternion.identity;
        sabreMesh.localScale = new Vector3(sabreWidth, sabreWidth, 1f);

        var rend = cube.GetComponent<MeshRenderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = sabreColor;
        
        sabreVisual.SetActive(false);

        // Boule lumineuse
        sabreGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sabreGlow.name = "SabreGlow";
        Destroy(sabreGlow.GetComponent<Collider>());
        sabreGlow.transform.localScale = Vector3.one * (sabreWidth * 1.5f);
        var glowRend = sabreGlow.GetComponent<MeshRenderer>();
        glowRend.material = new Material(Shader.Find("Sprites/Default"));
        glowRend.material.color = new Color(sabreColor.r, sabreColor.g, sabreColor.b, 0.8f);
        sabreGlow.SetActive(false);

        // Sources audio
        sabreSfxSource = gameObject.AddComponent<AudioSource>();
        sabreSfxSource.playOnAwake = false;
        sabreSfxSource.volume = 0.7f;
        sabreHumSource = gameObject.AddComponent<AudioSource>();
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
        if (crosshair != null) 
        {
            bool shouldBeVisible = (weapon == WeaponType.Couteaux && GameManager.Instance != null && GameManager.Instance.isPlaying);
            crosshair.SetActive(shouldBeVisible);
        }

        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid) return;

        if (!device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerValue))
            return;

        // --- NOUVEAU : Si on n'est pas en train de jouer, on désactive le sabre et on arrête là ---
        if (GameManager.Instance != null && !GameManager.Instance.isPlaying)
        {
            SetSabreActive(false);
            return;
        }

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

            // ===== SOLUTION RADICALE : LINE RENDERER EN WORLD SPACE =====
            // Le LineRenderer en World Space garantit 2000m réels 
            // peu importe les échelles du contrôleur parent (souvent < 1 en VR).
            var lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.enabled = true;
                lr.useWorldSpace = true;
                lr.startWidth = sabreWidth;
                lr.endWidth = sabreWidth;
                lr.startColor = sabreColor;
                lr.endColor = sabreColor;
                lr.positionCount = 2;
                lr.SetPosition(0, transform.position);
                lr.SetPosition(1, transform.position + transform.forward * 2000.0f);
            }

            // On désactive l'ancien Cube qui ne marchait pas à cause des échelles parent
            if (sabreVisual != null) sabreVisual.SetActive(false);

            // --- FIX TRANCHAGE : On recule l'origine et on force le mask ---
            Vector3 origin = transform.position - transform.forward * 0.25f;
            Vector3 direction = transform.forward;
            
            // Si le masque est vide (0), on force sur "Tout" (-1) pour être sûr de toucher les fruits
            int finalMask = (sliceLayerMask.value == 0) ? -1 : sliceLayerMask.value;

            // === DÉTECTION ET TRANCHAGE ===
            // Most reliable: overlap a capsule along the blade.
            float radius = Mathf.Clamp(sabreSliceRadius, 0.02f, 0.20f);
            float len = Mathf.Clamp(sabreHitLength, 0.5f, 25f);
            var p1 = origin;
            var p2 = origin + direction * len;

            int hits = Physics.OverlapCapsuleNonAlloc(
                p1,
                p2,
                radius,
                sabreOverlapBuffer,
                finalMask,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hits; i++)
            {
                var col = sabreOverlapBuffer[i];
                if (col == null) continue;

                int id = col.gameObject.GetInstanceID();
                if (recentlySlicedIds.Contains(id)) continue;

                // Approx contact point for VFX
                var contact = col.ClosestPoint(origin);

                FruitTarget fruit = col.GetComponent<FruitTarget>();
                if (fruit != null)
                {
                    fruit.Slice(direction, contact, direction * 10f);
                    recentlySlicedIds.Add(id);
                    continue;
                }

                Bomb bomb = col.GetComponent<Bomb>();
                if (bomb != null)
                {
                    bomb.Slice();
                    recentlySlicedIds.Add(id);
                }
            }

            if (sabreGlow != null)
            {
                // On affiche la boule à 20m
                sabreGlow.transform.position = origin + direction * 20f;
                sabreGlow.transform.localScale = Vector3.one * (sabreWidth * 2.5f);
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

        if (sabreVisual != null) sabreVisual.SetActive(active);
        if (sabreGlow != null) sabreGlow.SetActive(active);

        // NOUVEAU : On active/désactive le composant LineRenderer (le trait infini)
        var lr = GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = active;

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
