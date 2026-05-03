using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
///   1. Activate 호출 -> owner 전방 일정 거리에 덫 오브젝트 소환
///   2. 덫이 활성화되어 trapDuration 동안 플레이어 감지
///   3. 덫에 닿은 플레이어는 holdDuration 동안 그 자리에 속박
///   4. duration 만료 -> 코루틴 자연 종료 (덫 오브젝트는 자체 lifetime으로 소멸)
///
/// 온/오프라인 분기:
///   - 온라인: NetworkServer.Spawn으로 등록.
///   - 오프라인: 단순 Instantiate
/// </summary>
[CreateAssetMenu(menuName = "Item/Active/Trap/Effect")]
public class UnifiedTrap_Effect : ScriptableObject, IActive
{
    [Header("아이템 설정")]
    [SerializeField] private float duration = 3f;

    [Header("덫 생성")]
    [Tooltip("owner 기준 덫 소환 오프셋 (x: 우측, y: 위, z: 전방)")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 2f);

    [Header("덫 설정")]
    [Tooltip("덫이 필드에 존재하는 시간 (초)")]
    [SerializeField] private float trapLifetime = 30f;
    [Tooltip("덫 트리거 반경")]
    [SerializeField] private float trapRadius = 1.2f;
    [Tooltip("플레이어가 덫에 걸렸을 때 묶이는 시간 (초)")]
    [SerializeField] private float holdDuration = 3f;
    [Tooltip("덫 시각 효과 색상")]
    [SerializeField] private Color trapColor = new Color(0.8f, 0.2f, 0.2f, 0.6f);
    [Tooltip("한 번 발동 후 사라지는지 여부 (true: 1회용, false: trapLifetime 동안 유지)")]
    [SerializeField] private bool consumeOnTrigger = true;

    [Header("프리팹")]
    [Tooltip("덫 프리팹 (UnifiedTrap 컴포넌트 보유)")]
    [SerializeField] private GameObject trapPrefab;

    public float AvailableTime => duration;

    public virtual IEnumerator Activate(GameObject owner)
    {
        if (owner == null) yield break;

        Debug.Log("[UnifiedTrap] 활성화 시작");

        // 덫 소환 위치 계산
        Vector3 spawnPos = CalculateSpawnPosition(owner.transform);

        // 덫 오브젝트 생성
        SpawnTrap(spawnPos, owner);

        // duration 동안 대기 (소환 자체가 액티브의 핵심이므로 이후엔 덫이 자체 수명을 가짐)
        yield return new WaitForSeconds(duration);

        Debug.Log("[UnifiedTrap] 활성화 종료");
    }

    public virtual void OnDeactivate(GameObject owner)
    {
        Debug.Log("[UnifiedTrap] 비활성화");
        // 덫은 본인의 lifetime으로 자체 소멸하므로 별도 정리 없음
    }

    // 덫 소환 위치 계산
    private Vector3 CalculateSpawnPosition(Transform ownerTransform)
    {
        return ownerTransform.position
             + ownerTransform.forward * spawnOffset.z
             + ownerTransform.up * spawnOffset.y
             + ownerTransform.right * spawnOffset.x;
    }

    // 덫 오브젝트 생성
    private void SpawnTrap(Vector3 position, GameObject owner)
    {
        if (AuthorityGuard.IsOffline)
        {
            SpawnTrapLocal(position, owner);
            return;
        }

        SpawnTrapNetwork(position, owner);
    }

    private void SpawnTrapLocal(Vector3 position, GameObject owner)
    {
        GameObject trapObj;

        if (trapPrefab != null)
        {
            trapObj = Instantiate(trapPrefab, position, Quaternion.identity);
        }
        else
        {
            trapObj = new GameObject("UnifiedTrap_Runtime");
            trapObj.transform.position = position;
            if (trapObj.GetComponent<UnifiedTrap_Object>() == null)
                trapObj.AddComponent<UnifiedTrap_Object>();
        }

        InjectTrapParameters(trapObj, owner);
        HardenOfflineObject(trapObj);
    }

    private void SpawnTrapNetwork(Vector3 position, GameObject owner)
    {
        NetworkIdentity ownerIdentity = owner.GetComponent<NetworkIdentity>();
        if (ownerIdentity == null || !NetworkServer.active)
        {
            Debug.LogWarning("[UnifiedTrap] 서버가 아니거나 NetworkIdentity가 없음.");
            return;
        }

        GameObject trapObj;

        if (trapPrefab != null)
        {
            trapObj = Instantiate(trapPrefab, position, Quaternion.identity);
        }
        else
        {
            trapObj = new GameObject("NetworkTrap_Runtime");
            trapObj.transform.position = position;

            if (trapObj.GetComponent<NetworkIdentity>() == null)
                trapObj.AddComponent<NetworkIdentity>();

            if (trapObj.GetComponent<UnifiedTrap_Object>() == null)
                trapObj.AddComponent<UnifiedTrap_Object>();
        }

        InjectTrapParameters(trapObj, owner);
        NetworkServer.Spawn(trapObj);
    }

    // 덫 오브젝트에 파라미터 주입
    private void InjectTrapParameters(GameObject trapObj, GameObject owner)
    {
        UnifiedTrap_Object trap = trapObj.GetComponent<UnifiedTrap_Object>();
        if (trap == null) return;

        trap.lifetime = trapLifetime;
        trap.triggerRadius = trapRadius;
        trap.holdDuration = holdDuration;
        trap.trapColor = trapColor;
        trap.consumeOnTrigger = consumeOnTrigger;
        trap.SetOwner(owner);
    }

    // 온라인과 오프라인 분기에서의 처리 관리
    private static void HardenOfflineObject(GameObject obj)
    {
        if (obj == null) return;
        if (!AuthorityGuard.IsOffline) return; // 온라인에선 Mirror가 관리

        if (obj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;

        if (!obj.activeSelf) obj.SetActive(true);
    }
}