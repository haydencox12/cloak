using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using TMPro;
using System.Linq;
using UnityEditor.VersionControl;

public class GameLogic : MonoBehaviour
{
    public TextMeshProUGUI outputText;
    public int controllingID = -1;

    [SerializeField]
    TextMeshProUGUI timerText;
    [SerializeField]
    int roundTime;
    [SerializeField]
    int totalRounds;
    [SerializeField]
    GameObject answer;
    [SerializeField]
    Transform canvasTransform;
    [SerializeField]
    GameObject avatar;

    [SerializeField]
    GameObject joinGame;
    [SerializeField]
    GameObject startGameButton;
    [SerializeField]
    GameObject timer;
    [SerializeField]
    GameObject output;
    [SerializeField]
    float offset;

    float timerNum;
    int currentRound;
    List<GameObject> answers;
    List<GameObject> avatars;
    
    List<int> highestAnswers;

    
    bool enoughPlayers;
    int numPlayers;
    bool startTimer;
    int gameMode;
    

    Dictionary<int, string> playerRoles = new Dictionary<int, string>();
    Dictionary<int, int> votes = new Dictionary<int, int>();

    void Awake()
    {
        AirConsole.instance.onMessage += OnMessage;
        AirConsole.instance.onConnect += OnConnect;
    }

    /// <param name="device_id">The device_id that connected</param>
    void OnConnect(int device_id)
    {
        numPlayers++;
        // Create a new avatar then reposition all of them
        GameObject avatarClone = Instantiate(avatar, Vector3.zero, Quaternion.identity, canvasTransform);
        avatarClone.GetComponentInChildren<TextMeshProUGUI>().text = "Player " + numPlayers;
        avatars.Add(avatarClone);
        RepositionList(avatars, 50);
        // Make sure there are at least two players to start a game with
        if (AirConsole.instance.GetActivePlayerDeviceIds.Count == 0)
        {
            if (AirConsole.instance.GetControllerDeviceIds().Count >= 2)
            {
                enoughPlayers = true;
            }
        }
    }

    void OnMessage(int fromDeviceID, JToken data)
    {
        if(data ["action"] != null)
        {
            if(data["action"].ToString().Equals("fromDevice"))
            {
                outputText.text = "message from " + fromDeviceID + ", data: " + data;
            }
            if (data["action"].ToString().Equals("takeControl"))
            {
                controllingID = fromDeviceID;
                SendBroadcast("ControlTaken");
                SendMessageToDevice(controllingID, "TakenControl");
            }
        }
        if(data ["input"] != null)
        {
            if (currentRound != -1)
            {
                GameObject answerClone = Instantiate(answer, Vector3.zero, Quaternion.identity, canvasTransform);
                answerClone.GetComponentInChildren<TextMeshProUGUI>().text = data["input"].ToString();
                answers.Add(answerClone);
                RepositionList(answers, Screen.height / 2);
                SendMessageToDevice(fromDeviceID, "Sent");
                SendBroadcast(data["input"].ToString());
            }
            else
            {
                //if(currentRound != -1 && fromDeviceID == controllingID)
                //{
                //    //outputText.fontSize = 72;
                //    outputText.text += "\n" + data["input"].ToString();
                //}
            }
        }
        if (data["action"] != null)
        {
            switch (data["action"].ToString())
            {
                case "gameModeSelected":
                    int gameMode = (int)data["mode"];  // Assuming mode is an integer from 0 to 3
                    StartGameWithMode(gameMode);
                    HideGameModeSelection();
                    break;
            }
        }

        if (data["action"] != null && data["action"].ToString() == "vote")
        {
            int voterId = fromDeviceID;
            int votedPlayerId = (int)data["votedPlayerId"]; // Ensure the data contains the player ID being voted for

            // Check if this voter has already voted to prevent double voting
            if (!votes.ContainsKey(voterId))
            {
                votes[voterId] = votedPlayerId;
                CheckIfAllVotesAreIn();
            }
        }

        if (data["vote"] != null)
        {
            int i = 0;
            foreach(GameObject answer in answers)
            {
                if(answer.GetComponentInChildren<TextMeshProUGUI>().text.Equals(data["vote"].ToString()))
                {
                    votes[i]++;
                }
                i++;
            }
            SendMessageToDevice(fromDeviceID, "Voted");
        }
    }

