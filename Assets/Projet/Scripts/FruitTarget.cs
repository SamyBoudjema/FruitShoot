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
            GameManager.Instance.AddScore(scoreValue);
            GameManager.Instance.AddFruitSliced();
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
            // Create a temporary object to play the sound at the explosion location
            // so it doesn't get cut off when the fruit is destroyed
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }

        Destroy(gameObject);
    }

    private void Update()
    {
        // Sécurité abaissée à -3 pour éviter qu'il disparaisse avant de toucher le vrai sol
        if (transform.position.y < -3f)
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
            colTag.Contains("sol"))
        {
            hasTouchedGround = true;
            Destroy(gameObject, 0.5f); // Délai de 0.5 seconde avant destruction
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
