using System.Collections;
using UnityEngine;

/*
    ScriptableObject 기반 패시브 아이템 템플릿.
    라이프사이클:
    1) 필드에 놓인 아이템 오브젝트의 SetItem이 이 에셋을 ItemManager.passive에 주입
    2) ItemManager가 장착 즉시 this.Activate(owner)를 StartCoroutine으로 실행
       (액티브와 달리 플레이어 입력 없음)
    3) duration 경과 or 타이머 만료 → OnDeactivate로 원복

    실제 아이템을 만들 때 이 클래스를 상속하되 ApplyEffect / RemoveEffect만 오버라이드.
    상태는 ItemManager 쪽에 두거나 owner 파라미터로 넘겨서 관리할 것.
*/
[CreateAssetMenu(menuName = "Item/Passive/Template")]
public class PassiveTemplate : ScriptableObject, IPassive
{
    [Header("아이템 설정")]
    [SerializeField] private float duration = 5f;

    public float AvailableTime => duration;

    // 효과 본체. ItemManager에서 StartCoroutine으로 실행한다.
    public virtual IEnumerator Activate(GameObject owner)
    {
        Debug.Log("패시브 활성화 시작");
        ApplyEffect(owner);
        yield return new WaitForSeconds(duration);
        OnDeactivate(owner);
        Debug.Log("패시브 활성화 종료");
    }

    // 중도 해제 / 자연 종료 공용 정리. ItemManager 타이머가 StopCoroutine 할 때도 호출됨.
    public virtual void OnDeactivate(GameObject owner)
    {
        RemoveEffect(owner);
    }

    protected virtual void ApplyEffect(GameObject owner)
    {
        // 효과는 여기서 바꿔주면 된다.
    }

    protected virtual void RemoveEffect(GameObject owner)
    {
        // ApplyEffect에서 바꾼 값 등을 원상복구
    }
}