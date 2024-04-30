using UnityEngine;

public class Player : MonoBehaviour
{
    public string playerName = "New Player";

    public void SetName(string newName)
    {
        playerName = newName;
        // Update the UI or other elements to show the new name
    }
}