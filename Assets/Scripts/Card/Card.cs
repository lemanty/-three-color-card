using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    public Image cardImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI pointsText;

    public void InitializeCard(CardData cardData)
    {
        if (cardData == null) return;
        if (nameText != null) nameText.text = cardData.cardName;
        if (pointsText != null) pointsText.text = cardData.points.ToString();
        if (cardImage != null && cardData.cardImage != null) cardImage.sprite = cardData.cardImage;
    }
}
