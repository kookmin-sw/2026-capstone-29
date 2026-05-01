using UnityEngine;

/// <summary>
/// <see cref="CharacterHitBox"/>가 <see cref="CharacterHitBox.GetOwnerRoot"/>에서
/// 같은 GameObject에 붙은 <see cref="IWeaponHitBox"/>만 찾기 때문에,
/// 무기 루트가 아니라 자식에 HitBox가 있는 구조(예: UnifiedWeaponArrow의 자식 hitbox)에서
/// 소유자를 찾지 못하는 문제를 해결하는 릴레이.
///
/// 사용법:
/// - 무기 로직 측( <see cref="UnifiedWeaponArrow"/> 등 )이 Awake 단계에서
///   HitBox GameObject에 이 컴포넌트를 AddComponent 하고 <see cref="SetSource"/>로
///   본인을 넘겨 준다.
/// - <see cref="CharacterHitBox.GetOwnerRoot"/>가 같은 GameObject에서
///   <see cref="IWeaponHitBox"/>를 찾을 때 이 릴레이가 잡혀서,
///   <see cref="GetOwner"/>를 통해 실제 소유자를 돌려준다.
/// </summary>
public class WeaponOwnerRelay : MonoBehaviour, IWeaponHitBox
{
    [Tooltip("디버그용 — 원본 IWeaponHitBox 구현체 (MonoBehaviour).")]
    [SerializeField] private MonoBehaviour sourceBehaviour;

    private IWeaponHitBox _source;

    public void SetSource(IWeaponHitBox source)
    {
        _source = source;
        sourceBehaviour = source as MonoBehaviour;
    }

    public GameObject GetOwner()
    {
        if (_source != null) return _source.GetOwner();

        // 참조가 끊겼어도 Inspector에 연결된 MonoBehaviour로 복구 시도
        if (sourceBehaviour is IWeaponHitBox fallback)
        {
            _source = fallback;
            return fallback.GetOwner();
        }
        return null;
    }

    public void SetOwner(GameObject user)
    {
        if (_source != null) _source.SetOwner(user);
        else if (sourceBehaviour is IWeaponHitBox fallback)
        {
            _source = fallback;
            fallback.SetOwner(user);
        }
    }
}
