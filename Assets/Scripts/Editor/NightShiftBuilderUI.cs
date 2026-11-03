using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI half of the builder: a "night-shift security console" look on a 640x360 reference canvas.
// Every label sits in a fixed box with best-fit text, so English and Chinese both stay inside frames.
public static partial class NightShiftBuilder
{
    static readonly Color TextColor = new Color32(230, 234, 245, 255);
    static readonly Color DimText = new Color32(150, 160, 190, 255);
    static readonly Color InkText = new Color32(20, 18, 32, 255);
    static readonly Color Accent = new Color32(255, 160, 60, 255);
    static readonly Color GoldText = new Color32(255, 214, 90, 255);
    static readonly Color DangerText = new Color32(255, 90, 90, 255);

    static readonly Vector2 TL = new Vector2(0f, 1f), TC = new Vector2(0.5f, 1f), TR = new Vector2(1f, 1f);
    static readonly Vector2 ML = new Vector2(0f, 0.5f), C = new Vector2(0.5f, 0.5f), MR = new Vector2(1f, 0.5f);
    static readonly Vector2 BL = new Vector2(0f, 0f), BC = new Vector2(0.5f, 0f), BR = new Vector2(1f, 0f);

    static void BuildUI()
    {
        var canvasObject = new GameObject("UI Canvas");
        var canvas = canvasObject.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        UIManager ui = canvasObject.AddComponent<UIManager>();

        RectTransform hud = BuildHud(root);
        RectTransform menu = BuildMainMenu(root, ui);
        RectTransform options = BuildOptions(root, ui);
        RectTransform howTo = BuildHowTo(root, ui);
        RectTransform pause = BuildPause(root, ui);
        RectTransform shop = BuildShop(root);
        RectTransform gameOver = BuildResults(root, ui, "Game Over", "over.title", DangerText, new Color(0.1f, 0.02f, 0.04f, 0.8f), "over.retry", true);
        RectTransform victory = BuildResults(root, ui, "Victory", "win.title", GoldText, new Color(0.12f, 0.08f, 0.02f, 0.8f), "win.again", false);

        Set(ui, "hudPanel", hud.gameObject);
        Set(ui, "mainMenuPanel", menu.gameObject);
        Set(ui, "optionsPanel", options.gameObject);
        Set(ui, "howToPanel", howTo.gameObject);
        Set(ui, "pausePanel", pause.gameObject);
        Set(ui, "shopPanel", shop.gameObject);
        Set(ui, "gameOverPanel", gameOver.gameObject);
        Set(ui, "victoryPanel", victory.gameObject);

        foreach (RectTransform panel in new[] { hud, options, howTo, pause, shop, gameOver, victory })
            panel.gameObject.SetActive(false);
    }

    // ---------- HUD ----------

