using UnityEngine;
using UnityEngine.UI;

// Between-wave shop panel: rebuilds every row whenever coins, upgrades, health or language change.
public class ShopController : MonoBehaviour
{
    [SerializeField] private ShopItemView[] items = new ShopItemView[0];
    [SerializeField] private Text titleText;
    [SerializeField] private Text coinsText;
    [SerializeField] private Text healthText;
    [SerializeField] private Image healthFill;
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueLabel;

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
        Loc.Changed += RefreshAll;
        RefreshAll();
    }

    private void OnDisable()
    {
        GameEvents.CoinsChanged -= OnCoinsChanged;
        GameEvents.UpgradePurchased -= OnUpgradePurchased;
        GameEvents.PlayerHealthChanged -= OnHealthChanged;
        Loc.Changed -= RefreshAll;
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
            {
                titleText.font = Loc.Font;
                titleText.text = Loc.Format("shop.title", manager.CurrentWave);
            }
            if (coinsText != null)
                coinsText.text = manager.Coins.ToString();
            if (continueLabel != null)
            {
                continueLabel.font = Loc.Font;
                continueLabel.text = Loc.Format("shop.next", manager.CurrentWave + 1);
            }

            PlayerController player = manager.Player;
            if (player != null && player.Health != null)
            {
                if (healthText != null)
                    healthText.text = Loc.Format("shop.hp", player.Health.CurrentHP, player.Health.MaxHP);
                if (healthFill != null)
                    healthFill.fillAmount = (float)player.Health.CurrentHP / Mathf.Max(1, player.Health.MaxHP);
            }
        }

        foreach (ShopItemView item in items)
            if (item != null)
                item.Refresh();
    }
}
