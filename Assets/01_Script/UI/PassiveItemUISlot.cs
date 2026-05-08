using UnityEngine;
using UnityEngine.UI;

public class PassiveItemUISlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image timerFill;

    public void Initialize(Sprite sprite, bool useTimer)
    {
        if (icon != null)
            icon.sprite = sprite;

        if (timerFill != null)
        {
            timerFill.gameObject.SetActive(useTimer);
            timerFill.fillAmount = 1f;
        }
    }

    public void SetTimer(float normalized)
    {
        if (timerFill == null) return;

        timerFill.fillAmount = Mathf.Clamp01(normalized);
    }
}
