using System.Collections.Generic;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private Dictionary<int, string> playerRoles = new Dictionary<int, string>();

    void Start()
    {
        AirConsole.instance.onConnect += OnPlayerConnected;
        AirConsole.instance.onDisconnect += OnPlayerDisconnected;
        AirConsole.instance.onMessage += OnMessage;
    }

    void OnPlayerConnected(int deviceId)
    {
        Debug.Log("Player connected: " + deviceId);
        // You might want to wait until a "Start Game" button is pressed to assign roles
    }

    void AssignRoles()
    {
        var connectedDevices = AirConsole.instance.GetControllerDeviceIds();
        if (connectedDevices.Count < 2)
        {
            Debug.LogError("Not enough players.");
            return;
        }

        // Randomly assign leader and assassin
        int leaderIndex = Random.Range(0, connectedDevices.Count);
        int assassinIndex;
        do
        {
            assassinIndex = Random.Range(0, connectedDevices.Count);
        } while (assassinIndex == leaderIndex);

        playerRoles[connectedDevices[leaderIndex]] = "leader";
        playerRoles[connectedDevices[assassinIndex]] = "assassin";

        // Inform the leader to choose a category
        AirConsole.instance.Message(connectedDevices[leaderIndex], new { action = "choose_category" });

        // Inform other players of their role
        for (int i = 0; i < connectedDevices.Count; i++)
        {
            if (i != leaderIndex && i != assassinIndex)
            {
                AirConsole.instance.Message(connectedDevices[i], new { action = "participant" });
            }
            else if (i == assassinIndex)
            {
                AirConsole.instance.Message(connectedDevices[i], new { action = "blend_in" });
            }
        }
    }

    void OnPlayerDisconnected(int deviceId)
    {
        Debug.Log("Player disconnected: " + deviceId);
        playerRoles.Remove(deviceId);
    }

    void OnMessage(int device_id, JToken data)
    {
        Debug.Log("Message from " + device_id + ", data: " + data);

        if (data["action"] != null)
        {
            switch (data["action"].ToString())
            {
                case "start_game":
                    AssignRoles();
                    break;
                    // Handle other actions like voting, responses, etc.
            }
        }
    }

    void OnDestroy()
    {
        if (AirConsole.instance != null)
        {
            AirConsole.instance.onConnect -= OnPlayerConnected;
            AirConsole.instance.onDisconnect -= OnPlayerDisconnected;
            AirConsole.instance.onMessage -= OnMessage;
        }
    }
}