    static RectTransform BuildHud(RectTransform root)
    {
        RectTransform hud = StretchNode(root, "HUD");
        HUDController hc = hud.gameObject.AddComponent<HUDController>();

        Image hurt = Fill(hud, "Hurt Overlay", sprites["ui_vignette"], new Color(1f, 1f, 1f, 0f), 0f);
        hurt.enabled = false;

        // Status plate: portrait, health, blades, recall.
        RectTransform plate = Pic(hud, "Status", TL, TL, new Vector2(6f, -6f), new Vector2(184f, 52f), sprites["ui_frame"]).rectTransform;
        RectTransform portrait = Pic(plate, "Portrait", TL, TL, new Vector2(5f, -5f), new Vector2(42f, 42f), sprites["ui_socket"]).rectTransform;
        Pic(portrait, "Kestrel", C, C, new Vector2(0f, 1f), new Vector2(32f, 32f), sprites["kestrel"]);
        Pic(plate, "Heart", TL, TL, new Vector2(52f, -8f), new Vector2(12f, 10f), sprites["ico_heart"]);
        RectTransform hpBar = Pic(plate, "HP Bar", TL, TL, new Vector2(67f, -6f), new Vector2(110f, 14f), sprites["ui_bar_bg"]).rectTransform;
        Image hpFill = Fill(hpBar, "Fill", sprites["ui_fill"], new Color32(220, 56, 70, 255), 2f);
        MakeFilled(hpFill);
        Text hpText = StretchText(hpBar, "Value", "100/100", 8, TextColor, TextAnchor.MiddleCenter, null);
        AddOutline(hpText);

        var bladeIcons = new List<Image>();
        for (int i = 0; i < 5; i++)
        {
            RectTransform slot = Pic(plate, "Blade Slot", TL, TL, new Vector2(52f + i * 15f, -25f), new Vector2(13f, 13f), sprites["ui_socket"]).rectTransform;
            bladeIcons.Add(Pic(slot, "Blade", C, C, Vector2.zero, new Vector2(8f, 8f), sprites["blade"]));
        }
        Pic(plate, "Recall Icon", TL, TL, new Vector2(130f, -25f), new Vector2(12f, 12f), sprites["ico_recall"]);
        RectTransform recallBar = Pic(plate, "Recall Bar", TL, TL, new Vector2(145f, -28f), new Vector2(32f, 7f), sprites["ui_bar_bg"]).rectTransform;
        Image recallFill = Fill(recallBar, "Fill", sprites["ui_fill"], Accent, 1f);
        MakeFilled(recallFill);
        Text recallText = Txt(plate, "Recall Text", TL, TL, new Vector2(52f, -40f), new Vector2(125f, 9f), "", 7, DimText, TextAnchor.MiddleLeft, null);

        // Night progress: moon, ten wave pips, sun.
        RectTransform night = Pic(hud, "Night", TC, TC, new Vector2(0f, -6f), new Vector2(212f, 26f), sprites["ui_frame"]).rectTransform;
        Pic(night, "Moon", ML, ML, new Vector2(5f, 0f), new Vector2(16f, 16f), sprites["ico_moon"]);
        Pic(night, "Sun", MR, MR, new Vector2(-5f, 0f), new Vector2(16f, 16f), sprites["ico_sun"]);
        var pips = new List<Image>();
        for (int i = 0; i < 10; i++)
            pips.Add(Pic(night, "Wave Pip", ML, ML, new Vector2(29f + i * 16f, 0f), new Vector2(10f, 10f), sprites["pip_todo"]));
        Text waveText = Txt(hud, "Wave", TC, TC, new Vector2(0f, -34f), new Vector2(160f, 14f), "WAVE 1", 11, Accent, TextAnchor.MiddleCenter, null);
        waveText.fontStyle = FontStyle.Bold;
        AddOutline(waveText);
        Text timerText = Txt(hud, "Timer", TC, TC, new Vector2(0f, -48f), new Vector2(80f, 10f), "0:00", 7, DimText, TextAnchor.MiddleCenter, null);

        // Counters.
        RectTransform coinPill = Pic(hud, "Coins", TR, TR, new Vector2(-6f, -6f), new Vector2(74f, 22f), sprites["ui_frame"]).rectTransform;
        Pic(coinPill, "Coin", ML, ML, new Vector2(8f, 0f), new Vector2(8f, 8f), sprites["coin"]);
        Text coinsText = Txt(coinPill, "Value", MR, MR, new Vector2(-8f, 0f), new Vector2(46f, 14f), "0", 11, GoldText, TextAnchor.MiddleRight, null);
        RectTransform enemyPill = Pic(hud, "Intruders", TR, TR, new Vector2(-6f, -31f), new Vector2(74f, 22f), sprites["ui_frame"]).rectTransform;
        Pic(enemyPill, "Intruder", ML, ML, new Vector2(6f, 0f), new Vector2(12f, 12f), sprites["ico_intruder"]);
        Text enemiesText = Txt(enemyPill, "Value", MR, MR, new Vector2(-8f, 0f), new Vector2(46f, 14f), "0", 11, TextColor, TextAnchor.MiddleRight, null);

        // Key hints.
        KeyHint(hud, 6f, 22f, "ESC", "hud.key.pause");
        KeyHint(hud, 76f, 16f, "F1", "hud.key.fx");
        KeyHint(hud, 136f, 16f, "F2", "hud.key.debug");

        // Wave banner with hazard stripes.
        RectTransform banner = Node(hud, "Banner", C, C, new Vector2(0f, 64f), new Vector2(640f, 30f));
        CanvasGroup bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        Fill(banner, "Strip", null, new Color(0f, 0f, 0f, 0.6f), 0f);
        Band(banner, "Hazard Top", true, 3f);
        Band(banner, "Hazard Bottom", false, 3f);
        Text bannerText = StretchText(banner, "Text", "WAVE 1", 18, Accent, TextAnchor.MiddleCenter, null);
        bannerText.fontStyle = FontStyle.Bold;
        AddOutline(bannerText);

        // Tutorial prompt.
        RectTransform tutorial = Pic(hud, "Tutorial", BC, BC, new Vector2(0f, 24f), new Vector2(340f, 26f), sprites["ui_frame"]).rectTransform;
        CanvasGroup tutorialGroup = tutorial.gameObject.AddComponent<CanvasGroup>();
        tutorialGroup.alpha = 0f;
        tutorialGroup.blocksRaycasts = false;
        RectTransform cap = Pic(tutorial, "Key", ML, ML, new Vector2(6f, 0f), new Vector2(28f, 15f), sprites["ui_keycap"]).rectTransform;
        Text capText = StretchText(cap, "Label", "LMB", 7, InkText, TextAnchor.MiddleCenter, null, 1f, 1f);
        capText.fontStyle = FontStyle.Bold;
        Text tutorialText = Txt(tutorial, "Message", MR, MR, new Vector2(-8f, 0f), new Vector2(294f, 20f), "", 9, TextColor, TextAnchor.MiddleLeft, null);
        TutorialHints hints = tutorial.gameObject.AddComponent<TutorialHints>();
        Set(hints, "group", tutorialGroup);
        Set(hints, "messageText", tutorialText);
        Set(hints, "keyText", capText);

        Set(hc, "healthFill", hpFill);
        Set(hc, "healthText", hpText);
        SetArray(hc, "bladeIcons", bladeIcons.ToArray());
        Set(hc, "recallFill", recallFill);
        Set(hc, "recallText", recallText);
        SetArray(hc, "wavePips", pips.ToArray());
        Set(hc, "pipDone", sprites["pip_done"]);
        Set(hc, "pipCurrent", sprites["pip_current"]);
        Set(hc, "pipTodo", sprites["pip_todo"]);
        Set(hc, "waveText", waveText);
        Set(hc, "timerText", timerText);
        Set(hc, "enemiesText", enemiesText);
        Set(hc, "coinsText", coinsText);
        Set(hc, "bannerGroup", bannerGroup);
        Set(hc, "bannerText", bannerText);
        Set(hc, "hurtOverlay", hurt);
        return hud;
    }

