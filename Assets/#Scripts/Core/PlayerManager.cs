using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Player registration/removal, the authoritative per-player hand registry,
    /// the client-side opponent GUI roster, and final-score calculation at
    /// game end. Wraps the existing <see cref="PlayerHandRegistry"/>.
    /// </summary>
    internal sealed class PlayerManager
    {
        private readonly CardGameController _controller;
        private readonly PlayerHandRegistry _registry = new();

        private readonly List<GamePlayerGUI> _playerGUIs = new();
        private readonly Dictionary<uint, int> _netIdToGuiIndex = new();

        internal PlayerManager(CardGameController controller)
        {
            _controller = controller;
        }

        internal PlayerHandRegistry Registry => _registry;

        internal void AddPlayer(NetworkConnectionToClient conn, PlayerRoomInfo info)
        {
            uint netId = conn.identity.netId;

            _registry.AddPlayer(netId, new PlayerEntry
            {
                Conn = conn,
                RoomInfo = info
            });

            _controller.TurnManager.AddPlayer(netId);

            _controller.PlayerData[netId] = new PlayerGameInfo
            {
                connectionId = netId,
                cardCount = 0,
                playerName = info.playerName,
                isOwner = info.isOwner,
                avatarId = info.avatarId,
            };

            _controller.PlayerOrder.Add(netId);
        }

        internal void UpdatePlayerCardCount(NetworkConnectionToClient conn, int newCount)
        {
            uint netId = conn.identity.netId;

            if (!_controller.PlayerData.ContainsKey(netId))
                return;

            var data = _controller.PlayerData[netId];

            data.cardCount = newCount;

            _controller.PlayerData[netId] = data;
        }

        internal GamePlayerGUI GetPlayerGUI(uint netId)
        {
            return _netIdToGuiIndex.TryGetValue(netId, out var index) && index >= 0 && index < _playerGUIs.Count
                ? _playerGUIs[index]
                : null;
        }

        internal void ClearGuiRoster()
        {
            foreach (var gui in _playerGUIs)
            {
                if (gui != null)
                    UnityEngine.Object.Destroy(gui.gameObject);
            }

            _playerGUIs.Clear();
            _netIdToGuiIndex.Clear();
        }

        internal void RemovePlayer(uint netId)
        {
            _registry.RemovePlayer(netId);
            _controller.PlayerData.Remove(netId);
            _netIdToGuiIndex.Remove(netId);
        }

        internal void Clear() => _registry.Clear();

        internal void HandlePlayerDataChanged(SyncIDictionary<uint, PlayerGameInfo>.Operation op, uint netId, PlayerGameInfo data)
        {
            var isSelf = NetworkClient.localPlayer is { netId: var selfNetId } && netId == selfNetId;

            if (op == SyncIDictionary<uint, PlayerGameInfo>.Operation.OP_REMOVE)
            {
                if (_netIdToGuiIndex.TryGetValue(netId, out int index))
                {
                    if (index >= 0 && index < _playerGUIs.Count)
                    {
                        UnityEngine.Object.Destroy(_playerGUIs[index].gameObject);
                        _playerGUIs.RemoveAt(index);

                        // Adjust indices for all remaining players that were after the removed one
                        var keys = _netIdToGuiIndex.Keys.ToList();
                        foreach (var key in keys)
                        {
                            if (key != netId && _netIdToGuiIndex[key] > index)
                            {
                                _netIdToGuiIndex[key]--;
                            }
                        }
                    }
                    _netIdToGuiIndex.Remove(netId);
                }
                return;
            }

            // Handle player addition
            if (!_netIdToGuiIndex.TryGetValue(netId, out int playerIndex))
            {
                var playerGUI = UnityEngine.Object.Instantiate(_controller.PlayerGUIPrefab, _controller.PlayerGUIsParent);
                _netIdToGuiIndex[netId] = _playerGUIs.Count;
                _playerGUIs.Add(playerGUI);

                if (_controller.PlayerData.TryGetValue(netId, out var currentData))
                {
                    playerGUI.UpdateCardCount(currentData.cardCount);
                    playerGUI.SetUserName(isSelf ? "You" : currentData.playerName);
                }

                var seatIndex = _controller.PlayerOrder.IndexOf(netId);
                if (seatIndex >= 0)
                    playerGUI.transform.SetSiblingIndex(seatIndex);

                return;
            }

            // Handle update to existing player
            if (playerIndex >= 0 && playerIndex < _playerGUIs.Count)
            {
                if (_controller.PlayerData.TryGetValue(netId, out var updatedData))
                {
                    _playerGUIs[playerIndex].UpdateCardCount(updatedData.cardCount);
                    _playerGUIs[playerIndex].SetUserName(isSelf ? "You" : updatedData.playerName);
                }
            }
        }

        internal List<GameEndPlayerInfo> CalculateFinalScores(out uint winnerNetId)
        {
            List<GameEndPlayerInfo> finalScores = new();

            winnerNetId = default;
            var lowestScore = int.MaxValue;

            foreach (var i in _registry.AllHands)
            {
                var winnerScore = 0;

                foreach (var j in i.Value)
                {
                    var points = j.Type switch
                    {
                        CardType.Number => 7,
                        CardType.Skip => 20,
                        CardType.Reverse => 20,
                        CardType.DrawTwo => 20,
                        CardType.Wild => 50,
                        CardType.WildDrawFour => 50,
                        _ => 0
                    };

                    winnerScore += points;
                }

                var playerName = _controller.PlayerData.TryGetValue(i.Key, out var info) ? info.playerName : "Unknown";
                var avatarId = _controller.PlayerData.TryGetValue(i.Key, out var avatarInfo) ? avatarInfo.avatarId : 0;

                finalScores.Add(new GameEndPlayerInfo
                {
                    PlayerName = playerName,
                    PlayerScore = winnerScore,
                    AvatarId = avatarId
                });

                if (winnerScore < lowestScore)
                {
                    lowestScore = winnerScore;
                    winnerNetId = i.Key;
                }
            }

            return finalScores;
        }
    }
}
