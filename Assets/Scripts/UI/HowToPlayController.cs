using UnityEngine;
using UnityEngine.UI;

// Paged illustrated tutorial opened from the main menu or the pause menu.
public class HowToPlayController : MonoBehaviour
{
    [SerializeField] private GameObject[] pages = new GameObject[0];
    [SerializeField] private Image[] dots = new Image[0];
    [SerializeField] private Sprite dotOn;
    [SerializeField] private Sprite dotOff;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    private int index;

    private void Awake()
    {
        if (prevButton != null) prevButton.onClick.AddListener(() => Show(index - 1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Show(index + 1));
    }

    private void OnEnable() => Show(0);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Show(index + 1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Show(index - 1);
    }

    private void Show(int page)
    {
        if (pages.Length == 0) return;
        int next = Mathf.Clamp(page, 0, pages.Length - 1);
        if (next != index && isActiveAndEnabled)
            AudioManager.Instance?.PlaySfx(SfxId.UIClick, 0f);
        index = next;
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
        for (int i = 0; i < dots.Length; i++)
            if (dots[i] != null) dots[i].sprite = i == index ? dotOn : dotOff;
        if (prevButton != null) prevButton.interactable = index > 0;
        if (nextButton != null) nextButton.interactable = index < pages.Length - 1;
    }
}
