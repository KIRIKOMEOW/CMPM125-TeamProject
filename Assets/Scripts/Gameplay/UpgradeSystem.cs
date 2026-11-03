using System;
using UnityEngine;

[DisallowMultipleComponent]
public class UpgradeSystem : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerController player;

    [Header("Prices")]
    [SerializeField] private int[] throwRatePrices = { 20, 35, 55 };
    [SerializeField] private int[] damagePrices = { 25, 40, 60 };
    [SerializeField] private int[] maxHPPrices = { 20, 35, 55 };
    [SerializeField] private int[] moveSpeedPrices = { 15, 30 };
    [SerializeField] private int fanThrowPrice = 80;
    [SerializeField] private int healPrice = 15;

    [Header("Healing")]
    [SerializeField] private int healAmount = 50;

    private readonly int[] levels = new int[6];
    private bool purchasing;

    public static UpgradeSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == this)
            GameEvents.RunReset += ResetUpgrades;
    }

    private void OnDisable()
    {
        GameEvents.RunReset -= ResetUpgrades;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int GetLevel(UpgradeType t)
    {
        int index = (int)t;
        return index >= 0 && index < levels.Length ? levels[index] : 0;
    }

    public int GetMaxLevel(UpgradeType t)
    {
        switch (t)
        {
            case UpgradeType.ThrowRate:
            case UpgradeType.Damage:
            case UpgradeType.MaxHP:
                return 3;
            case UpgradeType.MoveSpeed:
                return 2;
            case UpgradeType.FanThrow:
                return 1;
            case UpgradeType.Heal:
                return -1;
            default:
                return 0;
        }
    }

    public int GetPrice(UpgradeType t)
    {
        int level = GetLevel(t);
        int max = GetMaxLevel(t);
        if (max >= 0 && level >= max)
            return -1;

        switch (t)
        {
            case UpgradeType.ThrowRate: return PriceAt(throwRatePrices, level);
            case UpgradeType.Damage: return PriceAt(damagePrices, level);
            case UpgradeType.MaxHP: return PriceAt(maxHPPrices, level);
            case UpgradeType.MoveSpeed: return PriceAt(moveSpeedPrices, level);
            case UpgradeType.FanThrow: return Mathf.Max(0, fanThrowPrice);
            case UpgradeType.Heal: return Mathf.Max(0, healPrice);
            default: return -1;
        }
    }

    public bool CanBuy(UpgradeType t)
    {
        GameManager manager = GameManager.Instance;
        PlayerController currentPlayer = ResolvePlayer();
        int price = GetPrice(t);
        if (!isActiveAndEnabled || Instance != this || manager == null || manager.State != GameState.Shop
            || currentPlayer == null || currentPlayer.Health == null || currentPlayer.Health.IsDead
            || price < 0 || manager.Coins < price)
            return false;

        if (t == UpgradeType.FanThrow && !currentPlayer.FanThrowAvailable)
            return false;
        return t != UpgradeType.Heal || currentPlayer.Health.CurrentHP < currentPlayer.Health.MaxHP;
    }

    public bool TryBuy(UpgradeType t)
    {
        if (purchasing || !CanBuy(t))
            return false;

        purchasing = true;
        try
        {
            GameManager manager = GameManager.Instance;
            PlayerController currentPlayer = ResolvePlayer();
            if (!manager.TrySpendCoins(GetPrice(t)))
                return false;

            int index = (int)t;
            if (levels[index] < int.MaxValue)
                levels[index]++;
            if (t == UpgradeType.Heal)
                currentPlayer.Health.Heal(healAmount);
            else
                ApplyToPlayer(currentPlayer);

            GameEvents.RaiseUpgradePurchased(t, levels[index]);
            return true;
        }
        finally
        {
            purchasing = false;
        }
    }

    private PlayerController ResolvePlayer()
    {
        if (player == null && GameManager.Instance != null)
            player = GameManager.Instance.Player;
        return player;
    }

    private void ResetUpgrades()
    {
        Array.Clear(levels, 0, levels.Length);
        PlayerController currentPlayer = ResolvePlayer();
        if (currentPlayer != null)
            ApplyToPlayer(currentPlayer);
    }

    private void ApplyToPlayer(PlayerController currentPlayer)
    {
        currentPlayer.ApplyUpgrades(GetLevel(UpgradeType.ThrowRate), GetLevel(UpgradeType.Damage),
            GetLevel(UpgradeType.MaxHP), GetLevel(UpgradeType.MoveSpeed), GetLevel(UpgradeType.FanThrow) > 0);
    }

    private static int PriceAt(int[] prices, int level)
    {
        return prices != null && level >= 0 && level < prices.Length ? Mathf.Max(0, prices[level]) : -1;
    }

    private void OnValidate()
    {
        fanThrowPrice = Mathf.Max(0, fanThrowPrice);
        healPrice = Mathf.Max(0, healPrice);
        healAmount = Mathf.Max(1, healAmount);
    }
}
