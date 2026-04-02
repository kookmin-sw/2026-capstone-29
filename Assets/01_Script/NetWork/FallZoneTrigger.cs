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
        // 플레이어인지 확인 - 오브젝트 방지용
        NetworkCharacterModel character = other.GetComponentInParent<NetworkCharacterModel>();
        if(character == null) return;

        // 죽은 상태 무시
        if(character.IsDead) return;

        // 자신의 플레이어만 요청
        if(!character.isOwned) return;

        Debug.Log($"[FallZone] {other.name} 추락 감지! 데미지: {fallDamage}"); // 디버그용

        character.CmdFallDamage(fallDamage);
    }
}
