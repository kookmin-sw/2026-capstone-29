using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonMove : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("마우스를 올렸을 때 이동할 X축")]
    public float moveAmountX = 20f; 
    
    [Tooltip("이동하는 속도를 조절")]
    public float moveSpeed = 10f;

    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector2 targetPosition; // 버튼이 향할 목표 위치

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        
        // 처음 시작할 때의 목표 위치는 원래 위치로 설정합니다.
        targetPosition = originalPosition; 
    }

    void Update()
    {
        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPosition, Time.deltaTime * moveSpeed);
    }

    // 마우스를 올리면 해당 위치로 변경
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetPosition = new Vector2(originalPosition.x + moveAmountX, originalPosition.y);
    }

    // 마우스를 벗어나면 원래 위치로 변경
    public void OnPointerExit(PointerEventData eventData)
    {
        targetPosition = originalPosition;
    }
}