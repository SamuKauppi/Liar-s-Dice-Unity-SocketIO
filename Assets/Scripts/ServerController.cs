using System.Collections.Generic;
using UnityEngine;
using Firesplash.UnityAssets.SocketIO;
using SimpleJSON;
using UnityEngine.UI;
using TMPro;

public class ServerController : MonoBehaviour
{
    // References
    [SerializeField] private SocketIOCommunicator sioCom;
    [SerializeField] private PlayerScript prefab;
    [SerializeField] private LiarHandling liarHandling;

    // Game
    [SerializeField] private Sprite[] diceSprites;
    [SerializeField] private List<PlayerScript> players = new();
    private int totalDiceCount;

    // CurrentPlayer
    public bool IsMyTurn { get; private set; } = false;
    [SerializeField] private GameObject controlVisual;
    [SerializeField] private GameObject diceVisual;
    [SerializeField] private TMP_Text myName;
    [SerializeField] private Image[] myDices;
    private PlayerScript thisPlayer;
    private int count = 1;
    private int dice = 1;

    // Other players
    [SerializeField] private OtherPlayer[] otherSlots;
    int otherIndex = 0;

    // Bidding
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image diceImage;
    private int l_count = 0;
    private int l_dice = 0;

    // Liar
    [SerializeField] private GameObject liarScreen;

    // Start is called before the first frame update
    void Start()
    {
        diceVisual.SetActive(false);
        controlVisual.SetActive(false);
        liarScreen.SetActive(false);

        foreach (OtherPlayer other in otherSlots)
        {
            other.otherPlayerVisual.SetActive(false);
        }

        sioCom.Instance.On("connect", (payload) =>
        {
            Debug.Log("Connected socket id: " + sioCom.Instance.SocketID);

            // Ollaan yhdistetty serveriin. Kerrotaan serverille, että halutaan luoda pelaaja
            sioCom.Instance.Emit("CREATEPLAYER", "data", true);
        });

        sioCom.Instance.On("INSTANCEPLAYER", (playerinfo) =>
        {
            JSONNode node = JSON.Parse(playerinfo);
            Debug.Log("Create player: " + node["playerName"]);
            PlayerScript playerScript = Instantiate(prefab);
            playerScript.ID = node["socketID"];
            int player_num = int.Parse(node["playerPos"]) + 1;

            if (node["socketID"].Equals(sioCom.Instance.SocketID))
            {
                diceVisual.SetActive(true);
                playerScript.ThisPlayer(myName, node["playerName"], player_num);

                int[] dices = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    dices[i] = int.Parse(node["dices"][i]);
                }
                playerScript.SetDices(dices);
                ShowDices(dices);
                thisPlayer = playerScript;
            }
            else
            {
                playerScript.OtherPlayer(otherSlots[otherIndex].otherPlayerVisual,
                    otherSlots[otherIndex].turnVisuals,
                    otherSlots[otherIndex].DiceImage,
                    otherSlots[otherIndex].m_count,
                    otherSlots[otherIndex].m_name,
                    player_num,
                    otherIndex,
                    node["playerName"]);
                otherIndex = (otherIndex + 1) % 3;
            }

            totalDiceCount += playerScript.DiceCount;
            players.Add(playerScript);
        });

        sioCom.Instance.On("STARTTURN", (bidinfo) =>
        {
            // Logataaan infoa
            JSONNode node = JSON.Parse(bidinfo);
            Debug.Log("it's Player " + node["socketID"] + " turn");
            Debug.Log("last bid: " + node["bid"][0] + " X " + node["bid"][1]);
            l_count = node["bid"][0];
            l_dice = node["bid"][1];

            IsMyTurn = true;
            controlVisual.SetActive(true);
            thisPlayer.WasPrevious = true;
        });

        sioCom.Instance.On("OTHERSTURN", (data) =>
        {
            JSONNode node = JSON.Parse(data);
            foreach (PlayerScript p in players)
            {
                if (p.ID == sioCom.Instance.SocketID)
                    continue;

                if (p.ID == node["socketID"])
                    p.IsMyTurn(true);
                else
                    p.IsMyTurn(false);
            }
        });

        sioCom.Instance.On("MADEBID", (data) =>
        {
            JSONNode node = JSON.Parse(data);
            Debug.Log("Made bid " + node.ToString());
            IsMyTurn = false;
            controlVisual.SetActive(false);
            sioCom.Instance.Emit("TURNENDED", node.ToString(), false);
        });

        sioCom.Instance.On("INVALID_BID", (reason) =>
        {
            // Display the reason why the bid was invalid
            Debug.Log("Invalid bid: " + reason);
        });

        sioCom.Instance.On("UPDATEOTHERS", (data) =>
        {
            JSONNode node = JSON.Parse(data);

            foreach (PlayerScript p in players)
            {
                if (p.ID == sioCom.Instance.SocketID)
                    continue;

                if (p.ID == node["socketID"])
                {
                    l_count = node["bid"][0];
                    l_dice = node["bid"][1];
                    p.UpdateOtherPlayerBid(l_count, l_dice);
                    break;
                }
            }
        });

        sioCom.Instance.On("CALLEDLIAR", (data) =>
        {
            JSONNode node = JSON.Parse(data);
            Debug.Log(node.ToString());
            foreach (PlayerScript p in players)
            {
                if (p.ID == sioCom.Instance.SocketID)
                    continue;

                int[] dices = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    dices[i] = int.Parse(node[p.ID][i]);
                }

                p.SetDices(dices);
            }

            liarHandling.gameObject.SetActive(true);
            liarHandling.ShowPlayers(players.ToArray(), node);
        });

        sioCom.Instance.On("INVALID_CALL", (reason) =>
        {
            Debug.Log(reason);
        });

        sioCom.Instance.On("DELETEPLAYER", (socketID) =>
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                if (players[i].ID == socketID)
                {
                    totalDiceCount -= players[i].DiceCount;

                    if (!players[i].IsThisPlayer)
                    {
                        otherIndex = players[i].OtherPlayerID;
                        players[i].RemovePlayer();
                    }

                    Destroy(players[i].gameObject);
                    players.RemoveAt(i);
                }
            }
        });

        sioCom.Instance.On("ERROR", (error) =>
        {
            Debug.Log("Error from server: " + error);
        });


        sioCom.Instance.Connect();
    }

    public void ShowDices(int[] values)
    {
        for (int i = 0; i < myDices.Length; i++)
        {
            if (i < values.Length)
                myDices[i].sprite = diceSprites[values[i]];
            else
                myDices[i].gameObject.SetActive(false);
        }
    }

    public void ChangeBidCount(int direction)
    {
        count = (count + direction - 1 + (totalDiceCount - 1 + 1)) % (totalDiceCount - 1 + 1) + 1;
        countText.text = count + " X";
    }

    public void ChangeDiceValue(int direction)
    {
        dice = (dice + direction - 1 + (6 - 1 + 1)) % (6 - 1 + 1) + 1;
        diceImage.sprite = diceSprites[dice];
    }

    public void CallLiar()
    {
        sioCom.Instance.Emit("CALLLIAR");
    }

    public void MakeBid()
    {
        sioCom.Instance.Emit("MAKEBID", GetBidData(), false);
    }

    private string GetBidData()
    {
        // Create a JSON object to hold the data
        JSONObject pldata = new();
        pldata.Add("socketID", sioCom.Instance.SocketID);

        // Create a JSON array for the bid values (count and dice)
        JSONArray plBid = new();
        plBid.Add(count);
        plBid.Add(dice);

        // Add the bid array to the payload object
        pldata.Add("bid", plBid);

        return pldata.ToString();
    }
}
