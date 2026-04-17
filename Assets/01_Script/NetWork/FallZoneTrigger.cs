using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class FallZoneTrigger : MonoBehaviour
{
    [Header("Fall Damage")]
    public float fallDamage = 50f; // 추락 시 깎일 체력

    // 추락 트리거와 접촉시 - 데미지/리스폰은 서버에서 별도처리
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어인지 확인 - 오브젝트 방지용 (ICharacterModel 구현체 전부 수용)
        ICharacterModel character = other.GetComponentInParent<ICharacterModel>();
        if(character == null) return;

        // 죽은 상태 무시
        if(character.IsDead) return;

        // 자신의 플레이어만 요청 (온라인 모드: isOwned, 오프라인: 무조건 본인)
        NetworkBehaviour nb = character as NetworkBehaviour;
        if(nb != null && NetworkClient.active && !nb.isOwned) return;

        Debug.Log($"[FallZone] {other.name} 추락 감지! 데미지: {fallDamage}"); // 디버그용

        character.RequestFallDamage(fallDamage);
    }
}
