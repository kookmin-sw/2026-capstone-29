using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class CharacterHitBox : MonoBehaviour
{
    public float damage = 10f; // 주먹 한 방의 대미지
    public Collider hitboxCollider; // 주먹에 달린 콜라이더

    [Header("Hit Effect")]
    public int effectIndex = 0;
    public GameObject hitEffectPrefab;  // VFX_ImpactClassic01 프리팹들을 여기에 드래그
    public float effectDuration = 2f;

    [Header("State Permission")]
    // 이 히트박스가 활성화될 수 있는 스테이트 이름 목록
    public List<string> allowedStates = new List<string>();

    private Animator _anim;
    private HashSet<GameObject> _hitTargets = new HashSet<GameObject>();

    private void Awake()
    {
        // 평소에는 주먹 콜라이더를 꺼둡니다 (닿아도 안 맞게)
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;

        _anim = this.transform.root.GetComponentInChildren<Animator>();
    }

    // 트리거(주먹)가 무언가에 닿았을 때 실행됨
    private void OnTriggerEnter(Collider other)
    {
        if (hitboxCollider == null) return;

        if (!IsAllowedState()) return;


        //소유자 obj
        GameObject ownerObj = GetOwnerRoot();
        Debug.Log($"[HitBox] other: {other.gameObject}, ownerObj: {ownerObj}, same: {other.gameObject == ownerObj}");
        NetworkCharacterModel target = other.GetComponent<NetworkCharacterModel>();

        if (target != null && other.gameObject != ownerObj)
        {
            if (_hitTargets.Contains(target.gameObject)) return;
            _hitTargets.Add(target.gameObject);

            Vector3 hitPoint  = other.ClosestPoint(hitboxCollider.transform.position);
            Vector3 hitNormal = (hitPoint - other.transform.position).normalized;
            if (hitNormal == Vector3.zero) hitNormal = Vector3.up;

            target.CmdTakeDamage(damage);
            target.CmdSpawnHitEffect(hitPoint, hitNormal, effectIndex); // 인덱스 전달
            hitboxCollider.enabled = false;
            return;
        }

        // 로컬용
        CharacterModel localTarget = other.GetComponent<CharacterModel>();
        if (localTarget != null && other.transform.root.gameObject != ownerObj)
        {
            if (_hitTargets.Contains(localTarget.gameObject)) return;
            _hitTargets.Add(localTarget.gameObject);

            Debug.Log($"{_anim}! {damage} 대미지");
            SpawnHitEffect(other);
            localTarget.TakeDamage(damage);
            hitboxCollider.enabled = false;
            return;
        }
    }

    //소유주 판별 함수로 소유주 판별하는 방식으로 변경. 무기가 root에 있지 않으므로
    private GameObject GetOwnerRoot()
    {
        IWeaponHitBox weapon = GetComponent<IWeaponHitBox>();
        if (weapon != null && weapon.GetOwner() != null)
        {
            return weapon.GetOwner();
        }
        return transform.root.gameObject;
    }

    private bool IsAllowedState()
    {
        if (_anim == null) return true;           // Animator 없으면 제한 없음
        if (allowedStates.Count == 0) return true; // 목록 비어있으면 제한 없음

        AnimatorStateInfo stateInfo = _anim.GetCurrentAnimatorStateInfo(0);
        foreach (string state in allowedStates)
        {
            if (stateInfo.IsName(state)) return true;
        }

        return false;
    }

    private void SpawnHitEffect(Collider other)
    {
        if (hitEffectPrefab == null) return;

        Vector3 hitPoint = other.ClosestPoint(hitboxCollider.transform.position);
        Vector3 hitNormal = (hitPoint - other.transform.position).normalized;
        if (hitNormal == Vector3.zero) hitNormal = Vector3.up;

        GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));

        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear();
            ps.Play();
        }

        Destroy(effect, effectDuration);
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
    
    public void ResetHitbox()
    {
        hitboxCollider.enabled = false;
        _hitTargets.Clear();
    }
}