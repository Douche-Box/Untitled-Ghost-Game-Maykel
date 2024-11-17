using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Managing.Logging;
using TMPro;
using FishNet;
using FishNet.Component.Spawning;
using System.Linq;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine.SceneManagement;
using FishNet.Managing.Scened;
using UnityEngine.UI;

/// <summary>
/// Handles team management, including joining teams, updating UI, and starting the game.
/// </summary>
public class TeamManager : NetworkBehaviour
{
    public List<TeamData> teams = new List<TeamData>(); // List of teams
    [SyncVar] public int allClients;
    [SyncVar] public int currentClients;
    public GameObject[] rects; // Containers for each team's UI elements

    [SerializeField] private GameObject[] switchTeamButtons;
    [SerializeField] private GameObject UIprefab;

    private bool done; // Tracks if initialization is complete
    public string LobbyToLoad; // Target scene to load for the game

    [SerializeField] private LoadManager loader;

    [SyncVar]
    public List<GameObject> players = new();

    /// <summary>
    /// Button-triggered function to join a team.
    /// </summary>
    public void JointTeamBtn(int teamInt)
    {
        int id = InstanceFinder.ClientManager.Connection.ClientId;
        JoinTeam(teamInt, id);
    }

    private void Update()
    {
        // Ensure the loader is assigned if not already
        if (loader == null)
            loader = FindObjectOfType<LoadManager>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        foreach (GameObject player in players)
        {
            player.GetComponent<PlayerData>().SetPlayerTeam();
        }
    }

    /// <summary>
    /// Server function to assign a player to a team.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void JoinTeam(int teamInt, int localPlayerId)
    {
        for (int y = 0; y < teams.Count; y++)
        {
            for (int i = 0; i < currentClients; i++)
            {
                if (teams[y].tData.Count > i && teams[y].tData[i].playerId == localPlayerId)
                {
                    if (teams[teamInt].tData.Count <= i)
                    {
                        // Move player to the new team
                        teams[teamInt].tData.Add(teams[y].tData[i]);
                        SetTeam(teams[y].tData[i].gameObject, teamInt);
                        teams[teams[y].tData[i].teamID].tData.Remove(teams[y].tData[i]);

                        UpdatePlayerTeam(localPlayerId, teamInt);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates a player's team in the UI and logic.
    /// </summary>
    private void UpdatePlayerTeam(int localPlayerId, int teamInt)
    {
        foreach (GameObject player in players)
        {
            PlayerData data = player.GetComponent<PlayerData>();
            if (data.playerId == localPlayerId)
            {
                data.UI.transform.SetParent(rects[teamInt].transform);
                StartCoroutine(RefreshUI());
            }
        }
    }

    /// <summary>
    /// Observer function to synchronize team changes.
    /// </summary>
    [ObserversRpc]
    public void SetTeam(GameObject data, int teamInt)
    {
        PlayerData playerData = data.GetComponent<PlayerData>();
        teams[teamInt].tData.Add(playerData);
        teams[playerData.teamID].tData.Remove(playerData);
    }

    private IEnumerator RefreshUI()
    {
        yield return new WaitForSeconds(0.1f);
        SetParents();
    }

    /// <summary>
    /// Updates parent UI elements for all players.
    /// </summary>
    [ObserversRpc]
    public void SetParents()
    {
        foreach (GameObject player in players)
        {
            player.GetComponent<PlayerData>().UI.transform.SetParent(
                rects[player.GetComponent<PlayerData>().teamID].transform
            );
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddTeam(PlayerData player, int team)
    {
        teams[team].tData.Add(player);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ParentPlayerUIServer(int team)
    {
        foreach (GameObject player in players)
        {
            player.GetComponent<PlayerData>().UI.transform.SetParent(
                rects[player.GetComponent<PlayerData>().teamID].transform
            );
        }
        ParentPlayerUIObserver(team);
    }

    [ObserversRpc]
    public void ParentPlayerUIObserver(int team)
    {
        foreach (GameObject player in players)
        {
            player.GetComponent<PlayerData>().UI.transform.SetParent(
                rects[player.GetComponent<PlayerData>().teamID].transform
            );
        }
    }

    /// <summary>
    /// Configures team-switch buttons based on the player's current team.
    /// </summary>
    [ObserversRpc]
    public void SetTeamSwitchButtons()
    {
        foreach (GameObject player in players)
        {
            PlayerData data = player.GetComponent<PlayerData>();
            if (LocalConnection.ClientId == data.playerId)
            {
                for (int x = 0; x < switchTeamButtons.Length; x++)
                {
                    switchTeamButtons[x].SetActive(x != data.teamID);
                }
            }
        }
    }

    /// <summary>
    /// Updates player names on their respective UI elements.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerNameServer()
    {
        foreach (GameObject player in players)
        {
            player.GetComponent<PlayerData>().UI.transform.GetComponentInChildren<TMP_Text>().text =
                player.GetComponent<PlayerData>().username;
        }
        SetPlayerNameObserver();
    }

    [ObserversRpc]
    public void SetPlayerNameObserver()
    {
        foreach (GameObject player in players)
        {
            player.GetComponent<PlayerData>().UI.transform.GetComponentInChildren<TMP_Text>().text =
                player.GetComponent<PlayerData>().username;
        }
    }

    /// <summary>
    /// Starts the game if all players are ready.
    /// </summary>
    public void StartGameButton()
    {
        if (players.All(player => player.GetComponent<PlayerData>().isReady))
        {
            StartGame();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartGame()
    {
        loader.StartLoading = true;
        loader.SceneToLoad = LobbyToLoad;
        loader.SceneToUnload = "Lobby Test";
    }

    /// <summary>
    /// Toggles readiness for the local player.
    /// </summary>
    public void SetReady()
    {
        int id = InstanceFinder.ClientManager.Connection.ClientId;
        ready = !ready;
        ChangeReadyServer(ready, id);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangeReadyServer(bool value, int id)
    {
        foreach (GameObject player in players)
        {
            PlayerData data = player.GetComponent<PlayerData>();
            if (data.playerId == id)
            {
                data.isReady = value;
            }
        }
    }
}