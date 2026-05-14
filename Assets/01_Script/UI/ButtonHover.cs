using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image hoverImage; // 위에 겹쳐진 이미지
    public float duration = 0.2f;

    void Start()
    {
        // 시작할 때 투명도를 0으로 설정
        hoverImage.CrossFadeAlpha(0f, 0f, true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스를 올리면 duration 동안 알파를 1로
        hoverImage.CrossFadeAlpha(1f, duration, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 마우스를 치우면 duration 동안 알파를 0으로
        hoverImage.CrossFadeAlpha(0f, duration, false);
    }
}