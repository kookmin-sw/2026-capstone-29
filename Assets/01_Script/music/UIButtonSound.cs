using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        UISoundManager.Instance?.PlayHoverSound();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        UISoundManager.Instance?.PlayClickSound();
    }
}
