using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Server-authoritative turn progression: advancing/reversing/skipping turns,
    /// the per-turn countdown, forced/stacked draw resolution, and the client-side
    /// "is it my turn" query. Delegates SyncVar reads/writes and RPC calls back to
    /// the owning <see cref="CardGameController"/>, since Mirror requires those to
    /// be declared on the NetworkBehaviour itself.
    /// </summary>
    internal sealed class TurnManager
    {
        private readonly CardGameController _controller;
        private readonly CardTurnStateMachine _turnState = new();
        private readonly HashSet<uint> _playersWhoDrewThisTurn = new();

        private Coroutine _turnTimerCoroutine;

        internal TurnManager(CardGameController controller)
        {
            _controller = controller;
        }

        internal int PlayerCount => _turnState.PlayerCount;
        internal int TurnDirection => _turnState.TurnDirection;
        internal IReadOnlyList<uint> TurnOrder => _turnState.TurnOrder;
        internal uint CurrentPlayerNetIdFromState => _turnState.CurrentPlayerNetId;

        internal void AddPlayer(uint netId) => _turnState.AddPlayer(netId);

        /// <returns>True if the removed player was the current player.</returns>
        internal bool RemovePlayer(uint netId) => _turnState.RemovePlayer(netId);

        internal void Clear()
        {
            _turnState.Clear();
            _playersWhoDrewThisTurn.Clear();
        }

        internal void ResetTurnIndex() => _turnState.ResetTurnIndex();

        internal void ResetDrawTracking() => _playersWhoDrewThisTurn.Clear();

        internal void ClearPlayerDrewThisTurn(uint netId) => _playersWhoDrewThisTurn.Remove(netId);

        internal void StopTurnTimer()
        {
            if (_turnTimerCoroutine is { })
            {
                _controller.StopCoroutine(_turnTimerCoroutine);
                _turnTimerCoroutine = null;
            }
        }

        internal void SetNextTurn(uint netId)
        {
            _controller.CurrentPlayerNetId = netId;

            RestartTurnTimer();

            if (_controller.HasPendingDraw && !PlayerHasMatchingStackCard(netId))
            {
                ResolvePendingDraw(netId);
            }
        }

        internal bool PlayerHasMatchingStackCard(uint netId)
        {
            if (!_controller.HasPendingDraw)
                return true;

            if (!_controller.AllowStacking)
                return false;

            foreach (var card in _controller.PlayerManager.Registry.GetHand(netId))
            {
                if (card.Type == _controller.PendingDraw.Type)
                    return true;
            }

            return false;
        }

        internal void RestartTurnTimer()
        {
            StopTurnTimer();

            _controller.TurnStartTime = NetworkTime.time;

            _turnTimerCoroutine = _controller.StartCoroutine(TurnTimerRoutine(_controller.CurrentPlayerNetId));
        }

        private IEnumerator TurnTimerRoutine(uint netIdForThisTurn)
        {
            var endTime = NetworkTime.time + _controller.GameRules.TurnTimeLimit;

            while (NetworkTime.time < endTime)
                yield return null;

            if (_controller.MatchEnded || _controller.CurrentPlayerNetId != netIdForThisTurn)
                yield break;

            if (_controller.HasPendingDraw)
            {
                ResolvePendingDraw(netIdForThisTurn);
                yield break;
            }

            if (_playersWhoDrewThisTurn.Contains(netIdForThisTurn))
            {
                _playersWhoDrewThisTurn.Remove(netIdForThisTurn);
                AdvanceTurn();
                yield break;
            }

            ForcePlayerDraw(netIdForThisTurn, 1, true);
            AdvanceTurn();
        }

        internal void AdvanceTurn()
        {
            if (_controller.MatchEnded)
                return;

            _turnState.AdvanceTurn();

            SetNextTurn(_turnState.CurrentPlayerNetId);
        }

        internal void ReverseTurnDirection() => _turnState.ReverseDirection();

        internal void SkipNextPlayer()
        {
            var skippedNetId = _turnState.GetNextPlayerNetId();

            _turnState.SkipNextPlayer();

            var tint = _controller.ToUnityColor(_controller.SyncedTopDiscard.Color);

            _controller.StartCoroutine(InvokeAfterDelay(_controller.PlayMoveDuration,
                () => _controller.RpcPlaySkipEffect(skippedNetId, tint)));
        }

        private static IEnumerator InvokeAfterDelay(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        internal bool IsCurrentPlayer(NetworkConnectionToClient conn)
        {
            return conn.identity.netId == _controller.CurrentPlayerNetId;
        }

        internal uint GetNextPlayerNetId() => _turnState.GetNextPlayerNetId();

        internal bool IsValidPlayerAction(NetworkConnectionToClient conn)
        {
            if (conn?.identity == null)
                return false;

            return _controller.PlayerManager.Registry.Players.ContainsKey(conn.identity.netId)
                   && conn.identity.netId == _controller.CurrentPlayerNetId;
        }

        internal bool IsMyTurn()
        {
            return NetworkClient.localPlayer is { netId: var netId }
                   && _controller.CurrentPlayerNetId == netId;
        }

        internal void ForcePlayerDraw(uint targetNetId, int count, bool isPenalty = false)
        {
            if (!_controller.PlayerManager.Registry.Players.TryGetValue(targetNetId, out var entry))
            {
                Debug.LogWarning($"[Server] ForcePlayerDraw: no connection found for netId {targetNetId}.");

                return;
            }

            var drawn = new List<GameCard>();

            _controller.Deck.DrawMultiple(count, drawn);

            _controller.PlayerManager.Registry.AddCardsToHand(targetNetId, drawn);

            var data = _controller.PlayerData[targetNetId];

            data.cardCount += drawn.Count;

            if (data.cardCount != 1)
                ResetLastCardCall(targetNetId);

            _controller.PlayerData[targetNetId] = data;

            entry.Conn.Send(new ClientDeckMessage
            {
                clientDeckOperation = ClientDeckOperation.CardDrawn,

                Cards = drawn.ToArray(),

                DrawPileCount = _controller.Deck.DrawPileCount,

                CanPlayDrawnCard = false
            });

            if (isPenalty)
            {
                _controller.TargetPlayPenaltyDrawSound(entry.Conn);
            }

            _controller.RpcShowTopDiscard(_controller.SyncedTopDiscard, _controller.Deck.DrawPileCount);
        }

        internal void ResetLastCardCall(uint netId)
        {
            if (!_controller.PlayerData.TryGetValue(netId, out var data))
                return;

            if (!data.hasCalledLastCard)
                return;

            data.hasCalledLastCard = false;

            _controller.PlayerData[netId] = data;
        }

        internal void ResolvePendingDraw(uint netId)
        {
            var count = _controller.PendingDraw.Count;

            _controller.PendingDraw = default;

            ForcePlayerDraw(netId, count);

            _playersWhoDrewThisTurn.Remove(netId);
            _controller.PlayerManager.Registry.ClearLastDrawnCardId(netId);

            if (_controller.PlayerManager.Registry.Players.TryGetValue(netId, out var entry))
            {
                entry.Conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.StackedDraw,
                    DrawPileCount = _controller.Deck.DrawPileCount
                });
            }

            AdvanceTurn();
        }

        internal void AddPendingDraw(CardType type, int count)
        {
            _controller.PendingDraw = new()
            {
                Count = _controller.PendingDraw.Count + count,
                Type = type
            };
        }

        internal void HandleDrawCard(NetworkConnectionToClient conn)
        {
            if (!IsValidPlayerAction(conn))
                return;

            var netId = conn.identity.netId;

            if (_controller.HasPendingDraw)
            {
                ResolvePendingDraw(netId);
                return;
            }

            if (!_controller.Deck.TryDraw(out GameCard drawnCard))
            {
                Debug.LogWarning("[Server] Draw pile empty.");

                return;
            }

            _controller.PlayerManager.Registry.AddCardsToHand(netId, new[] { drawnCard });

            _playersWhoDrewThisTurn.Add(netId);

            var data = _controller.PlayerData[netId];

            data.cardCount++;

            _controller.PlayerData[netId] = data;

            if (data.cardCount != 1)
                ResetLastCardCall(netId);

            var canPlay = _controller.PlayHandler.IsPlayableAgainstTop(drawnCard);

            conn.Send(new ClientDeckMessage
            {
                clientDeckOperation = ClientDeckOperation.CardDrawn,

                Cards = new[] { drawnCard },

                DrawPileCount = _controller.Deck.DrawPileCount,

                CanPlayDrawnCard = canPlay
            });

            _controller.RpcShowTopDiscard(_controller.SyncedTopDiscard, _controller.Deck.DrawPileCount);

            if (canPlay)
            {
                _controller.PlayerManager.Registry.SetLastDrawnCardId(netId, drawnCard.Id);
            }
            else
            {
                _controller.PlayerManager.Registry.ClearLastDrawnCardId(netId);

                AdvanceTurn();
            }
        }

        internal void HandlePassTurn(NetworkConnectionToClient conn)
        {
            if (!IsValidPlayerAction(conn))
                return;

            if (!IsCurrentPlayer(conn))
                return;

            var netId = conn.identity.netId;

            _controller.PlayerManager.Registry.ClearLastDrawnCardId(netId);
            _playersWhoDrewThisTurn.Remove(netId);

            Debug.Log("[Server] Player passed after drawing.");

            AdvanceTurn();
        }
    }
}
