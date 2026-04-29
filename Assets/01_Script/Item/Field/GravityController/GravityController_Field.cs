using UnityEngine;

[CreateAssetMenu(menuName = "Item/Field/GravityController/")]
public class GravityController_Field : FieldItem_Field
{
    // 전역 효과이므로 스폰 위치 커스텀 불필요.
    // 시각 이펙트 위치를 동적으로 결정하고 싶다면
    // GetSpawnPosition()을 오버라이드하면 된다.
}