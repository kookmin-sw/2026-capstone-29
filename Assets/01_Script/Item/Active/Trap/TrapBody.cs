using UnityEngine;

/// <summary>
/// 덫의 자식 오브젝트에 부착되는 트리거 감지기.
/// IsTrigger=true 인 콜라이더와 함께 자식 오브젝트에 부착한다.
/// 부모의 UnifiedTrap_Object에게 감지 이벤트를 전달한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrapBody : MonoBehaviour
{
    private UnifiedTrap_Object parentTrap;

    private void Awake()
    {
        parentTrap = GetComponentInParent<UnifiedTrap_Object>();
        if (parentTrap == null)
        {
            Debug.LogError($"[UnifiedTrap_TriggerSensor] 부모에 UnifiedTrap_Object가 없음: {name}");
        }

        // 콜라이더가 trigger인지 강제 확인
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[UnifiedTrap_TriggerSensor] {name}의 Collider가 IsTrigger=false. 강제로 true로 설정.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentTrap == null) return;
        parentTrap.OnSensorTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (parentTrap == null) return;
        parentTrap.OnSensorTriggerStay(other);
    }
}