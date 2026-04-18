using UnityEngine;

/// <summary>
/// 무기 프리팹에서 "손이 쥐는 점"을 표시하는 마커.
/// 무기 프리팹의 적절한 자식 Transform(손잡이 중심, 자세에 맞는 회전 포함)에 붙인다.
///
/// <see cref="UnifiedWeaponEquipHandler"/>가 장착 시 이 Transform이
/// 캐릭터 손 본의 원점과 정확히 겹치도록 무기 루트를 역변환하여 부착한다.
///
/// 즉, 아티스트가 프리팹에서 GripPoint를 손잡이에 딱 맞게 배치해 두면
/// 캐릭터별/무기별 오프셋 튜닝이 필요 없게 된다.
/// </summary>
public class WeaponGripPoint : MonoBehaviour
{
    // 내용 없음 — 마커 용도.
    // 필요하면 추후 "어느 손으로 쥐는지" 같은 속성 확장 가능.
}
