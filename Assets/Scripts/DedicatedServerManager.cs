using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Manages dedicated server mode
/// Automatically becomes master client and handles bot spawning
/// Run with -server command line argument or check "Run As Server" in inspector
/// </summary>
public class DedicatedServerManager : MonoBehaviourPunCallbacks
{
    [Header("Server Settings")]
    [Tooltip("Is this instance running as a dedicated server?")]
    public bool isServer = false;

    [Tooltip("Auto-detect server mode from command line arguments")]
    public bool autoDetectServerMode = true;

    [Tooltip("Server should run headless (no rendering)")]
    public bool headlessMode = true;

    [Tooltip("Target FPS for server (lower = less CPU)")]
    public int serverTargetFPS = 30;

    [Header("Local Photon Server")]
    [Tooltip("Use local Photon Server instead of Cloud")]
    public bool useLocalPhotonServer = false;

    [Tooltip("Local Photon Server IP address")]
    public string serverAddress = "127.0.0.1";

    [Tooltip("Local Photon Server port")]
    public int serverPort = 5055;

    [Header("Connection")]
    [Tooltip("Room name for server to create/join")]
    public string serverRoomName = "KlyraServer";

    [Tooltip("Max players in room")]
    public int maxPlayers = 24;

    private static DedicatedServerManager instance;
    public static DedicatedServerManager Instance { get { return instance; } }
    public static bool IsServerBuild { get { return instance != null && instance.isServer; } }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Check command line arguments
        if (autoDetectServerMode)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.ToLower() == "-server" || arg.ToLower() == "-dedicated")
                {
                    isServer = true;
                    Debug.Log("Server mode detected from command line arguments");
                    break;
                }
            }
        }

        if (isServer)
        {
            SetupServer();
        }
    }

    void SetupServer()
    {
        Debug.Log("=== DEDICATED SERVER MODE ENABLED ===");

        // Set target frame rate (servers don't need 60fps)
        Application.targetFrameRate = serverTargetFPS;

        // Disable VSync (not needed on server)
        QualitySettings.vSyncCount = 0;

        if (headlessMode)
        {
            // Disable rendering for headless mode
            Camera.main.enabled = false;

            // You could also disable all rendering
            // Application.targetFrameRate = 30;
            Debug.Log("Headless mode: Rendering disabled");
        }

        // Connect to Photon
        if (!PhotonNetwork.IsConnected)
        {
            if (useLocalPhotonServer)
            {
                Debug.Log($"Server connecting to LOCAL Photon Server at {serverAddress}:{serverPort}...");
                PhotonNetwork.PhotonServerSettings.AppSettings.Server = serverAddress;
                PhotonNetwork.PhotonServerSettings.AppSettings.Port = serverPort;
                PhotonNetwork.PhotonServerSettings.AppSettings.UseNameServer = false;
                PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = string.Empty;
            }
            else
            {
                Debug.Log("Server connecting to Photon Cloud...");
            }

            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        if (!isServer) return;

        Debug.Log("Server connected to Photon Master Server");

        // Create or join the server room
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayers,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.JoinOrCreateRoom(serverRoomName, roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        if (!isServer) return;

        Debug.Log($"Server joined room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"Server is Master Client: {PhotonNetwork.IsMasterClient}");

        // The server is now the master client and will handle bot spawning via BotSpawnManager
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!isServer) return;

        Debug.Log($"[SERVER] Player {newPlayer.NickName} joined. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!isServer) return;

        Debug.Log($"[SERVER] Player {otherPlayer.NickName} left. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!isServer) return;

        // If we're the server, we should always be master client
        // Try to reclaim master client status
        Debug.LogWarning("[SERVER] Master client switched! This shouldn't happen on dedicated server.");
    }

    void OnGUI()
    {
        if (!isServer) return;

        // Server info overlay
        GUI.color = Color.green;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.fontStyle = FontStyle.Bold;

        string status = PhotonNetwork.IsConnected ? "CONNECTED" : "CONNECTING...";
        string roomInfo = PhotonNetwork.InRoom ? $"Room: {PhotonNetwork.CurrentRoom.Name} | Players: {PhotonNetwork.CurrentRoom.PlayerCount}/{maxPlayers}" : "Not in room";

        GUI.Label(new Rect(10, 10, 500, 30), $"=== DEDICATED SERVER ({status}) ===", style);
        GUI.Label(new Rect(10, 35, 500, 25), roomInfo, style);
        GUI.Label(new Rect(10, 60, 500, 25), $"FPS: {(1f / Time.deltaTime):F0} | Master Client: {PhotonNetwork.IsMasterClient}", style);
    }

    void OnApplicationQuit()
    {
        if (isServer)
        {
            Debug.Log("Server shutting down...");
        }
    }
}