    static void KeyHint(RectTransform hud, float x, float width, string key, string labelKey)
    {
        RectTransform cap = Pic(hud, key + " Key", BL, BL, new Vector2(x, 6f), new Vector2(width, 13f), sprites["ui_keycap"]).rectTransform;
        StretchText(cap, "Label", key, 7, InkText, TextAnchor.MiddleCenter, null, 1f, 1f).fontStyle = FontStyle.Bold;
        Txt(hud, key + " Hint", BL, BL, new Vector2(x + width + 3f, 6f), new Vector2(40f, 13f), "", 7, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleLeft, labelKey);
    }

    // ---------- Main menu ----------

    static RectTransform BuildMainMenu(RectTransform root, UIManager ui)
    {
        RectTransform menu = StretchNode(root, "Main Menu");
        Fill(menu, "Backdrop", sprites["ui_gradient"], Color.white, 0f).raycastTarget = true;

        var rng = new System.Random(3);
        for (int i = 0; i < 46; i++)
        {
            float alpha = 0.25f + (float)rng.NextDouble() * 0.65f;
            Pic(menu, "Star", BL, C, new Vector2(rng.Next(8, 632), rng.Next(170, 352)), new Vector2(2f, 2f), sprites["pixel"], new Color(1f, 1f, 1f, alpha));
        }
        Pic(menu, "Moon Glow", C, C, new Vector2(196f, 100f), new Vector2(120f, 120f), sprites["ui_glow"], new Color(1f, 0.95f, 0.75f, 0.2f));
        Pic(menu, "Moon", C, C, new Vector2(196f, 100f), new Vector2(40f, 40f), sprites["ico_moon_big"]);

        // Showcase: Kestrel between two intruders, on a warm spotlight.
        Pic(menu, "Spotlight", C, C, new Vector2(150f, -60f), new Vector2(170f, 44f), sprites["ui_glow"], new Color(1f, 0.65f, 0.25f, 0.3f));
        Pic(menu, "Rusher", C, C, new Vector2(74f, -30f), new Vector2(64f, 64f), sprites["rusher"], new Color(0.5f, 0.55f, 0.7f, 1f));
        Pic(menu, "Guard Bot", C, C, new Vector2(228f, -30f), new Vector2(64f, 64f), sprites["guardbot"], new Color(0.5f, 0.55f, 0.7f, 1f));
        Pic(menu, "Kestrel", C, C, new Vector2(150f, -14f), new Vector2(96f, 96f), sprites["kestrel"]);

        Text title = Txt(menu, "Title", C, C, new Vector2(-110f, 92f), new Vector2(330f, 54f), "NIGHT SHIFT", 44, Accent, TextAnchor.MiddleCenter, null, false);
        title.fontStyle = FontStyle.Bold;
        AddOutline(title, 2f);
        var shadow = title.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.6f, 0.2f, 0.05f, 0.9f);
        shadow.effectDistance = new Vector2(3f, -3f);
        Image stripe = Pic(menu, "Stripe", C, C, new Vector2(-110f, 62f), new Vector2(240f, 5f), sprites["ui_hazard"]);
        stripe.type = Image.Type.Tiled;
        Txt(menu, "Subtitle", C, C, new Vector2(-110f, 48f), new Vector2(320f, 14f), "", 10, DimText, TextAnchor.MiddleCenter, "menu.subtitle");

        Button start = Btn(menu, "Start", C, C, new Vector2(-110f, 14f), new Vector2(170f, 26f), "menu.start", true, 12);
        Button howTo = Btn(menu, "How To Play", C, C, new Vector2(-110f, -18f), new Vector2(170f, 24f), "menu.howto", false);
        Button options = Btn(menu, "Options", C, C, new Vector2(-110f, -46f), new Vector2(170f, 24f), "menu.options", false);
        Button quit = Btn(menu, "Quit", C, C, new Vector2(-110f, -74f), new Vector2(170f, 24f), "menu.quit", false);

        Button language = Btn(menu, "Language", TR, TR, new Vector2(-8f, -8f), new Vector2(56f, 20f), null, false, 9);
        Text languageText = language.GetComponentInChildren<Text>();
        languageText.text = "中文";
        languageText.gameObject.AddComponent<LanguageNameLabel>();

