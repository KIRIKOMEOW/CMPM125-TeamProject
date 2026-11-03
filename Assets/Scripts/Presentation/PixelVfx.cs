using System.Collections.Generic;
using UnityEngine;

// Tiny pooled square-pixel particles for hits, deaths, explosions and coins.
// Uses scaled time on purpose: hit stop freezes the particles along with everything else.
public class PixelVfx : MonoBehaviour
{
    private class Particle
    {
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float life;
        public float maxLife;
        public Color color;
    }

    [SerializeField] private Sprite pixelSprite;
    [SerializeField] private int poolSize = 160;
    [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.55f);
    [SerializeField] private Color recallColor = new Color(1f, 0.55f, 0.15f);
    [SerializeField] private Color deathColor = new Color(0.85f, 0.85f, 0.95f);
    [SerializeField] private Color explosionColor = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private Color coinColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color playerHitColor = new Color(1f, 0.25f, 0.3f);

    private readonly List<Particle> active = new List<Particle>();
    private readonly Stack<Particle> free = new Stack<Particle>();

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = new GameObject("Pixel");
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixelSprite;
            sr.sortingOrder = 40;
            go.SetActive(false);
            free.Push(new Particle { renderer = sr });
        }
    }

    private void OnEnable()
    {
        GameEvents.EnemyHit += OnEnemyHit;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.BarrelExploded += OnBarrelExploded;
        GameEvents.CoinCollected += OnCoinCollected;
        GameEvents.PlayerHit += OnPlayerHit;
        GameEvents.RunReset += ClearAll;
    }

    private void OnDisable()
    {
        GameEvents.EnemyHit -= OnEnemyHit;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.BarrelExploded -= OnBarrelExploded;
        GameEvents.CoinCollected -= OnCoinCollected;
        GameEvents.PlayerHit -= OnPlayerHit;
        GameEvents.RunReset -= ClearAll;
    }

    private void OnEnemyHit(Vector2 position, bool fromRecall) => Burst(position, fromRecall ? 10 : 6, fromRecall ? recallColor : hitColor, 4f, 0.25f);
    private void OnEnemyKilled(Vector2 position, EnemyType type) => Burst(position, type == EnemyType.Brute ? 28 : 16, deathColor, 5f, 0.45f);
    private void OnBarrelExploded(Vector2 position) => Burst(position, 48, explosionColor, 9f, 0.6f);
    private void OnCoinCollected(Vector2 position, int amount) => Burst(position, 4, coinColor, 2.5f, 0.2f);
    private void OnPlayerHit(Vector2 position) => Burst(position, 10, playerHitColor, 4f, 0.3f);

    private void Burst(Vector2 position, int count, Color color, float speed, float life)
    {
        for (int i = 0; i < count && free.Count > 0; i++)
        {
            Particle p = free.Pop();
            p.velocity = Random.insideUnitCircle.normalized * Random.Range(speed * 0.4f, speed);
            p.maxLife = Random.Range(life * 0.6f, life);
            p.life = p.maxLife;
            p.color = color;
            p.renderer.color = color;
            p.renderer.transform.position = position;
            p.renderer.gameObject.SetActive(true);
            active.Add(p);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Particle p = active[i];
            p.life -= dt;
            if (p.life <= 0f)
            {
                Recycle(i);
                continue;
            }
            p.velocity *= 1f - Mathf.Min(1f, 6f * dt);
            Vector3 pos = p.renderer.transform.position + (Vector3)(p.velocity * dt);
            p.renderer.transform.position = pos;
            Color c = p.color;
            c.a = Mathf.Clamp01(p.life / p.maxLife * 1.5f);
            p.renderer.color = c;
        }
    }

    private void Recycle(int index)
    {
        Particle p = active[index];
        p.renderer.gameObject.SetActive(false);
        active.RemoveAt(index);
        free.Push(p);
    }

    private void ClearAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            Recycle(i);
    }
}
