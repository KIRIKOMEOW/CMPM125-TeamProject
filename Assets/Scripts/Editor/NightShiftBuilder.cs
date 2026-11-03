using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

// Frontend one-click builder: layers, generated art/audio, prefabs, the arena scene and all UI.
// Re-running it regenerates everything from scratch, so tweak values here rather than by hand.
public static partial class NightShiftBuilder
{
    const string PrefabFolder = "Assets/Prefabs";
    const string MaterialFolder = "Assets/Materials";
    const string ScenePath = "Assets/Scenes/NightShift.unity";

    // Layer indices (User Layers start at 6).
    const int PlayerLayer = 6, EnemyLayer = 7, BladeLayer = 8, WorldLayer = 9, PropLayer = 10, PickupLayer = 11, ProjectileLayer = 12;

    const float HalfX = 15f, HalfY = 9f;

    static Dictionary<string, Sprite> sprites;
    static Font font;
    static Material flashMaterial, trailMaterial;

    [MenuItem("Night Shift/Build Everything")]
    public static void BuildEverything()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory("Assets/Scenes");

        SetupLayers();
        sprites = PixelArt.GenerateAll();
        Dictionary<SfxId, AudioClip> sfx = SfxSynth.GenerateEffects();
        AudioClip music = SfxSynth.GenerateMusic();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateMaterials();

        GameObject blade = BuildBlade();
        GameObject bolt = BuildProjectile();
        GameObject coin = BuildPickup("Coin", "coin");
        GameObject medkit = BuildPickup("Medkit", "medkit");
        GameObject rusher = BuildEnemy<RusherAI>("Rusher", "rusher", 0.32f, false, null);
        GameObject guard = BuildEnemy<GuardBotAI>("GuardBot", "guardbot", 0.34f, false, bolt);
        GameObject brute = BuildEnemy<BruteAI>("Brute", "brute", 0.55f, true, null);
        GameObject crate = BuildCrate();
        GameObject barrel = BuildBarrel();

