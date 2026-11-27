using Mirror;
using System.Linq;
using UnityEngine;

namespace CardClash
{
    /// <summary>
    /// Server-side validation and resolution of single and multi-card plays:
    /// legality against the discard/stack, card ownership, wild-color handling,
    /// the "last card" call bookkeeping, and the house rule for stacked
    /// same-number plays.
    /// </summary>
    internal sealed class CardPlayHandler
    {
        private readonly CardGameController _controller;

        internal CardPlayHandler(CardGameController controller)
        {
            _controller = controller;
        }

        internal void HandlePlayerCard(NetworkConnectionToClient conn, GameCard card, CardColor chosenWildColor, bool applyEffect = true)
        {
            if (_controller.MatchEnded)
            {
                return;
            }

            if (!_controller.TurnManager.IsValidPlayerAction(conn))
            {
                return;
            }

            var netId = conn.identity.netId;

            if (!IsPlayableAgainstTop(card))
            {
                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,

                    errorMessage = $"Illegal play: {card} does not match " + $"the current discard/stack requirement."
                });

                return;
            }

            if (_controller.NoPowerCardFinish && _controller.PlayerData[netId].cardCount == 1 && IsPowerCard(card.Type))
            {
                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,

                    errorMessage = "House Rule: You cannot finish " + "the game with a Wild card."
                });

                return;
            }

            if (card.Type is CardType.Wild or CardType.WildDrawFour)
            {
                if (chosenWildColor is CardColor.None)
                {
                    conn.Send(new ClientDeckMessage
                    {
                        clientDeckOperation = ClientDeckOperation.Error,

                        errorMessage = "You must choose a color " + "for the Wild card."
                    });

                    return;
                }

                card.Color = chosenWildColor;

                _controller.ShowNotification(chosenWildColor switch
                {
                    CardColor.Red => NotificationType.Red,
                    CardColor.Blue => NotificationType.Blue,
                    CardColor.Green => NotificationType.Green,
                    CardColor.Yellow => NotificationType.Yellow,
                    _ => NotificationType.LastCard
                }, "Color changes to: " + chosenWildColor.ToString());

                AudioManager.Instance.PlaySoundEffect("color_changed");
                VibrationUtility.Vibrate(milliseconds: 250, amplitude: 255);
            }

            _controller.TurnManager.StopTurnTimer();

            if (!TryRemoveFromHand(netId, card))
            {
                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,

                    errorMessage = $"Illegal play: {card} " + $"is not in your hand."
                });

                return;
            }

            _controller.Deck.Discard(card);

            _controller.SetTopDiscard(card, _controller.Deck.DrawPileCount);

            var data = _controller.PlayerData[netId];

            data.cardCount -= 1;

            var wasDrawnCard = _controller.PlayerManager.Registry.WasLastDrawnCard(netId, card.Id);

            _controller.PlayerManager.Registry.ClearLastDrawnCardId(netId);

            _controller.TurnManager.ClearPlayerDrewThisTurn(netId);

            if (wasDrawnCard && data.cardCount == 1)
            {
                data.hasCalledLastCard = true;
            }

            _controller.PlayerData[netId] = data;

            conn.Send(new ClientDeckMessage
            {
                clientDeckOperation = ClientDeckOperation.CardPlayed,

                TopDiscardCard = card,

                DrawPileCount = _controller.Deck.DrawPileCount
            });


            if (data.cardCount <= 0)
            {
                _controller.TriggerPlayerWin();
                return;
            }

            if (data.cardCount == 1)
            {
                if (!data.hasCalledLastCard)
                {
                    PenalizeMissedLastCardCall(netId);
                }

                else if (wasDrawnCard)
                {
                    _controller.ShowNotification(NotificationType.LastCard, "LAST CARD! " + data.playerName);
                }
            }

            if (!_controller.MatchEnded && applyEffect)
                _controller.CardEffects.Resolve(card.Type).Apply(_controller);

            _controller.RpcPlayDiscardEffect(card, _controller.TurnManager.TurnDirection);
        }

        internal void HandlePlayerCards(NetworkConnectionToClient conn, GameCard[] cards, CardColor chosenWildColor)
        {
            if (!_controller.PlaySameNumberEnabled || cards == null || cards.Length == 0)
                return;

            if (_controller.MatchEnded || !_controller.TurnManager.IsValidPlayerAction(conn))
                return;

            // House rule validation: all cards must share the same number, and the
            // first card must be legally playable against the current top/stack.
            if (cards.Length > _controller.MaxCardsPerTurn)
            {
                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,
                    errorMessage = $"House Rule: You can only play up to {_controller.MaxCardsPerTurn} cards at once."
                });
                return;
            }

            var firstCard = cards[0];

            if (firstCard.Type != CardType.Number ||
                cards.Any(c => c.Type != CardType.Number || c.FaceValue != firstCard.FaceValue))
            {
                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,
                    errorMessage = "House Rule: All stacked cards must be the same number."
                });
                return;
            }

            if (!IsPlayableAgainstTop(firstCard))
            {
                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,
                    errorMessage = $"Illegal play: {firstCard} does not match the current discard requirement."
                });
                return;
            }

            var netId = conn.identity.netId;

            // Play each card sequentially through the existing single-card pipeline,
            // so all effects (Skip/Reverse/DrawTwo/Wild color, win/last-card checks) apply.
            for (int i = 0; i < cards.Length; i++)
            {
                if (_controller.MatchEnded) break;
                HandlePlayerCard(conn, cards[i], chosenWildColor, applyEffect: i == cards.Length - 1);
            }
        }

        internal bool TryRemoveFromHand(uint netId, GameCard card)
        {
            return _controller.PlayerManager.Registry.TryRemoveCard(netId, card);
        }

        internal bool IsPlayableAgainstTop(GameCard card)
        {
            if (_controller.PendingDraw.Count > 0)
                return _controller.AllowStacking && card.Type == _controller.PendingDraw.Type;

            if (_controller.Deck.TopDiscard is not { } topCard)
                return true;

            return card.Type switch
            {
                CardType.Wild or CardType.WildDrawFour => true,

                _ when card.Color == topCard.Color => true,

                CardType.Number when topCard.Type is CardType.Number
                    && card.FaceValue == topCard.FaceValue => true,

                not CardType.Number when card.Type == topCard.Type => true,

                _ => false
            };
        }

        internal bool IsValidPlay(GameCard card)
        {
            if (_controller.PendingDraw.Count > 0)
                return _controller.AllowStacking && card.Type == _controller.PendingDraw.Type;

            if (card.Type is CardType.Wild || card.Type is CardType.WildDrawFour)
                return true;

            if (card.Color == _controller.SyncedTopDiscard.Color)
                return true;

            if (card.Type is CardType.Number && _controller.SyncedTopDiscard.Type is CardType.Number)
                return card.FaceValue == _controller.SyncedTopDiscard.FaceValue;

            return card.Type == _controller.SyncedTopDiscard.Type;
        }

        internal void HandleLastCardCall(NetworkConnectionToClient conn)
        {
            if (!_controller.TurnManager.IsValidPlayerAction(conn))
                return;

            var netId = conn.identity.netId;

            if (!_controller.PlayerData.TryGetValue(netId, out var data))
                return;

            if (data.hasCalledLastCard)
                return;

            data.hasCalledLastCard = true;

            _controller.PlayerData[netId] = data;

            foreach (var entry in _controller.PlayerManager.Registry.Players.Values)
            {
                entry.Conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.LastCardCalled
                });
            }

            _controller.ShowNotification(NotificationType.LastCard, "LAST CARD! " + data.playerName);
        }

        private void PenalizeMissedLastCardCall(uint netId)
        {
            _controller.TurnManager.ForcePlayerDraw(netId, _controller.GameRules.MissedLastCardPenalty, true);

            _controller.TurnManager.ResetLastCardCall(netId);
        }

        private bool IsPowerCard(CardType type) =>
            type is CardType.Wild or CardType.WildDrawFour;
    }
}
