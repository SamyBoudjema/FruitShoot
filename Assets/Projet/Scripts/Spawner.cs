using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public List<GameObject> prefabsToSpawn;
    [Header("Spawn pacing (Start -> End of game)")]
    public float startMinSpawnDelay = 2.0f;
    public float startMaxSpawnDelay = 3.5f;
    public float endMinSpawnDelay = 0.35f;
    public float endMaxSpawnDelay = 0.9f;

    [Header("Launch force (Start -> End of game)")]
    public float startSpawnForceMin = 4.5f;
    public float startSpawnForceMax = 7.0f;
    public float endSpawnForceMin = 7.0f;
    public float endSpawnForceMax = 11.0f;

    [Header("Falling feel (Start -> End of game)")]
    [Tooltip("Higher drag makes fruits fall slower.")]
    public float startDrag = 2.2f;
    public float endDrag = 0.6f;

    [Tooltip("Optional curve for difficulty progression across the match (0..1). If empty, uses linear.")]
    public AnimationCurve difficultyCurve;

    public float spawnTorqueMax = 5f;
    public Transform spawnArea; 
    
    private Coroutine spawnCoroutine;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart.AddListener(StartSpawning);
            GameManager.Instance.OnGameOver.AddListener(StopSpawning);
        }
    }

    public void StartSpawning()
    {
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            var t = GetDifficulty01();
            var delayMult = GameManager.Instance != null ? GameManager.Instance.GetDifficultyDelayMultiplier() : 1f;
            
            // Délai entre les vagues complètes (multiplié un peu pour compenser la quantité)
            var minDelay = Mathf.Lerp(startMinSpawnDelay, endMinSpawnDelay, t) * delayMult * 2.0f;
            var maxDelay = Mathf.Lerp(startMaxSpawnDelay, endMaxSpawnDelay, t) * delayMult * 2.5f;
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            if (prefabsToSpawn.Count > 0)
            {
                // Détermine la taille de la vague
                int waveSize = Random.Range(2, 5);
                if (GameManager.Instance != null && GameManager.Instance.difficulty == Difficulty.Difficile)
                    waveSize = Random.Range(4, 7);

                // Crache tous les éléments de la vague très rapidement
                for (int i = 0; i < waveSize; i++)
                {
                    SpawnSingleItem(t);
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                }
            }
        }
    }

    private void SpawnSingleItem(float t)
    {
        GameObject prefab = prefabsToSpawn[Random.Range(0, prefabsToSpawn.Count)];
        
        if (GameManager.Instance != null && GameManager.Instance.difficulty == Difficulty.Difficile)
        {
            if (Random.value < 0.30f) // 30% en hard par objet
            {
                var bombPrefab = prefabsToSpawn.Find(p => p.GetComponent<Bomb>() != null);
                if (bombPrefab != null) prefab = bombPrefab;
            }
        }
        
        Vector3 spawnPos = transform.position + new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.3f, 0.3f));
        GameObject spawned = Instantiate(prefab, spawnPos, Random.rotation);
        
        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb != null)
        {
            var forceMult = GameManager.Instance != null ? GameManager.Instance.GetDifficultyForceMultiplier() : 1f;
            var forceMin = Mathf.Lerp(startSpawnForceMin, endSpawnForceMin, t) * forceMult;
            var forceMax = Mathf.Lerp(startSpawnForceMax, endSpawnForceMax, t) * forceMult;
            float force = Random.Range(forceMin, forceMax);
            
            // Direction légèrement conique pour étaler la vague
            Vector3 jumpDir = (Vector3.up + new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.1f, 0.1f))).normalized;
            rb.AddForce(jumpDir * force, ForceMode.Impulse);

            var dragMult = GameManager.Instance != null ? GameManager.Instance.GetDifficultyDragMultiplier() : 1f;
            rb.drag = Mathf.Max(0f, Mathf.Lerp(startDrag, endDrag, t) * dragMult);
        }

        Destroy(spawned, 4f); // 4 secondes au cas où
    }

    private float GetDifficulty01()
    {
        if (GameManager.Instance == null) return 0f;
        var duration = Mathf.Max(0.01f, GameManager.Instance.gameDuration);
        var elapsed = Mathf.Clamp(duration - GameManager.Instance.currentTime, 0f, duration);
        var linear = Mathf.Clamp01(elapsed / duration);

        if (difficultyCurve != null && difficultyCurve.keys != null && difficultyCurve.length > 0)
            return Mathf.Clamp01(difficultyCurve.Evaluate(linear));

        return linear;
    }
}
