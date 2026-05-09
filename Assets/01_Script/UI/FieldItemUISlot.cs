using UnityEngine;
using UnityEngine.UI;

public class FieldItemUISlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image timerFill;

    public void Initialize(Sprite sprite)
    {
        if (icon != null) icon.sprite = sprite;
        if (timerFill != null) timerFill.fillAmount = 1f;
    }

    public void SetTimer(float normalized)
    {
        if (timerFill != null)
            timerFill.fillAmount = Mathf.Clamp01(normalized);
    }
}
