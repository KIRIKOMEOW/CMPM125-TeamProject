using UnityEngine;

public enum PickupType { Coin, Health }

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class Pickup : MonoBehaviour, IPoolable
{
    [Header("Coin Attraction")]
    [SerializeField] private float magnetRange = 12f;
    [SerializeField] private float magnetSpeed = 12f;
    [SerializeField] private float collectDistance = 0.35f;

    private Rigidbody2D body;
    private PlayerController target;
    private bool initialized;
    private bool collected;
    private int spawnId;

    public PickupType Type { get; private set; }
    public int Amount { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        GetComponent<CircleCollider2D>().isTrigger = true;
    }

    private void OnEnable()
    {
        GameEvents.GameStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameStateChanged -= OnGameStateChanged;
    }

    public void OnSpawn()
    {
        spawnId++;
        initialized = false;
        collected = false;
        Amount = 0;
        target = null;
        body.position = transform.position;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    public void Initialize(PickupType type, int amount, PlayerController player)
    {
        Type = type;
        Amount = Mathf.Max(0, amount);
        target = player;
        initialized = true;
    }

    public void OnDespawn()
    {
        initialized = false;
        collected = false;
        Amount = 0;
        target = null;
        body.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        GameManager manager = GameManager.Instance;
        if (!initialized || collected || manager == null || manager.State != GameState.Playing)
            return;
        if (target == null)
            target = manager.Player;
        if (target == null || target.Health.IsDead)
            return;

        Vector2 toPlayer = (Vector2)target.transform.position - body.position;
        float distance = toPlayer.magnitude;
        if (distance <= collectDistance)
        {
            TryCollect(target);
            return;
        }
        if (Type == PickupType.Coin && distance <= magnetRange)
            body.MovePosition(Vector2.MoveTowards(body.position, target.transform.position, magnetSpeed * Time.fixedDeltaTime));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CollectFromCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CollectFromCollider(other);
    }

    private void CollectFromCollider(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null && other.attachedRigidbody != null)
            player = other.attachedRigidbody.GetComponent<PlayerController>();
        if (player != null)
            TryCollect(player);
    }

    public bool TryCollect(PlayerController player)
    {
        GameManager manager = GameManager.Instance;
        if (!isActiveAndEnabled || !initialized || collected || Amount <= 0 || manager == null
            || player == null || player.Health == null || player.Health.IsDead)
            return false;

        bool settlingCoins = Type == PickupType.Coin
            && (manager.State == GameState.Shop || manager.State == GameState.Victory);
        if (manager.State != GameState.Playing && !settlingCoins)
            return false;
        if (Type == PickupType.Health && player.Health.CurrentHP >= player.Health.MaxHP)
            return false;

        collected = true;
        int collectedSpawnId = spawnId;
        if (Type == PickupType.Coin)
        {
            int amount = Amount;
            Vector2 position = transform.position;
            manager.AddCoins(amount);
            GameEvents.RaiseCoinCollected(position, amount);
        }
        else
            player.Health.Heal(Amount);

        if (spawnId == collectedSpawnId && isActiveAndEnabled)
            Despawn();
        return true;
    }

    private void OnGameStateChanged(GameState state)
    {
        // Menus freeze physics, so settle remaining gold before shopping can begin.
        if (Type != PickupType.Coin || (state != GameState.Shop && state != GameState.Victory))
            return;
        PlayerController player = target != null ? target : GameManager.Instance != null ? GameManager.Instance.Player : null;
        TryCollect(player);
    }

    private void Despawn()
    {
        if (PoolManager.Instance != null)
            PoolManager.Instance.Return(gameObject);
        else
        {
            OnDespawn();
            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        magnetRange = Mathf.Max(0f, magnetRange);
        magnetSpeed = Mathf.Max(0f, magnetSpeed);
        collectDistance = Mathf.Max(0.01f, collectDistance);
    }
}