        BuildScene(blade, bolt, coin, medkit, rusher, guard, brute, crate, barrel, sfx, music);
        AssetDatabase.SaveAssets();
        Debug.Log("Night Shift: build complete -> " + ScenePath);
    }

    // ---------- Project settings ----------

    static void SetupLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        string[] names = { "Player", "Enemy", "Blade", "World", "Prop", "Pickup", "EnemyProjectile" };
        for (int i = 0; i < names.Length; i++)
            layers.GetArrayElementAtIndex(PlayerLayer + i).stringValue = names[i];
        tagManager.ApplyModifiedPropertiesWithoutUndo();

        // Blades and bolts move by casting, pickups only need the player; everything else collides normally.
        int[] all = { 0, PlayerLayer, EnemyLayer, BladeLayer, WorldLayer, PropLayer, PickupLayer, ProjectileLayer };
        foreach (int a in all)
            foreach (int b in all)
            {
                bool ignore = a == BladeLayer || b == BladeLayer || a == ProjectileLayer || b == ProjectileLayer;
                if (a == PickupLayer || b == PickupLayer)
                    ignore = !((a == PickupLayer && b == PlayerLayer) || (b == PickupLayer && a == PlayerLayer));
                Physics2D.IgnoreLayerCollision(a, b, ignore);
            }
        foreach (Object settings in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset"))
            EditorUtility.SetDirty(settings);
    }

    static int Mask(params int[] layers)
    {
        int mask = 0;
        foreach (int layer in layers) mask |= 1 << layer;
        return mask;
    }

    static void CreateMaterials()
    {
        flashMaterial = SaveMaterial("HitFlash", Shader.Find("GUI/Text Shader"), Color.white);
        trailMaterial = SaveMaterial("BladeTrail", Shader.Find("Sprites/Default"), Color.white);
    }

    static Material SaveMaterial(string name, Shader shader, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        var mat = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------- Prefabs ----------

    static GameObject BuildBlade()
    {
        var root = new GameObject("Blade") { layer = BladeLayer };
        root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.12f;
        col.isTrigger = true;
        Blade blade = root.AddComponent<Blade>();
        Set(blade, "wallLayers", Mask(WorldLayer, PropLayer));
        Set(blade, "hitLayers", Mask(EnemyLayer));

        GameObject spinner = Child(root, "Sprite", Vector3.zero);
        SpriteRenderer sr = spinner.AddComponent<SpriteRenderer>();
        sr.sprite = sprites["blade"];
        sr.sortingOrder = 5;

        var trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.12f;
        trail.startWidth = 0.16f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.emitting = false;
        trail.sortingOrder = 4;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.75f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        BladeVisual visual = root.AddComponent<BladeVisual>();
        Set(visual, "spinner", spinner.transform);
        Set(visual, "trail", trail);
        return SavePrefab(root, "Blade");
    }

    static GameObject BuildProjectile()
    {
        var root = new GameObject("GuardBolt") { layer = ProjectileLayer };
        root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.09f;
        col.isTrigger = true;
        EnemyProjectile projectile = root.AddComponent<EnemyProjectile>();
        Set(projectile, "blockingLayers", Mask(WorldLayer, PropLayer));
        Set(projectile, "hitLayers", Mask(PlayerLayer));
        SpriteRenderer sr = Child(root, "Sprite", Vector3.zero).AddComponent<SpriteRenderer>();
        sr.sprite = sprites["bolt"];
        sr.sortingOrder = 6;
        return SavePrefab(root, "GuardBolt");
    }

    static GameObject BuildPickup(string name, string spriteKey)
    {
        var root = new GameObject(name) { layer = PickupLayer };
        root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.25f;
        col.isTrigger = true;
        root.AddComponent<Pickup>();
        SpriteRenderer sr = Child(root, "Sprite", Vector3.zero).AddComponent<SpriteRenderer>();
        sr.sprite = sprites[spriteKey];
        sr.sortingOrder = -2;
        return SavePrefab(root, name);
    }

    static GameObject BuildEnemy<T>(string name, string spriteKey, float radius, bool big, GameObject bolt) where T : EnemyBase
    {
        var root = new GameObject(name) { layer = EnemyLayer };
        T ai = root.AddComponent<T>();                  // RequireComponent adds Rigidbody2D + Health
        var rb = root.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.mass = big ? 3f : 1f;
        rb.linearDamping = 2f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = radius;
        col.offset = new Vector2(0f, big ? -0.3f : -0.16f);

        GameObject shadow = Child(root, "Shadow", new Vector3(0f, big ? -0.62f : -0.42f, 0f));
        SpriteRenderer shadowSr = shadow.AddComponent<SpriteRenderer>();
        shadowSr.sprite = sprites[big ? "shadow_big" : "shadow"];
        shadowSr.sortingOrder = -5;

        GameObject body = Child(root, "Body", Vector3.zero);
        SpriteRenderer bodySr = body.AddComponent<SpriteRenderer>();
        bodySr.sprite = sprites[spriteKey];

        Health health = root.GetComponent<Health>();
        Set(health, "flashMaterial", flashMaterial);
        SetArray(health, "spriteRenderers", new Object[] { bodySr });

        if (ai is RusherAI)
            Set(ai, "enemyType", (int)EnemyType.Rusher);
        if (ai is GuardBotAI)
        {
            Set(ai, "sightBlockingLayers", Mask(WorldLayer, PropLayer));
            Set(ai, "projectilePrefab", bolt);
        }
        if (ai is BruteAI)
            Set(ai, "chargeBlockingLayers", Mask(WorldLayer, PropLayer));

        CharacterVisual visual = root.AddComponent<CharacterVisual>();
        Set(visual, "body", body.transform);
        Set(visual, "bodySprite", bodySr);

        if (ai is GuardBotAI || ai is BruteAI)
        {
            GameObject labelObject = Child(root, "State Label", new Vector3(0f, big ? 0.95f : 0.65f, 0f));
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 40;
            label.characterSize = 0.05f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.text = "Patrol";
            MeshRenderer mr = labelObject.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 60;
            Set(labelObject.AddComponent<EnemyStateLabel>(), "label", label);
        }
        return SavePrefab(root, name);
    }

    static GameObject BuildCrate()
    {
        var root = new GameObject("Crate") { layer = PropLayer };
        root.AddComponent<Crate>();                      // RequireComponent adds Rigidbody2D + BoxCollider2D
        var rb = root.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        root.GetComponent<BoxCollider2D>().size = new Vector2(0.94f, 0.94f);
        SpriteRenderer sr = Child(root, "Sprite", Vector3.zero).AddComponent<SpriteRenderer>();
        sr.sprite = sprites["crate"];
        return SavePrefab(root, "Crate");
    }

    static GameObject BuildBarrel()
    {
        var root = new GameObject("ExplosiveBarrel") { layer = PropLayer };
        ExplosiveBarrel barrel = root.AddComponent<ExplosiveBarrel>();
        root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var col = root.GetComponent<CircleCollider2D>();
        col.radius = 0.33f;
        col.offset = new Vector2(0f, -0.08f);
        Set(barrel, "affectedLayers", Mask(PlayerLayer, EnemyLayer, PropLayer));
        GameObject shadow = Child(root, "Shadow", new Vector3(0f, -0.42f, 0f));
        SpriteRenderer shadowSr = shadow.AddComponent<SpriteRenderer>();
        shadowSr.sprite = sprites["shadow"];
        shadowSr.sortingOrder = -5;
        SpriteRenderer sr = Child(root, "Sprite", Vector3.zero).AddComponent<SpriteRenderer>();
        sr.sprite = sprites["barrel"];
        return SavePrefab(root, "ExplosiveBarrel");
    }

    // ---------- Scene ----------

    static void BuildScene(GameObject blade, GameObject bolt, GameObject coin, GameObject medkit,
        GameObject rusher, GameObject guard, GameObject brute, GameObject crate, GameObject barrel,
        Dictionary<SfxId, AudioClip> sfx, AudioClip music)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera rig: CameraFollow moves the parent, CameraShake offsets the child camera.
        var rig = new GameObject("Camera Rig");
        GameObject camObject = Child(rig, "Main Camera", new Vector3(0f, 0f, -10f));
        camObject.tag = "MainCamera";
        Camera cam = camObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 360f / 2f / PixelArt.PPU;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(12, 13, 20, 255);
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = Vector3.up;
        camObject.AddComponent<AudioListener>();
        PixelPerfectCamera ppc = camObject.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = PixelArt.PPU;
        ppc.refResolutionX = 640;
        ppc.refResolutionY = 360;
        ppc.upscaleRT = true;
        Set(camObject.AddComponent<CameraShake>(), "shakeTarget", camObject.transform);

        // Arena.
        var arena = new GameObject("Arena");
        TiledSprite(arena, "Floor", "floor", Vector2.zero, new Vector2(HalfX * 2f + 2f, HalfY * 2f + 2f), -100, false);
        Wall(arena, "Wall Top", new Vector2(0f, HalfY + 0.5f), new Vector2(HalfX * 2f + 2f, 1f));
        Wall(arena, "Wall Bottom", new Vector2(0f, -HalfY - 0.5f), new Vector2(HalfX * 2f + 2f, 1f));
        Wall(arena, "Wall Left", new Vector2(-HalfX - 0.5f, 0f), new Vector2(1f, HalfY * 2f));
        Wall(arena, "Wall Right", new Vector2(HalfX + 0.5f, 0f), new Vector2(1f, HalfY * 2f));
        foreach (Vector2 p in new[] { new Vector2(-6f, 4.5f), new Vector2(6f, 4.5f), new Vector2(-6f, -4.5f), new Vector2(6f, -4.5f) })
            Wall(arena, "Pillar", p, new Vector2(1.5f, 1.5f));
        Wall(arena, "Shelf North", new Vector2(0f, 6f), new Vector2(6f, 1f));
        Wall(arena, "Shelf South", new Vector2(0f, -6f), new Vector2(6f, 1f));

        var props = new GameObject("Props");
        foreach (Vector2 p in new[] { new Vector2(-3f, -2f), new Vector2(3f, 2f), new Vector2(-9f, -5.5f), new Vector2(9f, 5.5f), new Vector2(-12f, 4.5f), new Vector2(12f, -4.5f) })
            Place(crate, props, p);
        foreach (Vector2 p in new[] { new Vector2(-9f, 2f), new Vector2(9f, -2f), new Vector2(0f, 3.5f), new Vector2(-11f, -3f), new Vector2(11f, 3f) })
            Place(barrel, props, p);

        var spawnRoot = new GameObject("Spawn Points");
        var spawnPoints = new List<Transform>();
        foreach (Vector2 p in new[]
        {
            new Vector2(-13.5f, 7.5f), new Vector2(-4f, 7.8f), new Vector2(4f, 7.8f), new Vector2(13.5f, 7.5f),
            new Vector2(-13.8f, 0f), new Vector2(13.8f, 0f),
            new Vector2(-13.5f, -7.5f), new Vector2(-4f, -7.8f), new Vector2(4f, -7.8f), new Vector2(13.5f, -7.5f),
        })
        {
            GameObject point = Child(spawnRoot, "Spawn", p);
            spawnPoints.Add(point.transform);
        }

        PlayerController player = BuildPlayer(blade, cam);

        CameraFollow follow = rig.AddComponent<CameraFollow>();
        Set(follow, "target", player.transform);
        Set(follow, "viewCamera", cam);
        Set(follow, "arenaMin", new Vector2(-HalfX - 1f, -HalfY - 1f));
        Set(follow, "arenaMax", new Vector2(HalfX + 1f, HalfY + 1f));

        // Managers.
        var managers = new GameObject("Managers");
        Set(managers.AddComponent<GameManager>(), "player", player);
        PoolManager pool = managers.AddComponent<PoolManager>();
        SetPrewarm(pool, (blade, 12), (rusher, 16), (guard, 6), (brute, 3), (bolt, 12), (coin, 30), (medkit, 4));
        Set(managers.AddComponent<UpgradeSystem>(), "player", player);
        DropTable drops = managers.AddComponent<DropTable>();
        Set(drops, "coinPrefab", coin);
        Set(drops, "healthPrefab", medkit);
        WaveSpawner spawner = managers.AddComponent<WaveSpawner>();
        Set(spawner, "rusherPrefab", rusher.GetComponent<EnemyBase>());
        Set(spawner, "guardBotPrefab", guard.GetComponent<EnemyBase>());
        Set(spawner, "brutePrefab", brute.GetComponent<EnemyBase>());
        Set(spawner, "player", player);
        SetArray(spawner, "spawnPoints", spawnPoints.ToArray());
        managers.AddComponent<GameFeel>();
        managers.AddComponent<HitStop>();
        DebugOverlay debug = managers.AddComponent<DebugOverlay>();
        SetArray(debug, "spawnPoints", spawnPoints.ToArray());
        Set(debug, "markerSprite", sprites["marker"]);

        var vfx = new GameObject("VFX");
        Set(vfx.AddComponent<PixelVfx>(), "pixelSprite", sprites["pixel"]);

        var audioRoot = new GameObject("AudioManager");
        AudioManager audio = audioRoot.AddComponent<AudioManager>();
        SetSfx(audio, sfx);
        Set(audio, "musicClip", music);

        BuildUI();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static PlayerController BuildPlayer(GameObject bladePrefab, Camera cam)
    {
        var root = new GameObject("Kestrel") { layer = PlayerLayer };
        PlayerController player = root.AddComponent<PlayerController>(); // adds Rigidbody2D, Health, BladeRecall
        var rb = root.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = 1f;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.28f;
        col.offset = new Vector2(0f, -0.2f);

        GameObject shadow = Child(root, "Shadow", new Vector3(0f, -0.42f, 0f));
        SpriteRenderer shadowSr = shadow.AddComponent<SpriteRenderer>();
        shadowSr.sprite = sprites["shadow"];
        shadowSr.sortingOrder = -5;

        GameObject body = Child(root, "Body", Vector3.zero);
        SpriteRenderer bodySr = body.AddComponent<SpriteRenderer>();
        bodySr.sprite = sprites["kestrel"];

        GameObject aimPivot = Child(root, "Aim Pivot", new Vector3(0f, -0.05f, 0f));
        GameObject throwPoint = Child(aimPivot, "Throw Point", new Vector3(0.45f, 0f, 0f));
        SpriteRenderer arrow = Child(aimPivot, "Aim Arrow", new Vector3(0.8f, 0f, 0f)).AddComponent<SpriteRenderer>();
        arrow.sprite = sprites["aim"];
        arrow.sortingOrder = 3;

        Set(player, "bladePrefab", bladePrefab);
        Set(player, "aimCamera", cam);
        Set(player, "bodySprite", bodySr);
        Set(player, "aimPivot", aimPivot.transform);
        Set(player, "throwPoint", throwPoint.transform);

        Health health = root.GetComponent<Health>();
        Set(health, "isPlayer", true);
        Set(health, "flashMaterial", flashMaterial);
        SetArray(health, "spriteRenderers", new Object[] { bodySr });

        CharacterVisual visual = root.AddComponent<CharacterVisual>();
        Set(visual, "body", body.transform);
        Set(visual, "bodySprite", bodySr);
        Set(visual, "flipFromVelocity", false);
        return player;
    }

    static void Wall(GameObject parent, string name, Vector2 position, Vector2 size)
    {
        GameObject wall = TiledSprite(parent, name, "wall", position, size, 0, true);
        wall.layer = WorldLayer;
        wall.AddComponent<BoxCollider2D>().size = size;
    }

    static GameObject TiledSprite(GameObject parent, string name, string key, Vector2 position, Vector2 size, int order, bool sortWithWorld)
    {
        GameObject go = Child(parent, name, position);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[key];
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.sortingOrder = order;
        return go;
    }

    static void Place(GameObject prefab, GameObject parent, Vector2 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent.transform, false);
        instance.transform.position = position;
    }

    // ---------- Helpers ----------

    static GameObject Child(GameObject parent, string name, Vector3 localPosition)
    {
        var go = new GameObject(name) { layer = parent.layer };
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPosition;
        return go;
    }

    static GameObject SavePrefab(GameObject root, string name)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"Night Shift builder: {target.GetType().Name} has no serialized field '{field}'");
            return;
        }
        switch (value)
        {
            case Object o: p.objectReferenceValue = o; break;
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case bool b: p.boolValue = b; break;
            case Vector2 v: p.vector2Value = v; break;
            case Color c: p.colorValue = c; break;
            case string s: p.stringValue = s; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"Night Shift builder: {target.GetType().Name} has no serialized array '{field}'");
            return;
        }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetPrewarm(PoolManager pool, params (GameObject prefab, int count)[] entries)
    {
        var so = new SerializedObject(pool);
        SerializedProperty p = so.FindProperty("prewarmEntries");
        p.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            SerializedProperty e = p.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
            e.FindPropertyRelative("count").intValue = entries[i].count;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSfx(AudioManager audio, Dictionary<SfxId, AudioClip> clips)
    {
        var so = new SerializedObject(audio);
        SerializedProperty p = so.FindProperty("soundEffects");
        p.arraySize = clips.Count;
        int i = 0;
        foreach (KeyValuePair<SfxId, AudioClip> pair in clips)
        {
            SerializedProperty e = p.GetArrayElementAtIndex(i++);
            e.FindPropertyRelative("id").intValue = (int)pair.Key;
            e.FindPropertyRelative("clip").objectReferenceValue = pair.Value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
