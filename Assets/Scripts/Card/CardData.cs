using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName;
    public int points;
    public Sprite cardImage;
}