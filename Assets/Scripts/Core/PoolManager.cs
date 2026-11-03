using System;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    [Serializable]
    private class PrewarmEntry
    {
        public GameObject prefab;
        [Min(0)] public int count = 5;
    }

    private sealed class PoolEntry
    {
        public GameObject prefab;
        public MonoBehaviour[] behaviours;
        public bool spawned;
        public int generation;
    }

    [Header("Prewarming")]
    [SerializeField] private PrewarmEntry[] prewarmEntries = new PrewarmEntry[0];

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, PoolEntry> entries = new Dictionary<GameObject, PoolEntry>();
    private readonly HashSet<GameObject> spawnedObjects = new HashSet<GameObject>();
    private Transform storage;
    private bool returningAll;

    public static PoolManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        GameObject storageObject = new GameObject("Pooled Objects");
        storageObject.transform.SetParent(transform, false);
        storageObject.SetActive(false);
        storage = storageObject.transform;
    }

    private void Start()
    {
        if (Instance != this || prewarmEntries == null)
            return;

        foreach (PrewarmEntry entry in prewarmEntries)
        {
            if (entry != null)
                Prewarm(entry.prefab, entry.count);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public GameObject Get(GameObject prefab, Vector2 pos, Quaternion rot)
    {
        if (prefab == null || storage == null || returningAll)
            return null;

        Queue<GameObject> pool = GetQueue(prefab);
        GameObject instance = null;
        while (pool.Count > 0 && instance == null)
            instance = pool.Dequeue();

        if (instance == null)
            instance = CreateInstance(prefab);

        PoolEntry entry = entries[instance];
        entry.spawned = true;
        int generation = ++entry.generation;
        spawnedObjects.Add(instance);
        instance.transform.SetParent(null, false);
        instance.transform.SetPositionAndRotation(pos, rot);
        instance.SetActive(true);

        // Awake and OnEnable must run before gameplay receives its spawn callback.
        foreach (MonoBehaviour behaviour in entry.behaviours)
        {
            if (instance == null || !entry.spawned || entry.generation != generation)
                return null;

            if (behaviour != null && behaviour is IPoolable poolable)
                poolable.OnSpawn();
        }

        return instance != null && entry.spawned && entry.generation == generation ? instance : null;
    }

    public void Return(GameObject obj)
    {
        if (obj == null || obj == gameObject)
            return;

        if (!entries.TryGetValue(obj, out PoolEntry entry))
        {
            if (!obj.activeSelf)
                return;

            // Scene-placed objects have no source prefab, so they can only be deactivated.
            entry = new PoolEntry
            {
                behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true),
                spawned = true
            };
            entries.Add(obj, entry);
        }

        if (!entry.spawned)
            return;

        entry.spawned = false;
        spawnedObjects.Remove(obj);
        foreach (MonoBehaviour behaviour in entry.behaviours)
        {
            if (behaviour != null && behaviour is IPoolable poolable)
                poolable.OnDespawn();
        }

        if (obj == null)
            return;

        obj.SetActive(false);
        if (entry.prefab != null && storage != null)
        {
            obj.transform.SetParent(storage, false);
            GetQueue(entry.prefab).Enqueue(obj);
        }
    }

    public void ReturnAll()
    {
        if (returningAll)
            return;

        returningAll = true;
        try
        {
            // Despawn callbacks may return other objects, so never enumerate the live set.
            GameObject[] active = new GameObject[spawnedObjects.Count];
            spawnedObjects.CopyTo(active);
            foreach (GameObject obj in active)
                Return(obj);

            spawnedObjects.Clear();
        }
        finally
        {
            returningAll = false;
        }
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0 || storage == null || returningAll)
            return;

        Queue<GameObject> pool = GetQueue(prefab);
        int previousCount = pool.Count;
        for (int i = 0; i < previousCount; i++)
        {
            GameObject instance = pool.Dequeue();
            if (instance != null)
                pool.Enqueue(instance);
        }

        while (pool.Count < count)
            pool.Enqueue(CreateInstance(prefab));
    }

    private Queue<GameObject> GetQueue(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools.Add(prefab, pool);
        }

        return pool;
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        // The inactive parent prevents an active prefab from enabling before it is tracked.
        GameObject instance = Instantiate(prefab, storage);
        instance.SetActive(false);
        entries.Add(instance, new PoolEntry
        {
            prefab = prefab,
            behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true)
        });
        return instance;
    }
}
