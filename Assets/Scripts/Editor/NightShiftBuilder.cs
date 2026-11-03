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
public static class NightShiftBuilder
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

    // ---------- UI ----------

    static readonly Color TextColor = new Color32(230, 234, 245, 255);
    static readonly Color DimText = new Color32(150, 160, 185, 255);
    static readonly Color Accent = new Color32(255, 160, 60, 255);
    static readonly Color GoldText = new Color32(255, 214, 90, 255);
    static readonly Color DangerText = new Color32(255, 90, 90, 255);

    static void BuildUI()
    {
        var canvasObject = new GameObject("UI Canvas");
        var canvas = canvasObject.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        UIManager ui = canvasObject.AddComponent<UIManager>();

        // HUD.
        RectTransform hud = Panel(root, "HUD", null, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image hurt = Img(hud, "Hurt Overlay", sprites["ui_vignette"], new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        hurt.enabled = false;
        HUDController hudController = hud.gameObject.AddComponent<HUDController>();

        Label(hud, "HP Label", "HP", 9, Accent, TextAnchor.MiddleLeft, TopLeft(10f, -10f), new Vector2(20f, 12f));
        Image hpBack = Img(hud, "HP Back", sprites["ui_panel"], Color.white, TopLeft(28f, -10f), new Vector2(124f, 12f));
        Image hpFill = Img(hpBack.rectTransform, "HP Fill", sprites["ui_fill"], new Color32(220, 60, 70, 255), Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        Text hpText = Label(hpBack.rectTransform, "HP Text", "100/100", 8, TextColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var bladeIcons = new List<Image>();
        for (int i = 0; i < 5; i++)
            bladeIcons.Add(Img(hud, "Blade Icon", sprites["blade"], Color.white, TopLeft(12f + i * 14f, -30f), new Vector2(10f, 10f)));
        Image recallBack = Img(hud, "Recall Back", sprites["ui_panel"], Color.white, TopLeft(10f, -44f), new Vector2(72f, 7f));
        Image recallFill = Img(recallBack.rectTransform, "Recall Fill", sprites["ui_fill"], Accent, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
        recallFill.type = Image.Type.Filled;
        recallFill.fillMethod = Image.FillMethod.Horizontal;
        Text recallText = Label(hud, "Recall Text", "RECALL READY [E]", 7, DimText, TextAnchor.MiddleLeft, TopLeft(86f, -44f), new Vector2(110f, 10f));

        Text waveText = Label(hud, "Wave", "WAVE 1/10", 14, TextColor, TextAnchor.UpperCenter, Anchored(new Vector2(0.5f, 1f), new Vector2(0f, -6f)), new Vector2(200f, 18f));
        Text timerText = Label(hud, "Timer", "0:00", 8, DimText, TextAnchor.UpperCenter, Anchored(new Vector2(0.5f, 1f), new Vector2(0f, -24f)), new Vector2(100f, 12f));

        Img(hud, "Coin Icon", sprites["coin"], Color.white, Anchored(new Vector2(1f, 1f), new Vector2(-60f, -12f)), new Vector2(10f, 10f));
        Text coinsText = Label(hud, "Coins", "0", 12, GoldText, TextAnchor.MiddleLeft, Anchored(new Vector2(1f, 1f), new Vector2(-28f, -12f)), new Vector2(48f, 14f));
        Text enemiesText = Label(hud, "Intruders", "INTRUDERS 0", 8, DimText, TextAnchor.MiddleRight, Anchored(new Vector2(1f, 1f), new Vector2(-38f, -28f)), new Vector2(110f, 12f));

        Text banner = Label(hud, "Banner", "WAVE 1", 28, Accent, TextAnchor.MiddleCenter, Anchored(new Vector2(0.5f, 0.5f), new Vector2(0f, 60f)), new Vector2(400f, 40f));
        banner.enabled = false;
        Label(hud, "Hint", "ESC pause   F1 feel FX   F2 debug", 7, new Color(1f, 1f, 1f, 0.35f), TextAnchor.LowerLeft, Anchored(new Vector2(0f, 0f), new Vector2(90f, 8f)), new Vector2(170f, 10f));

        Set(hudController, "healthFill", hpFill);
        Set(hudController, "healthText", hpText);
        SetArray(hudController, "bladeIcons", bladeIcons.ToArray());
        Set(hudController, "recallFill", recallFill);
        Set(hudController, "recallText", recallText);
        Set(hudController, "waveText", waveText);
        Set(hudController, "timerText", timerText);
        Set(hudController, "enemiesText", enemiesText);
        Set(hudController, "coinsText", coinsText);
        Set(hudController, "bannerText", banner);
        Set(hudController, "hurtOverlay", hurt);

        // Main menu.
        RectTransform menu = Panel(root, "Main Menu", null, new Color(0.03f, 0.04f, 0.07f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Label(menu, "Title", "NIGHT SHIFT", 44, Accent, TextAnchor.MiddleCenter, Center(0f, 95f), new Vector2(500f, 56f)).fontStyle = FontStyle.Bold;
        Label(menu, "Subtitle", "Hold the warehouse until dawn.", 11, DimText, TextAnchor.MiddleCenter, Center(0f, 62f), new Vector2(400f, 16f));
        Button start = MakeButton(menu, "Start", "START SHIFT", Center(0f, 18f), new Vector2(140f, 24f));
        Button menuOptions = MakeButton(menu, "Options", "OPTIONS", Center(0f, -12f), new Vector2(140f, 24f));
        Button quit = MakeButton(menu, "Quit", "QUIT", Center(0f, -42f), new Vector2(140f, 24f));
        Label(menu, "Controls", "WASD move   Mouse aim   LMB throw   RMB fan throw   E / Space recall   Esc pause", 8, DimText, TextAnchor.MiddleCenter, Center(0f, -110f), new Vector2(600f, 14f));
        Label(menu, "Tip", "Blades stay where they land. Recall pulls them back through every enemy in the way.", 8, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, Center(0f, -126f), new Vector2(600f, 14f));

        // Options.
        RectTransform options = Panel(root, "Options", sprites["ui_panel"], Color.white, Mid(), Mid(), Vector2.zero, new Vector2(280f, 180f));
        Label(options, "Title", "OPTIONS", 18, Accent, TextAnchor.MiddleCenter, Center(0f, 66f), new Vector2(240f, 24f));
        Label(options, "Volume Label", "Master volume", 9, TextColor, TextAnchor.MiddleLeft, Center(-50f, 34f), new Vector2(140f, 14f));
        Text volumeValue = Label(options, "Volume Value", "100%", 9, GoldText, TextAnchor.MiddleRight, Center(100f, 34f), new Vector2(50f, 14f));
        Slider volume = MakeSlider(options, Center(0f, 18f), new Vector2(220f, 10f));
        Toggle feel = MakeToggle(options, "Game feel effects (F1)", Center(0f, -10f));
        Toggle debugToggle = MakeToggle(options, "Debug overlay (F2)", Center(0f, -32f));
        Button back = MakeButton(options, "Back", "BACK", Center(0f, -66f), new Vector2(110f, 22f));
        OptionsController optionsController = options.gameObject.AddComponent<OptionsController>();
        Set(optionsController, "volumeSlider", volume);
        Set(optionsController, "volumeValueText", volumeValue);
        Set(optionsController, "feelToggle", feel);
        Set(optionsController, "debugToggle", debugToggle);

        // Pause.
        RectTransform pause = Panel(root, "Pause", null, new Color(0.03f, 0.04f, 0.07f, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Label(pause, "Title", "PAUSED", 32, Accent, TextAnchor.MiddleCenter, Center(0f, 60f), new Vector2(300f, 40f));
        Button resume = MakeButton(pause, "Resume", "RESUME", Center(0f, 12f), new Vector2(140f, 24f));
        Button pauseOptions = MakeButton(pause, "Options", "OPTIONS", Center(0f, -18f), new Vector2(140f, 24f));
        Button pauseMenu = MakeButton(pause, "Main Menu", "MAIN MENU", Center(0f, -48f), new Vector2(140f, 24f));

        // Shop.
        RectTransform shop = Panel(root, "Shop", sprites["ui_panel"], Color.white, Mid(), Mid(), Vector2.zero, new Vector2(420f, 300f));
        Text shopTitle = Label(shop, "Title", "WAVE 1 CLEARED - SUPPLY ROOM", 14, Accent, TextAnchor.MiddleCenter, Center(0f, 130f), new Vector2(400f, 20f));
        Text shopHealth = Label(shop, "Health", "HP 100/100", 9, DangerText, TextAnchor.MiddleLeft, Center(-150f, 110f), new Vector2(110f, 14f));
        Img(shop, "Coin Icon", sprites["coin"], Color.white, Center(130f, 110f), new Vector2(10f, 10f));
        Text shopCoins = Label(shop, "Coins", "0", 11, GoldText, TextAnchor.MiddleLeft, Center(165f, 110f), new Vector2(60f, 14f));
        var items = new List<ShopItemView>
        {
            ShopRow(shop, 0, UpgradeType.ThrowRate, "Quick Hands", "+15% throw speed"),
            ShopRow(shop, 1, UpgradeType.Damage, "Honed Edges", "+10 damage per blade"),
            ShopRow(shop, 2, UpgradeType.MaxHP, "Kevlar Vest", "+25 max HP and full heal"),
            ShopRow(shop, 3, UpgradeType.MoveSpeed, "Night Sneakers", "+10% move speed"),
            ShopRow(shop, 4, UpgradeType.FanThrow, "Fan Throw", "Right click: throw 3 blades at once"),
            ShopRow(shop, 5, UpgradeType.Heal, "Med Kit", "Restore 50 HP"),
        };
        Button continueButton = MakeButton(shop, "Continue", "START WAVE 2", Center(0f, -128f), new Vector2(160f, 24f));
        ShopController shopController = shop.gameObject.AddComponent<ShopController>();
        SetArray(shopController, "items", items.ToArray());
        Set(shopController, "titleText", shopTitle);
        Set(shopController, "coinsText", shopCoins);
        Set(shopController, "healthText", shopHealth);
        Set(shopController, "continueButton", continueButton);

        // Results.
        RectTransform gameOver = Panel(root, "Game Over", null, new Color(0.08f, 0.02f, 0.04f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Label(gameOver, "Title", "LIGHTS OUT", 40, DangerText, TextAnchor.MiddleCenter, Center(0f, 70f), new Vector2(500f, 50f)).fontStyle = FontStyle.Bold;
        Text gameOverDetail = Label(gameOver, "Detail", "", 10, TextColor, TextAnchor.MiddleCenter, Center(0f, 25f), new Vector2(520f, 36f));
        Button gameOverRetry = MakeButton(gameOver, "Retry", "TRY AGAIN", Center(0f, -22f), new Vector2(140f, 24f));
        Button gameOverMenu = MakeButton(gameOver, "Main Menu", "MAIN MENU", Center(0f, -52f), new Vector2(140f, 24f));

        RectTransform victory = Panel(root, "Victory", null, new Color(0.1f, 0.07f, 0.03f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Label(victory, "Title", "DAWN", 48, GoldText, TextAnchor.MiddleCenter, Center(0f, 70f), new Vector2(500f, 56f)).fontStyle = FontStyle.Bold;
        Text victoryDetail = Label(victory, "Detail", "", 10, TextColor, TextAnchor.MiddleCenter, Center(0f, 25f), new Vector2(520f, 36f));
        Button victoryRetry = MakeButton(victory, "Play Again", "PLAY AGAIN", Center(0f, -22f), new Vector2(140f, 24f));
        Button victoryMenu = MakeButton(victory, "Main Menu", "MAIN MENU", Center(0f, -52f), new Vector2(140f, 24f));

        Set(ui, "hudPanel", hud.gameObject);
        Set(ui, "mainMenuPanel", menu.gameObject);
        Set(ui, "optionsPanel", options.gameObject);
        Set(ui, "pausePanel", pause.gameObject);
        Set(ui, "shopPanel", shop.gameObject);
        Set(ui, "gameOverPanel", gameOver.gameObject);
        Set(ui, "victoryPanel", victory.gameObject);
        Set(ui, "startButton", start);
        Set(ui, "menuOptionsButton", menuOptions);
        Set(ui, "quitButton", quit);
        Set(ui, "resumeButton", resume);
        Set(ui, "pauseOptionsButton", pauseOptions);
        Set(ui, "pauseMenuButton", pauseMenu);
        Set(ui, "optionsBackButton", back);
        Set(ui, "gameOverRetryButton", gameOverRetry);
        Set(ui, "gameOverMenuButton", gameOverMenu);
        Set(ui, "victoryRetryButton", victoryRetry);
        Set(ui, "victoryMenuButton", victoryMenu);
        Set(ui, "gameOverDetailText", gameOverDetail);
        Set(ui, "victoryDetailText", victoryDetail);

        foreach (RectTransform panel in new[] { hud, options, pause, shop, gameOver, victory })
            panel.gameObject.SetActive(false);
    }

    static ShopItemView ShopRow(RectTransform shop, int index, UpgradeType type, string title, string description)
    {
        float y = 82f - index * 33f;
        RectTransform row = Panel(shop, title, sprites["ui_button"], new Color(1f, 1f, 1f, 0.35f), Mid(), Mid(), new Vector2(0f, y), new Vector2(390f, 29f));
        Text name = Label(row, "Name", title, 10, TextColor, TextAnchor.MiddleLeft, Center(-95f, 5f), new Vector2(180f, 13f));
        name.fontStyle = FontStyle.Bold;
        Label(row, "Description", description, 7, DimText, TextAnchor.MiddleLeft, Center(-95f, -7f), new Vector2(180f, 11f));
        Text level = Label(row, "Level", "---", 10, Accent, TextAnchor.MiddleCenter, Center(60f, 0f), new Vector2(70f, 14f));
        Button buy = MakeButton(row, "Buy", "", Center(150f, 0f), new Vector2(70f, 21f));
        Img(buy.GetComponent<RectTransform>(), "Coin", sprites["coin"], Color.white, Center(-20f, 0f), new Vector2(8f, 8f));
        Text price = buy.GetComponentInChildren<Text>();
        price.color = GoldText;
        price.rectTransform.anchoredPosition = new Vector2(6f, 0f);

        ShopItemView view = row.gameObject.AddComponent<ShopItemView>();
        Set(view, "upgrade", (int)type);
        Set(view, "nameText", name);
        Set(view, "levelText", level);
        Set(view, "priceText", price);
        Set(view, "buyButton", buy);
        return view;
    }

    static Button MakeButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size)
    {
        Image image = Img(parent, name, sprites["ui_button"], Color.white, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.82f, 0.55f);
        colors.pressedColor = new Color(0.85f, 0.6f, 0.35f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f);
        button.colors = colors;
        Label(image.rectTransform, "Label", label, 10, TextColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    static DefaultControls.Resources ControlResources() => new DefaultControls.Resources
    {
        standard = sprites["ui_fill"],
        background = sprites["ui_panel"],
        knob = sprites["ui_knob"],
        checkmark = sprites["ui_check"],
    };

    static Slider MakeSlider(RectTransform parent, Vector2 position, Vector2 size)
    {
        GameObject go = DefaultControls.CreateSlider(ControlResources());
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Slider slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null) fill.color = Accent;
        return slider;
    }

    static Toggle MakeToggle(RectTransform parent, string label, Vector2 position)
    {
        GameObject go = DefaultControls.CreateToggle(ControlResources());
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(220f, 18f);
        Text text = go.GetComponentInChildren<Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 9;
        text.color = TextColor;
        return go.GetComponent<Toggle>();
    }

    static RectTransform Panel(RectTransform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        if (color.a > 0f)
        {
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
        }
        return rt;
    }

    static Image Img(RectTransform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
    {
        Image image = Img(parent, name, sprite, color, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        RectTransform rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = AnchorFor(position);
        rt.anchoredPosition = StripAnchor(position);
        rt.sizeDelta = size;
        return image;
    }

    static Image Img(RectTransform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border.sqrMagnitude > 0f)
            image.type = Image.Type.Sliced;
        return image;
    }

    static Text Label(RectTransform parent, string name, string content, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 box)
    {
        Text text = Label(parent, name, content, size, color, alignment, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = AnchorFor(position);
        rt.anchoredPosition = StripAnchor(position);
        rt.sizeDelta = box;
        return text;
    }

    static Text Label(RectTransform parent, string name, string content, int size, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    // Positions are encoded as anchor + offset so one helper handles corners and centers.
    struct AnchoredPos { public Vector2 anchor, offset; }
    static readonly Dictionary<Vector2, AnchoredPos> anchorLookup = new Dictionary<Vector2, AnchoredPos>();

    static Vector2 Anchored(Vector2 anchor, Vector2 offset)
    {
        // Encode the anchor in a far-away key so plain Center() positions stay readable.
        Vector2 key = new Vector2(offset.x + anchor.x * 100000f + 50000f, offset.y + anchor.y * 100000f + 50000f);
        anchorLookup[key] = new AnchoredPos { anchor = anchor, offset = offset };
        return key;
    }

    static Vector2 TopLeft(float x, float y) => Anchored(new Vector2(0f, 1f), new Vector2(x, y));
    static Vector2 Center(float x, float y) => new Vector2(x, y);
    static Vector2 Mid() => new Vector2(0.5f, 0.5f);

    static Vector2 AnchorFor(Vector2 position) => anchorLookup.TryGetValue(position, out AnchoredPos a) ? a.anchor : new Vector2(0.5f, 0.5f);
    static Vector2 StripAnchor(Vector2 position) => anchorLookup.TryGetValue(position, out AnchoredPos a) ? a.offset : position;

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
