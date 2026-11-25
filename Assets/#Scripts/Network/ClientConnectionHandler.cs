using Mirror;
using System;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Handles client connection lifecycle: start/stop client, connect/disconnect handling,
    /// session ID creation and player identity messages.
    /// </summary>
    internal sealed class ClientConnectionHandler
    {
        private readonly ClientRoomRepository _clientRepo;
        private readonly LobbyManager _lobbyManager;
        private readonly ClientMessageHandler _messageHandler;
        private readonly SettingsSO _settings;

        public ClientConnectionHandler(ClientRoomRepository clientRepo, LobbyManager lobbyManager, ClientMessageHandler messageHandler, SettingsSO settings )
        {
            _clientRepo = clientRepo;
            _lobbyManager = lobbyManager;
            _messageHandler = messageHandler;
            _settings = settings;
        }

        [ClientCallback]
        internal void OnStartClient()
        {
            _clientRepo.Clear();

            _lobbyManager.UpdateRoomList(_clientRepo.OpenRooms);

            NetworkClient.RegisterHandler<ClientRoomMessage>(_messageHandler.OnClientRoomMessage);
            NetworkClient.RegisterHandler<ClientDeckMessage>(_messageHandler.OnClientDeckMessage);
        }

        [ClientCallback]
        internal void OnClientConnect()
        {
            var username = _settings.UserName;

            var sessionId = PlayerPrefs.GetString("SessionId", string.Empty);
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("SessionId", sessionId);
            }

            NetworkClient.Send(new 
                SetPlayerInfoMessage { Username = username, SessionId = sessionId });
        }

        [ClientCallback]
        internal void OnClientDisconnect()
        {
            _clientRepo.Clear();
        }

        [ClientCallback]
        internal void OnStopClient()
        {
            _clientRepo.Clear();
        }
    }
}
