# Unified Item Scripts

`Item/` 폴더의 네트워크 전용 아이템 로직을 오프라인(싱글)에서도 동작하도록 통합한 버전.

> **기존 파일은 건드리지 않았음.** 새로 추가된 파일이며, 프리팹 컴포넌트 교체는 사용자 확인 후 수동 작업.

## 포함 파일

| 파일 | 역할 | 대체 대상 |
|---|---|---|
| `UnifiedItemPickUp.cs` | 플레이어에 부착. 트리거로 아이템 감지. 온라인/오프라인 분기. | `Item/ItemPickUp.cs` |
| `UnifiedSetItem.cs` | 각 필드 아이템에 부착. `IEquip.Save` 구현. 온라인/오프라인 분기. | `Item/SetItem.cs` |
| `UnifiedItemManager.cs` | 플레이어에 부착. 무기/액티브/패시브 상태·타이머 관리. | `Item/ItemManager.cs` |

## 동작 원리 요약

1. `UnifiedItemPickUp.OnTriggerEnter`
   - `AuthorityGuard.IsLocallyControlled` 로 자기 자신만 처리.
   - `RequestPickUp()` → 오프라인이면 `IEquip.Save` 직접 호출, 온라인이면 `CmdPickUp` 경유.

2. `UnifiedSetItem.Save`
   - 오프라인: 직접 `UnifiedItemManager`(없으면 레거시 `ItemManager`)에 세팅 + `SummonWeapon`으로 받은 오브젝트 `Instantiate` 결과를 그대로 사용 + `Destroy(item)`.
   - 온라인: `CmdSave` → 서버에서 `NetworkServer.Spawn` + `NetworkServer.Destroy` + `ClientRpc` 알림.
   - 온/오프라인 양쪽에서 `UnifiedItemManager` 우선, 없으면 `ItemManager`도 지원.

3. `UnifiedItemManager.Update`
   - 온라인: `isServer`에서만 타이머. 기존 동작과 동일.
   - 오프라인: `AuthorityGuard.IsOffline`이면 본인이 타이머 실행.
   - 알림은 오프라인이면 로컬 메서드 직접 호출, 온라인이면 `RpcOn*` 브로드캐스트. `ClientRpc` 본체도 로컬 메서드를 호출하도록 구조 통일.

## 프리팹 교체 체크리스트 (수동 작업)

> **테스트용 사본**을 먼저 만들어 실험할 것. 기존 프리팹에 바로 적용하지 말 것.

### 플레이어 프리팹
1. `ItemPickUp` 컴포넌트 제거 → `UnifiedItemPickUp` 추가.
2. `ItemManager` 컴포넌트 제거 → `UnifiedItemManager` 추가.
3. 호환: `UnifiedSetItem` / 기존 `SetItem` 어느 쪽이 붙어 있어도 `UnifiedItemManager`가 받아줌.

### 필드 아이템 프리팹 (각 무기/액티브/패시브/필드)
1. `SetItem` 컴포넌트 제거 → `UnifiedSetItem` 추가.
2. Inspector 에서 `itemAsset` 슬롯에 기존 `ScriptableObject`(예: `WeaponEffect_SummonWeapon` 인스턴스) 다시 연결.

### 태그 확인
- 아이템 프리팹 태그는 기존 로직과 동일하게 `Weapon / Active / Passive / Field` 중 하나로 유지.

## 호환성

- 레거시 `ItemManager`가 붙은 플레이어에 `UnifiedSetItem`을 사용해도 동작(온·오프라인 양쪽).
- 레거시 `SetItem`이 붙은 필드 아이템을 `UnifiedItemPickUp`이 주워도 동작(온라인에 한함 — 레거시 `SetItem`은 `CmdSave`만 가지고 있어 오프라인에선 실패).
- 완전한 오프라인 동작을 원하면 **3개 모두 통합본으로 교체** 필요.

## 정리/롤백

- `Item/Unified/` 폴더 전체를 삭제하면 기존 동작 그대로 복원됨(프리팹을 아직 교체하지 않은 경우).
- 프리팹을 교체한 경우엔 백업 프리팹으로 되돌리면 됨.