        Txt(menu, "Footer", BC, BC, new Vector2(0f, 6f), new Vector2(400f, 10f), "CMPM 125  ·  Gameplay Project  ·  Fall 2026", 7, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, null);

        Set(ui, "startButton", start);
        Set(ui, "menuHowToButton", howTo);
        Set(ui, "menuOptionsButton", options);
        Set(ui, "quitButton", quit);
        Set(ui, "languageButton", language);
        Set(ui, "languageButtonText", languageText);
        return menu;
    }

    // ---------- Options ----------

    static RectTransform BuildOptions(RectTransform root, UIManager ui)
    {
        RectTransform win = Window(root, "Options", new Vector2(300f, 216f), "options.title", out RectTransform overlay);
        Txt(win, "Volume Label", TL, TL, new Vector2(16f, -34f), new Vector2(180f, 12f), "", 9, TextColor, TextAnchor.MiddleLeft, "options.volume");
        Text volumeValue = Txt(win, "Volume Value", TR, TR, new Vector2(-16f, -34f), new Vector2(60f, 12f), "100%", 9, GoldText, TextAnchor.MiddleRight, null);
        Slider volume = MakeSlider(win, TC, TC, new Vector2(0f, -50f), new Vector2(268f, 12f));
        Toggle feel = MakeToggle(win, "Feel Toggle", new Vector2(16f, -70f), "options.feel");
        Toggle debug = MakeToggle(win, "Debug Toggle", new Vector2(16f, -90f), "options.debug");
        Toggle tutorial = MakeToggle(win, "Tutorial Toggle", new Vector2(16f, -110f), "options.tutorial");
        Txt(win, "Language Label", TL, TL, new Vector2(16f, -132f), new Vector2(268f, 12f), "", 9, TextColor, TextAnchor.MiddleLeft, "options.language");
        Button english = Btn(win, "English", TC, TC, new Vector2(-58f, -148f), new Vector2(108f, 22f), null, false);
        english.GetComponentInChildren<Text>().text = "ENGLISH";
        Button chinese = Btn(win, "Chinese", TC, TC, new Vector2(58f, -148f), new Vector2(108f, 22f), null, false);
        Text chineseLabel = chinese.GetComponentInChildren<Text>();
        chineseLabel.text = "中文";
        chineseLabel.gameObject.AddComponent<LanguageNameLabel>();
        Button back = Btn(win, "Back", BC, BC, new Vector2(0f, 10f), new Vector2(120f, 22f), "common.back", true);

        OptionsController oc = win.gameObject.AddComponent<OptionsController>();
        Set(oc, "volumeSlider", volume);
        Set(oc, "volumeValueText", volumeValue);
        Set(oc, "feelToggle", feel);
        Set(oc, "debugToggle", debug);
        Set(oc, "tutorialToggle", tutorial);
        Set(oc, "englishButton", english);
        Set(oc, "chineseButton", chinese);
        Set(ui, "optionsBackButton", back);
        return overlay;
    }

    // ---------- How to play ----------

    static RectTransform BuildHowTo(RectTransform root, UIManager ui)
    {
        RectTransform win = Window(root, "How To Play", new Vector2(420f, 272f), "howto.title", out RectTransform overlay);
        Pic(win, "Stage", TC, TC, new Vector2(0f, -30f), new Vector2(392f, 96f), sprites["ui_socket"]);

        var pages = new List<GameObject>();
        for (int p = 1; p <= 5; p++)
        {
            RectTransform page = StretchNode(win, "Page " + p);
            Text pageTitle = Txt(page, "Title", TC, TC, new Vector2(0f, -132f), new Vector2(392f, 16f), "", 12, Accent, TextAnchor.MiddleCenter, $"howto.p{p}.title");
            pageTitle.fontStyle = FontStyle.Bold;
            Txt(page, "Body", TC, TC, new Vector2(0f, -150f), new Vector2(380f, 62f), "", 9, TextColor, TextAnchor.UpperCenter, $"howto.p{p}.body");
            Illustrate(page, p);
            page.gameObject.SetActive(p == 1);
            pages.Add(page.gameObject);
        }

        var dots = new List<Image>();
        for (int i = 0; i < 5; i++)
            dots.Add(Pic(win, "Dot", BC, C, new Vector2(-24f + i * 12f, 44f), new Vector2(6f, 6f), sprites["dot_off"]));

        Button prev = Btn(win, "Prev", BL, BL, new Vector2(12f, 10f), new Vector2(86f, 22f), "common.prev", false);
        Button next = Btn(win, "Next", BR, BR, new Vector2(-12f, 10f), new Vector2(86f, 22f), "common.next", false);
        Button close = Btn(win, "Close", BC, BC, new Vector2(0f, 10f), new Vector2(100f, 22f), "common.close", true);

        HowToPlayController hc = win.gameObject.AddComponent<HowToPlayController>();
        SetArray(hc, "pages", pages.ToArray());
        SetArray(hc, "dots", dots.ToArray());
        Set(hc, "dotOn", sprites["dot_on"]);
        Set(hc, "dotOff", sprites["dot_off"]);
        Set(hc, "prevButton", prev);
        Set(hc, "nextButton", next);
        Set(ui, "howToCloseButton", close);
        return overlay;
    }

    // Each page gets a small pixel-art diorama inside the stage box (centered 78 units below the top).
    static void Illustrate(RectTransform page, int p)
    {
        Color rooted = new Color(0.55f, 0.75f, 1f);
        switch (p)
        {
            case 1:
                ArtKey(page, "W", -112f, 10f, 16f);
                ArtKey(page, "A", -130f, -8f, 16f);
                ArtKey(page, "S", -112f, -8f, 16f);
                ArtKey(page, "D", -94f, -8f, 16f);
                Art(page, "kestrel", 0f, 0f, 32f);
                Art(page, "aim", 26f, -2f, 12f);
                Art(page, "crate", 64f, 0f, 32f);
                Art(page, "ico_mouse", 128f, 0f, 20f, 28f);
                break;
            case 2:
                Art(page, "kestrel", -130f, 0f, 32f);
                Art(page, "blade", -84f, 2f, 16f);
                Art(page, "blade", -48f, 2f, 16f);
                Art(page, "rusher", -8f, 0f, 32f);
                Art(page, "blade", 34f, 2f, 16f);
                Art(page, "crate", 92f, 0f, 32f);
                Art(page, "blade", 78f, 4f, 16f);
                Art(page, "ico_mouse", 148f, 0f, 20f, 28f);
                break;
            case 3:
                Art(page, "kestrel", -140f, 0f, 32f);
                Art(page, "ico_recall", -96f, 0f, 24f);
                Art(page, "rusher", -40f, 0f, 32f, color: rooted);
                Art(page, "blade", 4f, 10f, 16f);
                Art(page, "blade", 34f, -8f, 16f);
                Art(page, "blade", 64f, 8f, 16f);
                ArtKey(page, "E", 120f, 10f, 16f);
                ArtKey(page, "SPACE", 140f, -10f, 44f);
                break;
            case 4:
                Art(page, "rusher", -148f, 0f, 32f);
                Art(page, "guardbot", -96f, 0f, 32f);
                Art(page, "bolt", -66f, 2f, 12f);
                Art(page, "crate", -36f, 0f, 32f);
                Art(page, "brute", 30f, 0f, 48f);
                Art(page, "ui_glow", 120f, 0f, 70f, 70f, new Color(1f, 0.45f, 0.1f, 0.55f));
                Art(page, "barrel", 120f, 0f, 24f, 28f);
                Art(page, "barrel", 156f, -6f, 24f, 28f);
                break;
            case 5:
                Art(page, "ico_moon", -158f, 8f, 32f);
                Art(page, "ico_sun", 158f, 8f, 32f);
                for (int i = 0; i < 10; i++)
                    Art(page, i < 6 ? "pip_done" : i == 6 ? "pip_current" : "pip_todo", -117f + i * 26f, 8f, 8f);
                Art(page, "coin", -52f, -26f, 16f);
                Art(page, "coin", -32f, -26f, 16f);
                Art(page, "coin", -12f, -26f, 16f);
                Art(page, "medkit", 20f, -26f, 20f);
                Art(page, "kestrel", 60f, -26f, 32f);
                break;
        }
    }

    static void Art(RectTransform page, string key, float x, float y, float w, float h = -1f, Color? color = null)
    {
        Pic(page, key, TC, C, new Vector2(x, -78f + y), new Vector2(w, h < 0f ? w : h), sprites[key], color ?? Color.white);
    }

    static void ArtKey(RectTransform page, string label, float x, float y, float width)
    {
        RectTransform cap = Pic(page, "Key " + label, TC, C, new Vector2(x, -78f + y), new Vector2(width, 16f), sprites["ui_keycap"]).rectTransform;
        StretchText(cap, "Label", label, 8, InkText, TextAnchor.MiddleCenter, null, 1f, 1f).fontStyle = FontStyle.Bold;
    }

    // ---------- Pause ----------

    static RectTransform BuildPause(RectTransform root, UIManager ui)
    {
        RectTransform win = Window(root, "Pause", new Vector2(210f, 178f), "pause.title", out RectTransform overlay);
        Button resume = Btn(win, "Resume", TC, TC, new Vector2(0f, -34f), new Vector2(170f, 26f), "pause.resume", true, 12);
        Button howTo = Btn(win, "How To Play", TC, TC, new Vector2(0f, -66f), new Vector2(170f, 24f), "menu.howto", false);
        Button options = Btn(win, "Options", TC, TC, new Vector2(0f, -96f), new Vector2(170f, 24f), "menu.options", false);
        Button menu = Btn(win, "Main Menu", TC, TC, new Vector2(0f, -126f), new Vector2(170f, 24f), "common.mainmenu", false);
        Set(ui, "resumeButton", resume);
        Set(ui, "pauseHowToButton", howTo);
        Set(ui, "pauseOptionsButton", options);
        Set(ui, "pauseMenuButton", menu);
        return overlay;
    }

    // ---------- Shop ----------

    static RectTransform BuildShop(RectTransform root)
    {
        RectTransform win = Window(root, "Shop", new Vector2(472f, 308f), null, out RectTransform overlay);
        Text title = StretchText(win.Find("Header") as RectTransform, "Title", "", 11, InkText, TextAnchor.MiddleCenter, null, 4f, 2f);
        title.fontStyle = FontStyle.Bold;

        // Left column: Kestrel, health, coins.
        RectTransform portrait = Pic(win, "Portrait", TL, TL, new Vector2(14f, -32f), new Vector2(74f, 74f), sprites["ui_socket"]).rectTransform;
        Pic(portrait, "Kestrel", C, C, new Vector2(0f, 1f), new Vector2(64f, 64f), sprites["kestrel"]);
        Pic(win, "Heart", TL, TL, new Vector2(14f, -114f), new Vector2(12f, 10f), sprites["ico_heart"]);
        RectTransform hpBar = Pic(win, "HP Bar", TL, TL, new Vector2(30f, -112f), new Vector2(58f, 12f), sprites["ui_bar_bg"]).rectTransform;
        Image hpFill = Fill(hpBar, "Fill", sprites["ui_fill"], new Color32(220, 56, 70, 255), 2f);
        MakeFilled(hpFill);
        Text hpText = Txt(win, "HP Text", TL, TL, new Vector2(14f, -127f), new Vector2(74f, 11f), "", 8, TextColor, TextAnchor.MiddleCenter, null);
        RectTransform coinPill = Pic(win, "Coins", TL, TL, new Vector2(14f, -144f), new Vector2(74f, 22f), sprites["ui_socket"]).rectTransform;
        Pic(coinPill, "Coin", ML, ML, new Vector2(7f, 0f), new Vector2(8f, 8f), sprites["coin"]);
        Text coinsText = Txt(coinPill, "Value", MR, MR, new Vector2(-6f, 0f), new Vector2(48f, 14f), "0", 12, GoldText, TextAnchor.MiddleRight, null);
        Txt(win, "Hint", TL, TL, new Vector2(14f, -172f), new Vector2(74f, 70f), "", 7, DimText, TextAnchor.UpperLeft, "shop.hint");

        var items = new List<ShopItemView>();
        UpgradeType[] types = { UpgradeType.ThrowRate, UpgradeType.Damage, UpgradeType.MaxHP, UpgradeType.MoveSpeed, UpgradeType.FanThrow, UpgradeType.Heal };
        string[] icons = { "ico_throwrate", "ico_damage", "ico_vest", "ico_boot", "ico_fan", "medkit" };
        for (int i = 0; i < types.Length; i++)
            items.Add(ShopRow(win, i, types[i], icons[i]));

        Button next = Btn(win, "Continue", BC, BC, new Vector2(48f, 12f), new Vector2(210f, 26f), null, true, 12);

        ShopController sc = win.gameObject.AddComponent<ShopController>();
        SetArray(sc, "items", items.ToArray());
        Set(sc, "titleText", title);
        Set(sc, "coinsText", coinsText);
        Set(sc, "healthText", hpText);
        Set(sc, "healthFill", hpFill);
        Set(sc, "continueButton", next);
        Set(sc, "continueLabel", next.GetComponentInChildren<Text>());
        return overlay;
    }

    static ShopItemView ShopRow(RectTransform win, int index, UpgradeType type, string icon)
    {
        RectTransform row = Pic(win, type.ToString(), TL, TL, new Vector2(100f, -30f - index * 38f), new Vector2(358f, 34f), sprites["ui_socket"]).rectTransform;
        RectTransform iconBox = Pic(row, "Icon Box", ML, ML, new Vector2(4f, 0f), new Vector2(28f, 28f), sprites["ui_bar_bg"]).rectTransform;
        Sprite iconSprite = sprites[icon];
        Vector2 iconSize = new Vector2(iconSprite.rect.width * 2f, iconSprite.rect.height * 2f);
        Pic(iconBox, "Icon", C, C, Vector2.zero, iconSize, iconSprite);

        Text name = Txt(row, "Name", TL, TL, new Vector2(38f, -3f), new Vector2(170f, 14f), "", 10, TextColor, TextAnchor.MiddleLeft, $"up.{type}.name");
        name.fontStyle = FontStyle.Bold;
        Txt(row, "Description", TL, TL, new Vector2(38f, -18f), new Vector2(190f, 12f), "", 7, DimText, TextAnchor.MiddleLeft, $"up.{type}.desc");

        var pips = new List<Image>();
        for (int j = 0; j < 3; j++)
            pips.Add(Pic(row, "Level Pip", MR, MR, new Vector2(-104f + j * 10f, 0f), new Vector2(8f, 8f), sprites["pip_todo"]));
        Text level = Txt(row, "Level", MR, MR, new Vector2(-80f, 0f), new Vector2(34f, 12f), "", 9, Accent, TextAnchor.MiddleRight, null);

        Button buy = Btn(row, "Buy", MR, MR, new Vector2(-4f, 0f), new Vector2(70f, 24f), null, true, 11);
        Pic(buy.GetComponent<RectTransform>(), "Coin", ML, ML, new Vector2(9f, 1f), new Vector2(8f, 8f), sprites["coin"]);
        Text price = buy.GetComponentInChildren<Text>();
        price.rectTransform.offsetMin = new Vector2(22f, 2f);

        ShopItemView view = row.gameObject.AddComponent<ShopItemView>();
        Set(view, "upgrade", (int)type);
        SetArray(view, "levelPips", pips.ToArray());
        Set(view, "pipOn", sprites["pip_done"]);
        Set(view, "pipOff", sprites["pip_todo"]);
        Set(view, "levelText", level);
        Set(view, "priceText", price);
        Set(view, "buyButton", buy);
        return view;
    }

    // ---------- Results ----------

    static RectTransform BuildResults(RectTransform root, UIManager ui, string name, string titleKey, Color titleColor, Color tint, string retryKey, bool gameOver)
    {
        RectTransform overlay = StretchNode(root, name);
        Fill(overlay, "Dim", null, tint, 0f).raycastTarget = true;
        RectTransform win = Pic(overlay, "Window", C, C, Vector2.zero, new Vector2(384f, 204f), sprites["ui_frame"]).rectTransform;
        Image header = Pic(win, "Header", TC, TC, new Vector2(0f, -6f), new Vector2(372f, 10f), sprites[gameOver ? "ui_hazard" : "ui_header"]);
        if (gameOver) header.type = Image.Type.Tiled;

        Text title = Txt(win, "Title", TC, TC, new Vector2(0f, -22f), new Vector2(360f, 42f), "", 34, titleColor, TextAnchor.MiddleCenter, titleKey, false);
        title.fontStyle = FontStyle.Bold;
        AddOutline(title, 2f);
        Text detail = Txt(win, "Detail", TC, TC, new Vector2(0f, -68f), new Vector2(350f, 14f), "", 9, DimText, TextAnchor.MiddleCenter, null);

        string[] icons = { "ico_moon", "ico_intruder", "coin", "ico_sun" };
        string[] labels = { "stat.wave", "stat.kills", "stat.coins", "stat.time" };
        var stats = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransform box = Pic(win, "Stat", TC, TC, new Vector2(-129f + i * 86f, -90f), new Vector2(78f, 60f), sprites["ui_socket"]).rectTransform;
            Sprite iconSprite = sprites[icons[i]];
            float scale = iconSprite.rect.width <= 8f ? 2f : 1f;
            Pic(box, "Icon", TC, TC, new Vector2(0f, -6f), new Vector2(iconSprite.rect.width * scale, iconSprite.rect.height * scale), iconSprite);
            stats[i] = Txt(box, "Value", C, C, new Vector2(0f, -4f), new Vector2(74f, 16f), "0", 12, TextColor, TextAnchor.MiddleCenter, null);
            stats[i].fontStyle = FontStyle.Bold;
            Txt(box, "Label", BC, BC, new Vector2(0f, 4f), new Vector2(74f, 10f), "", 7, DimText, TextAnchor.MiddleCenter, labels[i]);
        }

        Button retry = Btn(win, "Retry", BC, BC, new Vector2(-70f, 16f), new Vector2(128f, 26f), retryKey, true, 11);
        Button menu = Btn(win, "Main Menu", BC, BC, new Vector2(70f, 16f), new Vector2(128f, 26f), "common.mainmenu", false, 11);

        if (gameOver)
        {
            Set(ui, "gameOverRetryButton", retry);
            Set(ui, "gameOverMenuButton", menu);
            Set(ui, "gameOverDetailText", detail);
            SetArray(ui, "gameOverStats", stats);
        }
        else
        {
            Set(ui, "victoryRetryButton", retry);
            Set(ui, "victoryMenuButton", menu);
            Set(ui, "victoryDetailText", detail);
            SetArray(ui, "victoryStats", stats);
        }
        return overlay;
    }

    // ---------- Widgets ----------

    // Dimmed full-screen overlay holding a framed window with an orange title bar.
    static RectTransform Window(RectTransform root, string name, Vector2 size, string titleKey, out RectTransform overlay)
    {
        overlay = StretchNode(root, name);
        Fill(overlay, "Dim", null, new Color(0.02f, 0.03f, 0.06f, 0.65f), 0f).raycastTarget = true;
        RectTransform win = Pic(overlay, "Window", C, C, Vector2.zero, size, sprites["ui_frame"]).rectTransform;
        RectTransform header = Pic(win, "Header", TC, TC, new Vector2(0f, -6f), new Vector2(size.x - 12f, 18f), sprites["ui_header"]).rectTransform;
        if (titleKey != null)
            StretchText(header, "Title", "", 11, InkText, TextAnchor.MiddleCenter, titleKey, 4f, 2f).fontStyle = FontStyle.Bold;
        return win;
    }

    static Button Btn(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string key, bool primary, int fontSize = 10)
    {
        Image image = Pic(parent, name, anchor, pivot, pos, size, sprites[primary ? "btn_primary" : "btn_secondary"]);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = sprites[primary ? "btn_primary_hover" : "btn_secondary_hover"],
            pressedSprite = sprites[primary ? "btn_primary_pressed" : "btn_secondary_pressed"],
            selectedSprite = sprites[primary ? "btn_primary" : "btn_secondary"],
            disabledSprite = sprites["btn_disabled"],
        };
        // The face sits above a 2px lip, so the label is nudged up to stay centered on it.
        Text label = StretchText(image.rectTransform, "Label", "", fontSize, primary ? InkText : TextColor, TextAnchor.MiddleCenter, key, 5f, 2f);
        label.rectTransform.offsetMin = new Vector2(5f, 3f);
        label.rectTransform.offsetMax = new Vector2(-5f, -1f);
        label.fontStyle = FontStyle.Bold;
        return button;
    }

    static Toggle MakeToggle(RectTransform parent, string name, Vector2 pos, string key)
    {
        RectTransform node = Node(parent, name, TL, TL, pos, new Vector2(268f, 16f));
        Image box = Pic(node, "Box", ML, ML, Vector2.zero, new Vector2(14f, 14f), sprites["ui_socket"]);
        box.raycastTarget = true;
        Image check = Pic(box.rectTransform, "Check", C, C, Vector2.zero, new Vector2(8f, 8f), sprites["ui_check"]);
        Txt(node, "Label", ML, ML, new Vector2(20f, 0f), new Vector2(248f, 14f), "", 9, TextColor, TextAnchor.MiddleLeft, key);
        Toggle toggle = node.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.isOn = true;
        return toggle;
    }

    static Slider MakeSlider(RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform node = Node(parent, "Volume Slider", anchor, pivot, pos, size);
        Image back = Fill(node, "Background", sprites["ui_bar_bg"], Color.white, 0f);
        back.raycastTarget = true;
        RectTransform fillArea = StretchNode(node, "Fill Area");
        fillArea.offsetMin = new Vector2(2f, 2f);
        fillArea.offsetMax = new Vector2(-2f, -2f);
        RectTransform fill = Node(fillArea, "Fill", BL, BL, Vector2.zero, Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = sprites["ui_fill"];
        fillImage.color = Accent;
        fillImage.raycastTarget = false;
        RectTransform handleArea = StretchNode(node, "Handle Area");
        handleArea.offsetMin = new Vector2(5f, 0f);
        handleArea.offsetMax = new Vector2(-5f, 0f);
        Image handle = Pic(handleArea, "Handle", ML, C, Vector2.zero, new Vector2(10f, 14f), sprites["ui_knob"]);
        handle.raycastTarget = true;

        Slider slider = node.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    static void Band(RectTransform parent, string name, bool top, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
        Image image = go.AddComponent<Image>();
        image.sprite = sprites["ui_hazard"];
        image.type = Image.Type.Tiled;
        image.raycastTarget = false;
    }

    static void MakeFilled(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.fillAmount = 1f;
    }

    static void AddOutline(Text text, float distance = 1f)
    {
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.07f, 0.12f, 1f);
        outline.effectDistance = new Vector2(distance, -distance);
    }

    // ---------- Layout primitives ----------

    static RectTransform Node(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform StretchNode(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image Pic(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite sprite, Color? color = null)
    {
        RectTransform rt = Node(parent, name, anchor, pivot, pos, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color ?? Color.white;
        image.raycastTarget = false;
        if (sprite != null && sprite.border.sqrMagnitude > 0f)
            image.type = Image.Type.Sliced;
        return image;
    }

    static Image Fill(RectTransform parent, string name, Sprite sprite, Color color, float inset)
    {
        RectTransform rt = StretchNode(parent, name);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border.sqrMagnitude > 0f)
            image.type = Image.Type.Sliced;
        return image;
    }

    static Text Txt(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string content, int fontSize, Color color, TextAnchor align, string key, bool fit = true)
    {
        RectTransform rt = Node(parent, name, anchor, pivot, pos, size);
        return SetupText(rt, content, fontSize, color, align, key, fit);
    }

    static Text StretchText(RectTransform parent, string name, string content, int fontSize, Color color, TextAnchor align, string key, float insetX = 0f, float insetY = 0f)
    {
        RectTransform rt = StretchNode(parent, name);
        rt.offsetMin = new Vector2(insetX, insetY);
        rt.offsetMax = new Vector2(-insetX, -insetY);
        return SetupText(rt, content, fontSize, color, align, key, true);
    }

    static Text SetupText(RectTransform rt, string content, int fontSize, Color color, TextAnchor align, string key, bool fit)
    {
        Text text = rt.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = key != null ? Loc.Get(key) : content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        if (fit)
        {
            // Wrap inside the box and shrink when a translation runs long; never spill past the frame.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(5, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
        }
        else
        {
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
        if (key != null)
            Set(rt.gameObject.AddComponent<LocalizedText>(), "key", key);
        return text;
    }
}