    void onDestroy()
    {
        //unregister events
        if(AirConsole.instance != null)
        {
            AirConsole.instance.onMessage -= OnMessage;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        enoughPlayers = false;
        answers = new List<GameObject>();
        avatars = new List<GameObject>();
  
        highestAnswers = new List<int>();
        timerNum = roundTime;
        startTimer = false;
        currentRound = -1;
    }

    // Update is called once per frame
    void Update()
    {
        // Only run the timer after the game has started and there are still more rounds
        if (startTimer && currentRound <= totalRounds)
        {
            timerNum -= Time.deltaTime;
            timerText.text = timerNum.ToString("F0");
            if (timerNum <= 0)
            {
                InitiateVoting();  
                // Call the method to initiate voting
                startTimer = false;
            }
           
        }
    }

    void SendBroadcast(JToken message)
    {
        // Assuming AirConsole.instance.Message can take a JToken directly
        foreach (int deviceID in AirConsole.instance.GetControllerDeviceIds())
        {
            AirConsole.instance.Message(deviceID, message);
        }
    }

    void SendMessageToDevice(int deviceID, JToken data)
    {
        AirConsole.instance.Message(deviceID, data);
    }

    public void SetupRound()
    {
        if (enoughPlayers)
        {
            // Switch out the main menu to display the gameplay objects
            currentRound = 0;
            joinGame.SetActive(false);
            startGameButton.SetActive(false);
            output.SetActive(true);
            SendBroadcast("SetupRound");
            AssignRoles();
            BroadcastRoles();

            // Initiate game mode selection by a randomly chosen player
            StartGameModeSelection();  // This should handle selecting the game mode
        }
    }

    void StartGameModeSelection()
    {
        List<int> deviceIds = AirConsole.instance.GetControllerDeviceIds();
        if (deviceIds.Count == 0) return;

        // Randomly select a player to choose the game mode
        int selectorIndex = Random.Range(0, deviceIds.Count);
        int selectorDeviceId = deviceIds[selectorIndex];
        controllingID = selectorDeviceId;  // Store the ID of the player who controls the game mode selection

        // Send a message to all devices to update their UI, only the selected player gets the selection UI
        foreach (var deviceId in deviceIds)
        {
            if (deviceId == selectorDeviceId)
            {
                JObject message = new JObject();
                message["action"] = "chooseGameMode";
                AirConsole.instance.Message(deviceId, message);
            }
            else
            {
                JObject message = new JObject();
                message["action"] = "wait";
                AirConsole.instance.Message(deviceId, message);
            }
        }
    }

    void HideGameModeSelection()
    {
        List<int> deviceIds = AirConsole.instance.GetControllerDeviceIds();
        foreach (var deviceId in deviceIds)
        {
            JObject message = new JObject();
            message["action"] = "hideGameModeSelection";
            AirConsole.instance.Message(deviceId, message);
        }
    }


    void StartGameWithMode(int mode)
    {
        gameMode = mode;
        currentRound = 0;  // Reset or set the round to begin
        outputText.text = "Game Mode: " + GetGameModeName(mode);
        timer.SetActive(true);
        startTimer = true;  // Continue to setup the round as needed

    }

    string GetGameModeName(int mode)
    {
        switch (mode)
        {
            case 0: return "Truth and Lies";
            case 1: return "Find the Leader";
            case 2: return "Silent Assassin";
            case 3: return "Last Stand";
            default: return "Unknown Mode";
        }
    }



    void AssignRoles()
    {
        List<int> deviceIds = AirConsole.instance.GetControllerDeviceIds();
        int totalPlayers = deviceIds.Count;

        if (totalPlayers < 2)
        {
            Debug.LogError("Not enough players to assign special roles!");
            return; // Ensure there are enough players for the roles you need.
        }

        // Randomly select an index for the leader and assassin
        int leaderIndex = Random.Range(0, totalPlayers);
        int assassinIndex;
        do
        {
            assassinIndex = Random.Range(0, totalPlayers);
        } while (assassinIndex == leaderIndex); // Ensure the assassin and leader are not the same player

        for (int i = 0; i < totalPlayers; i++)
        {
            string role = "Citizen"; // Default role
            if (i == leaderIndex)
            {
                role = "Leader";
            }
            else if (i == assassinIndex)
            {
                role = "Assassin";
            }

            playerRoles[deviceIds[i]] = role;
        }
    }

    void BroadcastRoles()
    {
        foreach (KeyValuePair<int, string> entry in playerRoles)
        {
            JObject message = new JObject();
            message["action"] = "assignRole";
            message["role"] = entry.Value;
            AirConsole.instance.Message(entry.Key, message);
        }
    }

    void StartRound()
    {
        
        // Update the header for the selected story type
        if (currentRound == 0)
        {
            switch(gameMode)
            {
               
            }
        }
        // Adding the answer to the main output
        else
        {
            outputText.text += "\n" ;
            // Repositioning the main text to *try* to keep everything on the screen
            outputText.transform.position += new Vector3(0, offset, 0);
        }
        // Reset the answers and voting
        for(int i = 0; i < answers.Count; i++)
        {
            Destroy(answers[i]);
        }
        answers.Clear();
        highestAnswers.Clear();
        votes.Clear();
        //outputText.fontSize = 36;
        currentRound++;
        timerNum = roundTime;
        // Show a different header for each round based on the story type
        
        controllingID = -1;
        SendBroadcast("Reset");
        SendBroadcast("SetupRound");
    }

    void InitiateVoting()
    {
        votes.Clear(); // Clear previous votes
        outputText.text = "Voting has started. Look at your screens to vote.";
        JObject message = new JObject();
        message["action"] = "startVoting";
        AirConsole.instance.Broadcast(message);
        // Set up a timer to end voting if needed or wait for all votes to come in
    }

    void CheckIfAllVotesAreIn()
    {
        if (votes.Count == numPlayers)
        { // Ensure all connected players have voted
          // Determine the player with the most votes
            var mostVotedPlayer = votes.GroupBy(v => v.Value)
                                       .OrderByDescending(gp => gp.Count())
                                       .First()
                                       .Key;
            bool isAssassin = playerRoles[mostVotedPlayer] == "Assassin";
            ProcessVotingResult(mostVotedPlayer, isAssassin);
        }
    }

    void ProcessVotingResult(int playerId, bool isAssassin)
    {
        outputText.text = $"Player {playerId} received the most votes. They are {(isAssassin ? "" : "not ")}the assassin.";
        StartCoroutine(DisplayResults(isAssassin));
    }

    IEnumerator DisplayResults(bool isAssassin)
    {
        yield return new WaitForSeconds(3); // Show results for 3 seconds
        if (isAssassin)
        {
            outputText.text = "The Assassin has been found. Game over.";
            yield return new WaitForSeconds(3);
            // Optionally reset the game or return to main menu
        }
        else
        {
            outputText.text = "Incorrect guess. The game continues...";
            yield return new WaitForSeconds(3);
            StartGameModeSelection(); // Restart game mode selection
        }
    }

    void RepositionList(List<GameObject> list, float height)
    {
        // Evenly spaces out the players and the answers on the screen horizontally
        int i = 1;
        foreach(GameObject item in list)
        {
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.SetPositionAndRotation(new Vector3(((Screen.width / (list.Count + 1)) * i), height, 0), Quaternion.identity);
            i++;
        }
    }
}


