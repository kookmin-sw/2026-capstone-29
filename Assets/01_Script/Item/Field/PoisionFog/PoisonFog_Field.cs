using UnityEngine;

/// <summary>
/// 독안개 필드 아이템 템플릿.
/// 스폰 위치는 (인스펙터 x, 모든 플레이어의 평균 y, 인스펙터 z).
/// 플레이어가 한 명도 없으면 인스펙터 y를 그대로 사용.
///
/// 사용법:
/// 1) Project 창에서 Create → Item → Field → PoisonFogTemplate 으로 에셋 생성
/// 2) 인스펙터에서 fieldPrefab에 PoisonFogField 붙은 프리팹 지정
/// 3) spawnPosition의 x, z를 원하는 좌표로 설정 (y는 런타임에 덮어씌워짐)
/// </summary>
[CreateAssetMenu(menuName = "Item/Field/PoisonFog")]
public class PoisonFog_Field : FieldItem_Field
{
    protected override Vector3 GetSpawnPosition()
    {
        // 부모의 인스펙터 spawnPosition 가져오기 (x, z용)
        Vector3 baseline = base.GetSpawnPosition();

        // 모든 플레이어 찾기. "Player" 태그 기반.
        // (NetworkGameManger가 플레이어 리스트 API를 제공하면 그쪽으로 교체 가능)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        if (players == null || players.Length == 0)
        {
            // 플레이어가 없으면 인스펙터 y 그대로
            return baseline;
        }

        float ySum = 0f;
        for (int i = 0; i < players.Length; i++)
            ySum += players[i].transform.position.y;
        float yAvg = ySum / players.Length;

        return new Vector3(baseline.x, yAvg, baseline.z);
    }
}