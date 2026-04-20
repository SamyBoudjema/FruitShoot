using UnityEngine;

public class Bomb : MonoBehaviour
{
    public float timePenalty = 10f;
    public GameObject explosionVFX;
    public AudioClip explosionSound;

    public void Slice()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddBombHit(); // S'occupe désormais des Strikes
        }

        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position, Quaternion.identity);
        }
        else
        {
            CreateDynamicExplosionVFX(transform.position);
        }

        if (explosionSound != null)
        {
            // On joue le son à la position de la caméra pour qu'il soit bien fort (effet d'impact direct)
            Vector3 soundPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(explosionSound, soundPos, 1.0f);
        }

        Destroy(gameObject);
    }

    private void CreateDynamicExplosionVFX(Vector3 pos)
    {
        var splash = new GameObject("BombExplosion");
        splash.transform.position = pos;
        var ps = splash.AddComponent<ParticleSystem>();
        var pr = splash.GetComponent<ParticleSystemRenderer>();
        
        // Utilise un shader universel pour éviter les "carrés violets" de shader cassé (ex: URP/HDRP)
        pr.material = new Material(Shader.Find("Sprites/Default"));

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startColor = new Color(1f, 0.3f, 0f, 0.9f); // Feu / Explosion
        
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40, 60) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        // Auto destroy
        Destroy(splash, 1.5f);
    }

    private bool hasTouchedGround = false;

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

    private void OnCollisionEnter(Collision collision)
    {
        if (hasTouchedGround) return;
        
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
            Destroy(gameObject, 0.1f);
        }
    }
}
