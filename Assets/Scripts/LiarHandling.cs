using SimpleJSON;
using System.Collections;
using TMPro;
using UnityEngine;

public class LiarHandling : MonoBehaviour
{
    [SerializeField] private TMP_Text callingText;
    [SerializeField] private Sprite[] diceSprites;
    [SerializeField] private LiarScreenPlayer[] playerVisuals;

    public void ShowPlayers(PlayerScript[] players, JSONNode node)
    {
        // Start the private coroutine using the MonoBehaviour's StartCoroutine method
        StartCoroutine(ShowPlayersCoroutine(players, node));
    }

    // Private coroutine that does the actual work
    private IEnumerator ShowPlayersCoroutine(PlayerScript[] players, JSONNode node)
    {
        string winnerID = node["winner"]["socketID"];
        string winner_name = node["winner"]["playername"];

        string loserID = node["loser"]["socketID"];
        string loser_name = node["loser"]["playername"];

        string callerName = node["caller"];
        string calledName = node["called"];

        int count = int.Parse(node["bid"]["count"]);
        int dice = int.Parse(node["bid"]["dice"]);

        int total_count = 0;

        for (int i = 0; i < playerVisuals.Length; i++)
        {
            if (i >= players.Length)
            {
                playerVisuals[i].showPlayer.SetActive(false);
                continue;
            }

            var player = players[i];
            var playerVisual = playerVisuals[i];

            playerVisual.nameText.text = player.PlayerName;

            bool isWinnerOrLoser = player.ID.Equals(winnerID) || player.ID.Equals(loserID);
            if (isWinnerOrLoser)
            {
                playerVisual.IsWinnerImage.gameObject.SetActive(true);
                playerVisual.IsWinnerImage.color = player.ID.Equals(winnerID) ? Color.green : Color.red;
            }

            for (int di = 0; di < playerVisual.diceImages.Length; di++)
            {
                if (di >= player.Dices.Length)
                {
                    playerVisual.diceImages[di].gameObject.SetActive(false);
                    continue;
                }

                playerVisual.diceImages[di].sprite = diceSprites[player.Dices[di]];

                if (player.Dices[di] == 1 || player.Dices[di] == dice)
                {
                    playerVisual.diceImages[di].transform.GetChild(0).gameObject.SetActive(true);
                    total_count++;
                }
            }

        }
        yield return new WaitForSecondsRealtime(2f);
        callingText.text = callerName + " called " + calledName + " a liar!\n";
        
        yield return new WaitForSecondsRealtime(2f);
        callingText.text += " The bid was: " + count + " x " + dice + " and there were " + total_count + " of " + dice + ".\n";
        
        yield return new WaitForSecondsRealtime(2f);
        callingText.text += winner_name + " was correct! Shame on you, " + loser_name + ", for lying...";

    }

}
