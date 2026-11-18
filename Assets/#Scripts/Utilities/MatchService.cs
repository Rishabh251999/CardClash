using Mirror;
using System;
using System.Linq;
using UnityEngine;

namespace CardClash
{
    internal sealed class MatchService
    {
        private readonly ServerRoomRepository _repo;
        private readonly CardGameController _matchControllerPrefab;
        private readonly Action _sendRoomList;
        private readonly DeckRulesSO _deckRulesSO;

        public MatchService(ServerRoomRepository repo, CardGameController matchControllerPrefab, DeckRulesSO deckRulesSO, Action sendRoomList)
        {
            _repo = repo;
            _matchControllerPrefab = matchControllerPrefab;
            _deckRulesSO = deckRulesSO;
            _sendRoomList = sendRoomList;
        }

        #region Start Match

        internal void StartMatch(NetworkConnectionToClient conn)
        {
            if (!NetworkServer.active || conn == null)
                return;

            // Only the room owner can start a match.
            if (!_repo.PlayerInfos.TryGetValue(conn, out var ownerInfo) ||
                !ownerInfo.isOwner)
                return;

            if (!_repo.PlayerRooms.TryGetValue(conn, out var roomCode))
                return;

            if (!_repo.OpenRooms.TryGetValue(roomCode, out var roomInfo) ||
                !_repo.RoomConnections.TryGetValue(
                    roomCode, out var connections))
                return;

            var players = connections.ToArray();

            // Validate room state before spawning network objects.
            if (players.Length < 2 ||
                players.Length > roomInfo.maxPlayers)
                return;

            if (players.Any(player =>
                    player == null ||
                    !_repo.PlayerInfos.TryGetValue(
                        player, out var info) ||
                    info.roomCode != roomCode))
            {
                Debug.LogWarning(
                    $"[Server] Invalid player data in room {roomCode}.");
                return;
            }

            if (players.Any(player =>
                    !_repo.PlayerInfos[player].isReady))
            {
                Debug.LogWarning(
                    $"[Server] Cannot start room {roomCode}: " +
                    "not all players are ready.");
                return;
            }

            if (_matchControllerPrefab == null ||
                NetworkManager.singleton == null ||
                NetworkManager.singleton.playerPrefab == null)
            {
                Debug.LogError(
                    "[Server] Match controller or player prefab is missing.");
                return;
            }

            // Validate required NetworkMatch components before spawning.
            if (!_matchControllerPrefab.TryGetComponent<NetworkMatch>(
                    out _))
            {
                Debug.LogError(
                    "[Server] Match controller prefab requires NetworkMatch.");
                return;
            }

            if (!NetworkManager.singleton.playerPrefab.TryGetComponent<NetworkMatch>(
                    out _))
            {
                Debug.LogError(
                    "[Server] Player prefab requires NetworkMatch.");
                return;
            }

            CardGameController matchController = null;

            try
            {
                // Create the match controller.
                matchController = UnityEngine.Object.Instantiate(
                    _matchControllerPrefab);

                matchController.GetComponent<NetworkMatch>().matchId =
                    roomCode;

                NetworkServer.Spawn(matchController.gameObject);

                _repo.MatchControllers.Add(
                    roomCode, matchController);

                // Build the deck for this match.
                CardDeck deck = new();
                deck.BuildDeck(_deckRulesSO);

                _repo.RoomDecks.Add(roomCode, deck);

                // Create a match player for every connection.
                foreach (var playerConn in players)
                {
                    var player = UnityEngine.Object.Instantiate(NetworkManager.singleton.playerPrefab);

                    player.GetComponent<NetworkMatch>().matchId = roomCode;

                    if (playerConn.identity != null)
                    {
                        NetworkServer.ReplacePlayerForConnection(
                            playerConn,
                            player,
                            ReplacePlayerOptions.Destroy);
                    }
                    else
                    {
                        NetworkServer.AddPlayerForConnection(
                            playerConn,
                            player);
                    }

                    matchController.AddPlayer(
                        playerConn,
                        _repo.PlayerInfos[playerConn]);

                    var info = _repo.PlayerInfos[playerConn];
                    info.isReady = false;
                    _repo.PlayerInfos[playerConn] = info;

                    playerConn.Send(new ClientRoomMessage
                    {
                        clientRoomOperation = ClientRoomOperation.Started,
                        roomCode = roomCode
                    });
                }

                // Start gameplay after registering every player.
                matchController.StartGame(deck, roomInfo.startingCards, roomInfo.allowStacking, 
                    roomInfo.noPowerCardFinish, roomInfo.playSameNumber, roomInfo.maxCardsPerTurn);

                // Remove all waiting-room mappings.
                foreach (var playerConn in players)
                    _repo.PlayerRooms.Remove(playerConn);

                _repo.OpenRooms.Remove(roomCode);
                _repo.RoomConnections.Remove(roomCode);

                _sendRoomList?.Invoke();

                Debug.Log(
                    $"[Server] Match started for room {roomCode}.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);

                // Avoid retaining a partially registered match.
                _repo.MatchControllers.Remove(roomCode);
                _repo.RoomDecks.Remove(roomCode);

                if (matchController != null)
                {
                    matchController.EndMatch();

                    if (matchController.gameObject != null)
                        NetworkServer.Destroy(matchController.gameObject);
                }

                // Do not remove the waiting-room records here:
                // they may be needed to recover from a failed startup.
            }
        }

        #endregion

        #region Quit Match

        internal void QuitMatch(NetworkConnectionToClient conn, CardGameController controller)
        {
            if (!NetworkServer.active ||
                conn == null ||
                controller == null)
                return;

            if (!_repo.PlayerInfos.TryGetValue(
                    conn, out var playerInfo))
                return;

            Debug.Log(
                $"[Server] Player quitting match. " +
                $"Connection={conn.connectionId}, " +
                $"Owner={playerInfo.isOwner}");

            // Owner quitting ends the match for everyone.
            if (playerInfo.isOwner)
            {
                EndMatch(controller);
                return;
            }

            // Remove the player from the active game.
            controller.HandlePlayerQuit(conn);

            playerInfo.isReady = false;
            playerInfo.roomCode = System.Guid.Empty;
            _repo.PlayerInfos[conn] = playerInfo;

            _repo.PlayerRooms.Remove(conn);

            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Left
            });

            _sendRoomList?.Invoke();
        }

        #endregion

        #region End Match

        internal void EndMatch(CardGameController controller)
        {
            if (!NetworkServer.active || controller == null)
                return;

            if (!controller.TryGetComponent<NetworkMatch>(
                    out var networkMatch))
            {
                Debug.LogWarning(
                    "[Server] Cannot end match: NetworkMatch is missing.");
                return;
            }

            var roomCode = networkMatch.matchId;

            Debug.Log($"[Server] Ending match for room {roomCode}.");

            controller.EndMatch();

            _repo.MatchControllers.Remove(roomCode);
            _repo.RoomDecks.Remove(roomCode);

            if (controller != null)
                NetworkServer.Destroy(controller.gameObject);

            _sendRoomList?.Invoke();
        }

        #endregion
    }
}
