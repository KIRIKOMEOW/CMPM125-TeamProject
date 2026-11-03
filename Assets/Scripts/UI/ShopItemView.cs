using UnityEngine;
using UnityEngine.UI;

// One shop row. Reads prices and levels from UpgradeSystem; buying goes through TryBuy only.
public class ShopItemView : MonoBehaviour
{
    [SerializeField] private UpgradeType upgrade;
    [SerializeField] private Image[] levelPips = new Image[0];
    [SerializeField] private Sprite pipOn;
    [SerializeField] private Sprite pipOff;
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

        for (int i = 0; i < levelPips.Length; i++)
        {
            if (levelPips[i] == null) continue;
            levelPips[i].enabled = i < max;
            levelPips[i].sprite = i < level ? pipOn : pipOff;
        }

        if (levelText != null)
            levelText.text = max < 0 && level > 0 ? $"x{level}" : "";

        if (priceText != null)
        {
            priceText.font = Loc.Font;
            priceText.text = price < 0 ? Loc.Get("shop.max") : price.ToString();
        }

        if (buyButton != null)
            buyButton.interactable = upgrades.CanBuy(upgrade);
    }
}
