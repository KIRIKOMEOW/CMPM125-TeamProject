using UnityEngine;
using UnityEngine.UI;

// Language switch labels ("中文" / "EN") always need CJK glyphs, whatever language is active.
[RequireComponent(typeof(Text))]
public class LanguageNameLabel : MonoBehaviour
{
    private void OnEnable()
    {
        GetComponent<Text>().font = Loc.ChineseFont;
    }
}
