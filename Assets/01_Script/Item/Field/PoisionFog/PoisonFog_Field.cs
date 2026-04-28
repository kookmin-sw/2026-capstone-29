using UnityEngine;

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