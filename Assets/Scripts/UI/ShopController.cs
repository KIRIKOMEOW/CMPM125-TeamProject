using UnityEngine;
using UnityEngine.UI;

// Between-wave shop panel: rebuilds every row whenever coins, upgrades or health change.
public class ShopController : MonoBehaviour
{
    [SerializeField] private ShopItemView[] items = new ShopItemView[0];
    [SerializeField] private Text titleText;
    [SerializeField] private Text coinsText;
    [SerializeField] private Text healthText;
    [SerializeField] private Button continueButton;

    private void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySfx(SfxId.UIClick, 0f);
                GameManager.Instance?.CloseShopAndStartNextWave();
            });
        }
    }

    private void OnEnable()
    {
        GameEvents.CoinsChanged += OnCoinsChanged;
        GameEvents.UpgradePurchased += OnUpgradePurchased;
        GameEvents.PlayerHealthChanged += OnHealthChanged;
        RefreshAll();
    }

    private void OnDisable()
    {
        GameEvents.CoinsChanged -= OnCoinsChanged;
        GameEvents.UpgradePurchased -= OnUpgradePurchased;
        GameEvents.PlayerHealthChanged -= OnHealthChanged;
    }

    private void OnCoinsChanged(int total) => RefreshAll();
    private void OnUpgradePurchased(UpgradeType type, int level) => RefreshAll();
    private void OnHealthChanged(int current, int max) => RefreshAll();

    private void RefreshAll()
    {
        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            if (titleText != null)
                titleText.text = $"WAVE {manager.CurrentWave} CLEARED - SUPPLY ROOM";
            if (coinsText != null)
                coinsText.text = manager.Coins.ToString();
            if (continueButton != null)
            {
                Text label = continueButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"START WAVE {manager.CurrentWave + 1}";
            }

            PlayerController player = manager.Player;
            if (healthText != null && player != null && player.Health != null)
                healthText.text = $"HP {player.Health.CurrentHP}/{player.Health.MaxHP}";
        }

        foreach (ShopItemView item in items)
            if (item != null)
                item.Refresh();
    }
}
