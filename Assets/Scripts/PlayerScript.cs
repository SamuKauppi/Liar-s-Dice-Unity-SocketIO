using SimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerScript : MonoBehaviour
{
    public string PlayerName { get; private set; }
    public string ID { get; set; } = "";
    public int DiceCount { get; private set; } = 5;
    public int[] Dices { get; private set; } = new int[5];

    // Player refs
    public bool IsThisPlayer { get; private set; }
    public int OtherPlayerID { get; private set; }
    private TMP_Text m_name;

    // Other player refs
    [SerializeField] private Sprite[] diceSprites;
    private GameObject otherPlayerVisual;
    private GameObject turnVisual;
    private Image m_dice;
    private TMP_Text m_count;
    public bool WasPrevious { get; set; }

    public void ThisPlayer(TMP_Text nameText, string playerName, int id)
    {
        IsThisPlayer = true;
        PlayerName = id + ". " + playerName + " (you)";
        m_name = nameText;
        m_name.text = PlayerName;
    }

    public void OtherPlayer(GameObject otherVis,
        GameObject turnvis,
        Image dice,
        TMP_Text count,
        TMP_Text name,
        int id,
        int otherId,
        string playername)
    {
        otherPlayerVisual = otherVis;
        otherPlayerVisual.SetActive(true);
        turnVisual = turnvis;
        turnVisual.SetActive(false);
        m_dice = dice;
        m_dice.gameObject.SetActive(false);
        m_count = count;
        m_count.gameObject.SetActive(false);
        m_name = name;
        PlayerName = id + ". " + playername;
        m_name.text = PlayerName;
        OtherPlayerID = otherId;
    }

    public void UpdateOtherPlayerBid(int count, int dice)
    {
        m_count.gameObject.SetActive(true);
        m_count.text = count + " X";
        m_dice.gameObject.SetActive(true);
        m_dice.sprite = diceSprites[dice];
    }

    public void RemovePlayer()
    {
        otherPlayerVisual.SetActive(false);
    }

    public void IsMyTurn(bool value)
    {
        if (value)
        {
            turnVisual.gameObject.SetActive(true);
            turnVisual.GetComponent<Image>().color = Color.white;
            WasPrevious = true;
        }
        else if (!value && WasPrevious)
        {
            turnVisual.GetComponent<Image>().color = Color.gray;
            WasPrevious = false;
        }
        else
        {
            turnVisual.gameObject.SetActive(false);
        }

    }

    public void SetDices(int[] dices)
    {
        Dices = dices;
    }
}
