using UnityEngine;
using Mirror;
using StarterAssets;

// 중력변환장치 효과.
// 스폰 시 모든 플레이어의 중력 배율을 변경하고, 파괴 시 원래 값으로 복원한다.
public class GravityController_Effect : FieldEffect
{
    [Header("점프 배율 설정")]
    [Tooltip("점프 배율. 1보다 작으면 낮은 점프력(낮게 뜀), 1보다 크면 높게 뜀.")]
    [SerializeField] private float jumpHeightMultiplier = 1.5f;

    [Header("중력변환 설정")]
    [Tooltip("중력 배율. 1보다 작으면 낮은 중력(높이 뜀), 1보다 크면 강한 중력.")]
    [SerializeField] private float gravityMultiplier = 0.3f;

    private bool _applied = false;

    public override void Initialize(float duration)
    {
        Debug.Log("효과 발동!");
        base.Initialize(duration);

        if (!isServer) return;

        ApplyGravityToAll(jumpHeightMultiplier, gravityMultiplier);
        _applied = true;
    }

    private void OnDestroy()
    {
        // 서버·클라 모두에서 복원 시도 (RPC가 도달하지 못할 경우 대비)
        RestoreGravityLocal();
    }

    // 서버에서 모든 플레이어를 찾아 중력 배율을 적용한다. ClientRpc로 각 클라이언트에도 동일하게 적용.
    [Server]
    private void ApplyGravityToAll(float jumpHeightMultiplier, float gravityMultiplier)
    {
        RpcApplyGravity(jumpHeightMultiplier, gravityMultiplier);

        // 서버 자신도 호스트 플레이어가 있을 수 있으므로 로컬 적용
        ApplyGravityLocal(jumpHeightMultiplier, gravityMultiplier);
    }

    [Server]
    private void RestoreGravityOnAll()
    {
        RpcRestoreGravity();
        RestoreGravityLocal();
    }

    [ClientRpc]
    private void RpcApplyGravity(float jumpHeightMultiplier, float gravityMultiplier)
    {
        ApplyGravityLocal(jumpHeightMultiplier, gravityMultiplier);
    }

    [ClientRpc]
    private void RpcRestoreGravity()
    {
        RestoreGravityLocal();
    }

    private void ApplyGravityLocal(float jumpHeightMultiplier, float gravityMultiplier)
    {
        foreach (var controller in FindObjectsOfType<UnifiedThirdPersonController>())
        {
            controller.SetGravityMultiplier(jumpHeightMultiplier, gravityMultiplier);
        }
    }

    private void RestoreGravityLocal()
    {
        foreach (var controller in FindObjectsOfType<UnifiedThirdPersonController>())
        {
            controller.ResetGravity();
        }
    }
}