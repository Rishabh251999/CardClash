using Mirror;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace CardClash
{
    public class GameManager : MonoBehaviour
    {
        #region constants/readonly

        #endregion

        #region Script References

        [Header("Script References")]
        [SerializeField] private LobbyManager _lobbyManager;
        [SerializeField] private RoomManager _roomManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private DeckRulesSO _deckRules;

        #endregion

        #region GUI References

        [Space(5f)]

        [Header("GUI References")]
        [SerializeField] private CardGameController matchControllerPrefab;

        #endregion

        #region Runtime State

        private RoomService _roomService;
        private MatchService _matchService;
        private readonly ServerRoomRepository _serverRepo = new();
        private readonly ClientRoomRepository _clientRepo = new();
        internal ClientRoomRepository ClientRoomRepository => _clientRepo;

        private PlayerReconnectionService _reconnectService;

        private ServerConnectionHandler _serverConnectionHandler;
        private ServerMessageHandler _serverMessageHandler;
        private ClientConnectionHandler _clientConnectionHandler;
        private ClientMessageHandler _clientMessageHandler;
        private RoomNavigationController _roomNavigationController;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetMaxRefreshRate();

            _roomService = new(_serverRepo, () => SendRoomList());

            _matchService = new(_serverRepo, matchControllerPrefab, _deckRules, () => SendRoomList());

            _reconnectService = new(_serverRepo, this, () => SendRoomList());

            _lobbyManager.Initialize(_clientRepo);

            _serverMessageHandler = new(_serverRepo, _roomService, _matchService, SendRoomList);
            _serverConnectionHandler = new(_serverRepo, _reconnectService, _roomService, _matchService,
                _serverMessageHandler, () => SendRoomList());

            _roomNavigationController = new(_clientRepo, _uiManager, _lobbyManager);
            _clientMessageHandler = new(_clientRepo, _roomManager, _uiManager, _lobbyManager, _roomNavigationController);
            _clientConnectionHandler = new(_clientRepo, _lobbyManager, _clientMessageHandler);
        }

        private void SetMaxRefreshRate()
        {
            var maxRate = Screen.resolutions.Length > 0 ? Screen.resolutions[^1].refreshRateRatio.value
                : Screen.currentResolution.refreshRateRatio.value;

            Application.targetFrameRate = Mathf.Min(60, Mathf.CeilToInt((float)maxRate));

            // Disable VSync so targetFrameRate actually takes effect
            QualitySettings.vSyncCount = 0;
        }

        #endregion

        #region Server Callbacks

        [ServerCallback]
        internal void OnStartServer() => _serverConnectionHandler.OnStartServer();

        [ServerCallback]
        internal void OnServerReady(NetworkConnectionToClient conn) => _serverConnectionHandler.OnServerReady(conn);

        [ServerCallback]
        internal IEnumerator OnServerDisconnect(NetworkConnectionToClient conn) => _serverConnectionHandler.OnServerDisconnect(conn);

        [ServerCallback]
        internal void OnStopServer() => _serverConnectionHandler.OnStopServer();

        #endregion

        #region Client Callbacks

        [ClientCallback]
        internal void OnStartClient() => _clientConnectionHandler.OnStartClient();

        [ClientCallback]
        internal void OnClientConnect() => _clientConnectionHandler.OnClientConnect();

        [ClientCallback]
        internal void OnClientDisconnect() => _clientConnectionHandler.OnClientDisconnect();

        [ClientCallback]
        internal void OnStopClient() => _clientConnectionHandler.OnStopClient();

        #endregion

        #region Button Callbacks (UI)

        [ClientCallback]
        public void SelectRoom(Guid roomId) => _roomNavigationController.SelectRoom(roomId);

        #endregion

        #region UI Update Methods

        public void OnRoomLeft() => _roomNavigationController.OnRoomLeft();

        #endregion

        #region Helper Methods

        [ServerCallback]
        void SendRoomList(NetworkConnectionToClient conn = null)
        {
            if (conn != null)
            {
                conn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.ListUpdated,
                    roomInfo = _serverRepo.OpenRooms.Values.ToArray()
                });
            }

            else
            {
                foreach (var item in _serverRepo.WaitingConnections)
                {
                    item.Send(new ClientRoomMessage
                    {
                        clientRoomOperation = ClientRoomOperation.ListUpdated,
                        roomInfo = _serverRepo.OpenRooms.Values.ToArray()
                    });
                }
            }
        }

        #endregion
    }
}
