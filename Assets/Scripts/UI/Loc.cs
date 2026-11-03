using System;
using System.Collections.Generic;
using UnityEngine;

public enum Language { English, Chinese }

// Tiny two-language string table. English is the default; the choice is saved in PlayerPrefs.
public static class Loc
{
    private const string Pref = "NightShift.Language";
    private static bool loaded;
    private static Language current;
    private static Font latinFont;
    private static Font chineseFont;

    public static event Action Changed;

    public static Language Current
    {
        get
        {
            Load();
            return current;
        }
        set
        {
            Load();
            if (current == value) return;
            current = value;
            PlayerPrefs.SetInt(Pref, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    // Chinese glyphs come from the OS (Microsoft YaHei on Windows); Latin text keeps the default font.
    public static Font Font
    {
        get
        {
            if (Current == Language.Chinese)
                return ChineseFont;
            if (latinFont == null)
                latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return latinFont;
        }
    }

    public static Font ChineseFont
    {
        get
        {
            if (chineseFont == null)
                chineseFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial Unicode MS" }, 16);
            return chineseFont;
        }
    }

    public static string Get(string key)
    {
        if (!Table.TryGetValue(key, out string[] entry))
            return key;
        return Current == Language.Chinese ? entry[1] : entry[0];
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        current = (Language)Mathf.Clamp(PlayerPrefs.GetInt(Pref, 0), 0, 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }

    private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
    {
        ["menu.subtitle"] = new[] { "Hold the warehouse until dawn.", "守住仓库，撑到天亮。" },
        ["menu.start"] = new[] { "START SHIFT", "开始值班" },
        ["menu.howto"] = new[] { "HOW TO PLAY", "玩法说明" },
        ["menu.options"] = new[] { "OPTIONS", "设置" },
        ["menu.quit"] = new[] { "QUIT", "退出" },

        ["common.back"] = new[] { "BACK", "返回" },
        ["common.mainmenu"] = new[] { "MAIN MENU", "主菜单" },
        ["common.close"] = new[] { "CLOSE", "关闭" },
        ["common.prev"] = new[] { "< PREV", "< 上一页" },
        ["common.next"] = new[] { "NEXT >", "下一页 >" },

        ["options.title"] = new[] { "OPTIONS", "设置" },
        ["options.volume"] = new[] { "Master Volume", "主音量" },
        ["options.feel"] = new[] { "Game feel effects (F1)", "打击感特效 (F1)" },
        ["options.debug"] = new[] { "Debug overlay (F2)", "调试显示 (F2)" },
        ["options.tutorial"] = new[] { "Tutorial hints in wave 1", "第一波显示新手提示" },
        ["options.language"] = new[] { "Language", "语言" },

        ["pause.title"] = new[] { "PAUSED", "已暂停" },
        ["pause.resume"] = new[] { "RESUME", "继续" },

        ["howto.title"] = new[] { "HOW TO PLAY", "玩法说明" },
        ["howto.p1.title"] = new[] { "MOVE & AIM", "移动与瞄准" },
        ["howto.p1.body"] = new[] {
            "WASD to move. The mouse aims: the orange arrow shows where your blades will fly. Walk into crates to push them. Crates block enemy shots.",
            "WASD 移动，鼠标瞄准：橙色箭头就是飞刃飞出的方向。走过去可以推动木箱，木箱能挡住敌人的子弹。" },
        ["howto.p2.title"] = new[] { "THROW BLADES", "投掷飞刃" },
        ["howto.p2.body"] = new[] {
            "Hold LEFT CLICK to throw. You carry 5 blades. They pierce through enemies, then stay where they land or stick into walls and crates.",
            "按住鼠标左键投掷。你身上有 5 把飞刃，它们会穿透敌人，然后留在落地的位置，或者钉在墙上和箱子上。" },
        ["howto.p3.title"] = new[] { "RECALL", "飞刃回收" },
        ["howto.p3.body"] = new[] {
            "Press E or SPACE to pull every blade back. Returning blades hit everything on the way. 3 returning hits root an enemy for 1 second. Throw past a crowd, then recall through it!",
            "按 E 或空格召回所有飞刃。回程的飞刃会打中路上的一切，被 3 把回程飞刃命中的敌人会被定身 1 秒。先把刃扔到敌群后面，再收回来穿过它们！" },
        ["howto.p4.title"] = new[] { "ENEMIES & HAZARDS", "敌人与危险物" },
        ["howto.p4.body"] = new[] {
            "Rushers charge straight at you. Guard Bots shoot when they can see you, so break line of sight behind cover. Brutes wind up, then charge. Red barrels explode after 2 hits and set off nearby barrels.",
            "冲锋者会直接冲向你。安保机器人看得见你就会开火，躲到掩体后面切断视线。重装者会先蓄力再冲锋。红色油桶被击中 2 次就会爆炸，还会引爆旁边的油桶。" },
        ["howto.p5.title"] = new[] { "SURVIVE UNTIL DAWN", "撑到天亮" },
        ["howto.p5.body"] = new[] {
            "Clear 10 waves. Enemies drop coins; spend them in the supply room between waves. Low on health? More med kits drop. Clear a wave without getting hit for bonus coins.",
            "清除 10 波敌人。敌人会掉金币，在两波之间的补给室里花掉。血量低时会掉更多医疗包；一波不受伤，下一波的金币更多。" },

        ["hud.wave"] = new[] { "WAVE {0}", "第 {0} 波" },
        ["hud.recall.ready"] = new[] { "RECALL READY", "可以回收" },
        ["hud.recall.cd"] = new[] { "RECALL {0:0.0}s", "回收冷却 {0:0.0} 秒" },
        ["hud.banner.wave"] = new[] { "WAVE {0}", "第 {0} 波" },
        ["hud.banner.final"] = new[] { "FINAL WAVE", "最后一波" },
        ["hud.banner.cleared"] = new[] { "WAVE CLEARED", "本波已清除" },
        ["hud.banner.dawn"] = new[] { "DAWN!", "天亮了！" },
        ["hud.key.pause"] = new[] { "Pause", "暂停" },
        ["hud.key.fx"] = new[] { "FX", "特效" },
        ["hud.key.debug"] = new[] { "Debug", "调试" },

        ["shop.title"] = new[] { "WAVE {0} CLEARED  ·  SUPPLY ROOM", "第 {0} 波已清除  ·  补给室" },
        ["shop.next"] = new[] { "START WAVE {0}", "开始第 {0} 波" },
        ["shop.max"] = new[] { "MAX", "已满" },
        ["shop.hint"] = new[] { "Spend coins on upgrades before the next wave.", "下一波开始前，用金币购买升级。" },
        ["shop.hp"] = new[] { "{0}/{1}", "{0}/{1}" },
        ["up.ThrowRate.name"] = new[] { "Quick Hands", "快手" },
        ["up.ThrowRate.desc"] = new[] { "+15% throw speed", "投掷速度 +15%" },
        ["up.Damage.name"] = new[] { "Honed Edges", "磨利刀刃" },
        ["up.Damage.desc"] = new[] { "+10 damage per blade", "每把飞刃伤害 +10" },
        ["up.MaxHP.name"] = new[] { "Kevlar Vest", "防弹背心" },
        ["up.MaxHP.desc"] = new[] { "+25 max HP and full heal", "最大生命 +25 并回满" },
        ["up.MoveSpeed.name"] = new[] { "Night Sneakers", "夜行跑鞋" },
        ["up.MoveSpeed.desc"] = new[] { "+10% move speed", "移动速度 +10%" },
        ["up.FanThrow.name"] = new[] { "Fan Throw", "扇形投掷" },
        ["up.FanThrow.desc"] = new[] { "Right click: throw 3 blades at once", "右键：一次投出 3 把飞刃" },
        ["up.Heal.name"] = new[] { "Med Kit", "医疗包" },
        ["up.Heal.desc"] = new[] { "Restore 50 HP", "恢复 50 生命" },

        ["over.title"] = new[] { "LIGHTS OUT", "失守" },
        ["over.detail"] = new[] { "The intruders broke through on wave {0}.", "入侵者在第 {0} 波突破了防线。" },
        ["over.retry"] = new[] { "TRY AGAIN", "再试一次" },
        ["win.title"] = new[] { "DAWN", "天亮了" },
        ["win.detail"] = new[] { "The sun is up. The warehouse is safe.", "太阳升起，仓库安然无恙。" },
        ["win.again"] = new[] { "PLAY AGAIN", "再玩一局" },
        ["stat.wave"] = new[] { "WAVE", "波次" },
        ["stat.kills"] = new[] { "STOPPED", "击退" },
        ["stat.coins"] = new[] { "COINS", "金币" },
        ["stat.time"] = new[] { "TIME", "用时" },

        ["tut.throw"] = new[] { "Hold LEFT CLICK to throw blades", "按住鼠标左键投掷飞刃" },
        ["tut.recall"] = new[] { "Blades stay where they land. Press E or SPACE to recall", "飞刃会留在落地处。按 E 或空格收回" },
        ["tut.pierce"] = new[] { "Returning blades hit every enemy on the way back!", "回程的飞刃会打中沿途的每一个敌人！" },
        ["tut.barrel"] = new[] { "Hit red barrels twice to blow up crowds", "击中红色油桶两次，炸飞一群敌人" },
    };
}
