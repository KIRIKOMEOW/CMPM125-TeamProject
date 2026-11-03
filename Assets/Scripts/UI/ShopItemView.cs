using UnityEngine;
using UnityEngine.UI;

// One shop row. Reads prices and levels from UpgradeSystem; buying goes through TryBuy only.
public class ShopItemView : MonoBehaviour
{
    [SerializeField] private UpgradeType upgrade;
    [SerializeField] private Text nameText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text priceText;
    [SerializeField] private Button buyButton;

    public UpgradeType Upgrade => upgrade;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(Buy);
    }

    private void Buy()
    {
        UpgradeSystem upgrades = UpgradeSystem.Instance;
        if (upgrades == null)
            return;

        if (!upgrades.TryBuy(upgrade))
            AudioManager.Instance?.PlaySfx(SfxId.UIClick, 0f);
        // UpgradePurchased / CoinsChanged make ShopController refresh every row.
    }

    public void Refresh()
    {
        UpgradeSystem upgrades = UpgradeSystem.Instance;
        if (upgrades == null)
            return;

        int level = upgrades.GetLevel(upgrade);
        int max = upgrades.GetMaxLevel(upgrade);
        int price = upgrades.GetPrice(upgrade);

        if (levelText != null)
        {
            if (max < 0)
                levelText.text = level > 0 ? $"bought x{level}" : "";
            else
                levelText.text = new string('#', level) + new string('-', Mathf.Max(0, max - level));
        }

        if (priceText != null)
            priceText.text = price < 0 ? "MAX" : price.ToString();

        if (buyButton != null)
            buyButton.interactable = upgrades.CanBuy(upgrade);
    }
}
