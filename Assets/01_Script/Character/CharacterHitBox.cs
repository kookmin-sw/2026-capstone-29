using UnityEngine;
using Mirror;

public class CharacterHitBox : MonoBehaviour
{
    public float damage = 10f; // 주먹 한 방의 대미지
    public Collider hitboxCollider; // 주먹에 달린 콜라이더

    private void Awake()
    {
        // 평소에는 주먹 콜라이더를 꺼둡니다 (닿아도 안 맞게)
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }

    // 트리거(주먹)가 무언가에 닿았을 때 실행됨
    private void OnTriggerEnter(Collider other)
    {
        // 1. 내가 때린 대상이 NetworkCharacterModel을 가지고 있는지 확인
        NetworkCharacterModel target = other.GetComponent<NetworkCharacterModel>();

        // 2. 대상이 존재하고, 내 자신을 때린 게 아니라면
        if (target != null && target.gameObject != this.transform.root.gameObject)
        {
            Debug.Log($"적 적중! 대미지 {damage}를 줍니다.");
            
            // 3. 대상의 서버(Cmd)로 대미지를 깎으라고 명령!
            target.CmdTakeDamage(damage);

            // 한 번 때렸으면 다단히트(여러 번 맞는 것) 방지를 위해 콜라이더를 즉시 끔
            hitboxCollider.enabled = false;
        }
    }

    // 공격 애니메이션이 재생될 때 켜주는 함수 (nPlayerCombat 등에서 호출)
    public void EnableHitbox()
    {
        hitboxCollider.enabled = true;
    }

    // 공격 애니메이션이 끝날 때 꺼주는 함수
    public void DisableHitbox()
    {
        hitboxCollider.enabled = false;
    }
}