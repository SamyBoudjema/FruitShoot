using UnityEngine;

public class KunaiProjectile : MonoBehaviour
{
    public float velocity = 30f;
    private Rigidbody rb;
    private bool hasHit = false;

    void Start()
    {
        // On détruit le Kunai après 3 secondes s'il ne touche rien
        Destroy(gameObject, 3f);

        // Setup du Rigidbody
        rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false; // Plus de gravité, il ira tout droit !
        rb.mass = 0.1f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Empêche de traverser les fruits à grande vitesse
        // Propulsion initiale
        rb.velocity = transform.forward * velocity;
        
        // On s'assure qu'il y a un collider Trigger
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Ajouter une traînée stylisée
        var tr = gameObject.AddComponent<TrailRenderer>();
        tr.time = 0.15f;
        tr.startWidth = 0.05f;
        tr.endWidth = 0f;
        tr.material = new Material(Shader.Find("Sprites/Default"));
        tr.startColor = new Color(0.8f, 0.9f, 1f, 0.7f);
        tr.endColor = new Color(0.8f, 0.9f, 1f, 0f);
    }

    void Update()
    {
        if (rb != null && !hasHit && rb.velocity.sqrMagnitude > 1f)
        {
            // Oriente toujours le couteau dans la direction de sa course
            transform.rotation = Quaternion.LookRotation(rb.velocity);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        FruitTarget fruit = other.GetComponent<FruitTarget>();
        if (fruit != null)
        {
            hasHit = true;
            fruit.Slice(transform.forward, transform.position, rb.velocity);
            Destroy(gameObject);
            return;
        }

        Bomb bomb = other.GetComponent<Bomb>();
        if (bomb != null)
        {
            hasHit = true;
            bomb.Slice();
            Destroy(gameObject);
            return;
        }

        // Si on touche le sol ou un mur, on s'y plante
        string colName = other.gameObject.name.ToLower();
        if (other.CompareTag("Floor") || colName.Contains("sol") || colName.Contains("ground"))
        {
            hasHit = true;
            rb.isKinematic = true; // Stop moving
            Destroy(gameObject, 1f); // Disparait après 1 sec au sol
        }
    }
}
