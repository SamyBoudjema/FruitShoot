using UnityEngine;

public class FruitTarget : MonoBehaviour
{
    public string ingredientName = "Pomme";
    public int scoreValue = 10;
    public GameObject explosionPrefab; // Prefab du système de particules (VFX d'explosion)
    public AudioClip explosionSound;    // Son d'écrasement de fruit

    public void Slice(Vector3 sliceDirection, Vector3 contactPoint, Vector3 sliceVelocity)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ProcessFruitSlice(ingredientName);
        }

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, transform.rotation);
        }
        else
        {
            CreateDynamicSplashVFX(transform.position);
        }

        if (explosionSound != null)
        {
            // On joue le son à la position de la caméra pour qu'il soit bien présent (effet ASMR/Proche)
            Vector3 soundPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(explosionSound, soundPos, 1.0f);
        }

        Destroy(gameObject);
    }

    private void Start()
    {
        // Sécurité : détruit le fruit après 5 secondes s'il n'est pas coupé
        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        // Destruction immédiate si l'objet descend trop bas (Y = -2m)
        if (transform.position.y < -2.0f)
        {
            Destroy(gameObject);
        }
    }

    private bool hasTouchedGround = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasTouchedGround) return;

        // S'il touche un objet nommé Sol, Ground ou Floor, on le détruit après 1 seconde
        string colName = collision.gameObject.name.ToLower();
        string colTag = collision.gameObject.tag.ToLower();

        if (colName.Contains("sol") || 
            colName.Contains("ground") || 
            colName.Contains("floor") ||
            colTag.Contains("floor") ||
            colTag.Contains("sol") ||
            collision.gameObject.layer == LayerMask.NameToLayer("Floor"))
        {
            hasTouchedGround = true;
            Destroy(gameObject, 0.1f); // Presque instantané pour laisser un micro-temps de calcul si besoin
        }
    }

    private void CreateDynamicSplashVFX(Vector3 pos)
    {
        var splash = new GameObject("FruitSplash");
        splash.transform.position = pos;
        var ps = splash.AddComponent<ParticleSystem>();
        var pr = splash.GetComponent<ParticleSystemRenderer>();
        
        // Utilise un shader universel pour éviter les "carrés violets" de shader cassé (ex: URP/HDRP)
        pr.material = new Material(Shader.Find("Sprites/Default"));

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(0f, 0.6f, 1f, 0.8f); // Splash d'eau/jus
        
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 30) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        // Auto destroy
        Destroy(splash, 1.5f);
    }
}
