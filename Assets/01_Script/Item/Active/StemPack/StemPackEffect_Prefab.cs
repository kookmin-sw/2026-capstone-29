using Mirror;
using StarterAssets;
using UnityEngine;

public class StemPackEffect_Object : NetworkBehaviour
{
    [SyncVar] public uint targetNetId;
    [SyncVar] public float duration;
    [SyncVar] public float moveSpeedMultiplier;
    [SyncVar] public float animSpeedMultiplier;

    [SyncVar(hook = nameof(OnActiveChanged))]
    private bool _isActive;

    private float elapsed;

    private GameObject targetCache;
    private UnifiedThirdPersonController controllerCache;
    private Animator animatorCache;

    private float appliedMoveMul = 1f;
    private float appliedAnimMul = 1f;
    private bool effectApplied;

    private bool HasAuthority => AuthorityGuard.IsOffline || isServer;

    // ── 온라인 진입점 ──
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!ResolveTarget())
        {
            Debug.LogWarning($"[StemPackEffect] 타겟을 찾을 수 없음. netId={targetNetId}. 즉시 파괴.");
            NetworkServer.Destroy(gameObject);
            return;
        }

        ApplyEffect();
        _isActive = true;
    }

    // ── 오프라인 진입점 ──
    public void InitializeOffline(GameObject owner, float dur, float moveMul, float animMul)
    {
        targetCache = owner;
        duration = dur;
        moveSpeedMultiplier = moveMul;
        animSpeedMultiplier = animMul;

        ResolveRefsFromCache();
        ApplyEffect();
    }

    private bool ResolveTarget()
    {
        if (targetCache != null) return true;

        if (AuthorityGuard.IsOffline) return false;

        if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity nid))
            return false;
        if (nid == null) return false;

        targetCache = nid.gameObject;
        ResolveRefsFromCache();
        return true;
    }

    private void ResolveRefsFromCache()
    {
        if (targetCache == null) return;
        controllerCache = targetCache.GetComponent<UnifiedThirdPersonController>();
        animatorCache = targetCache.GetComponentInChildren<Animator>();
    }

    private void ApplyEffect()
    {
        if (effectApplied) return;
        if (targetCache == null) return;

        if (controllerCache != null)
        {
            appliedMoveMul = moveSpeedMultiplier;
            controllerCache.SetSpeedMultiplier(controllerCache.GetSpeedMultiplier() * appliedMoveMul);

            appliedAnimMul = animSpeedMultiplier;
            float currentAnimSpeed = animatorCache != null ? animatorCache.speed : 1f;
            controllerCache.RequestSetAnimatorSpeed(currentAnimSpeed * appliedAnimMul);
        }

        effectApplied = true;
        Debug.Log($"[StemPackEffect] 효과 적용: target={targetCache.name}, moveMul={appliedMoveMul}, animMul={appliedAnimMul}");
    }

    private void RemoveEffect()
    {
        if (!effectApplied) return;

        if (targetCache == null)
        {
            effectApplied = false;
            return;
        }

        if (controllerCache != null && appliedMoveMul > 0f)
        {
            controllerCache.SetSpeedMultiplier(controllerCache.GetSpeedMultiplier() / appliedMoveMul);
        }
        if (controllerCache != null && animatorCache != null && appliedAnimMul > 0f)
        {
            controllerCache.RequestSetAnimatorSpeed(animatorCache.speed / appliedAnimMul);
        }

        effectApplied = false;
        Debug.Log($"[StemPackEffect] 효과 원복: target={targetCache.name}");
    }

    public void RefreshDuration(float newDuration)
    {
        if (!HasAuthority) return;

        duration = newDuration;
        elapsed = 0f;
        Debug.Log($"[StemPackEffect] 리프레시. duration={duration}");
    }

    private void Update()
    {
        if (!HasAuthority) return;
        if (!effectApplied) return;

        // 타겟 소멸 감시
        if (targetCache == null)
        {
            Debug.Log("[StemPackEffect] 타겟 소멸. 자기 파괴.");
            EndAndDestroy();
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            EndAndDestroy();
        }
    }

    private void EndAndDestroy()
    {
        RemoveEffect();
        _isActive = false;

        if (AuthorityGuard.IsOffline)
            Destroy(gameObject);
        else
            NetworkServer.Destroy(gameObject);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (effectApplied) RemoveEffect();
    }

    private void OnDestroy()
    {
        // 오프라인에서 강제 파괴 시 안전 원복.
        if (AuthorityGuard.IsOffline && effectApplied)
        {
            RemoveEffect();
        }
    }

    private void OnActiveChanged(bool oldValue, bool newValue)
    {
        if (newValue)
            Debug.Log($"[StemPackEffect] (Client) 효과 시작: targetNetId={targetNetId}");
        else
            Debug.Log($"[StemPackEffect] (Client) 효과 종료: targetNetId={targetNetId}");
    }

    // ── 활성 효과 탐색 ──
    public static StemPackEffect_Object FindActiveOn(GameObject offlineOwner, uint netId)
    {
        StemPackEffect_Object[] all = FindObjectsOfType<StemPackEffect_Object>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;

            if (AuthorityGuard.IsOffline)
            {
                if (all[i].targetCache == offlineOwner) return all[i];
            }
            else
            {
                if (all[i].targetNetId == netId) return all[i];
            }
        }
        return null;
    }
}