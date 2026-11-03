using UnityEngine;
using UnityEngine.UI;

// Static UI label that follows the selected language.
[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;

    private Text text;

    private void Awake()
    {
        text = GetComponent<Text>();
    }

    private void OnEnable()
    {
        Loc.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        Loc.Changed -= Apply;
    }

    private void Apply()
    {
        if (text == null) return;
        text.font = Loc.Font;
        text.text = Loc.Get(key);
    }
}
