using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("기본 정보")]
    public string characterName;
    [TextArea(2, 4)]
    public string description;

    [Header("이미지")]
    public Sprite portrait;    // 선택창 풀샷 일러스트
    public Sprite thumbnail;   // 하단 썸네일

    [Header("테마 색상")]
    public Color accentColor = Color.white;
}