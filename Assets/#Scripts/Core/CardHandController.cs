using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    /// <summary>
    /// Client-side management of the local player's hand view: dealing/removing
    /// card views, single and multi-card selection, and interactability refresh
    /// in response to turn/state changes.
    /// </summary>
    internal sealed class CardHandController
    {
        private readonly CardGameController _controller;

        private readonly List<Card> _handCards = new();
        private readonly List<Card> _selectedCards = new();

        private readonly WaitForSeconds _waitForSeconds0_12 = new(0.12f);

        internal CardHandController(CardGameController controller)
        {
            _controller = controller;
        }

        internal bool AwaitingDrawnCardDecision { get; set; }

        #region Multi-Select

        internal void ToggleCardSelection(Card card)
        {
            if (!_controller.PlaySameNumberEnabled || !_controller.TurnManager.IsMyTurn()) return;
            if (card.CardData.Type != CardType.Number) return;

            if (_selectedCards.Remove(card))
            {
                card.SetSelectedIndex(0);
                RenumberSelectedCards();
            }
            else
            {
                var faceValue = card.CardData.FaceValue;

                if (_selectedCards.Count > 0 &&
                    _selectedCards[0].CardData.FaceValue != faceValue)
                    return;

                // only the first card in the stack must be legally playable against the top discard
                if (_selectedCards.Count == 0 && !_controller.PlayHandler.IsValidPlay(card.CardData))
                    return;

                if (_controller.MaxCardsPerTurn < 2 || _selectedCards.Count >= _controller.MaxCardsPerTurn)
                    return;

                if (!_handCards.Any(other =>
                        other != null && other != card &&
                        other.CardData.Type == CardType.Number &&
                        other.CardData.FaceValue == faceValue))
                    return;

                _selectedCards.Add(card);
                card.SetSelectedIndex(_selectedCards.Count);
            }

            _controller.GameButtonManager.SetPlaySelectedButtonState(_selectedCards.Count);
            _controller.HandButtonsGameObject.SetActive(_selectedCards.Count >= 2);

            RefreshLastCardButtonInteractable();
        }

        private void RenumberSelectedCards()
        {
            for (int i = 0; i < _selectedCards.Count; i++)
                _selectedCards[i].SetSelectedIndex(i + 1);
        }

        internal void ClearCardSelection()
        {
            foreach (var card in _selectedCards)
                if (card != null) card.SetSelectedIndex(0);

            _selectedCards.Clear();
            _controller.GameButtonManager.SetPlaySelectedButtonState(0);
            _controller.HandButtonsGameObject.SetActive(false);
        }

        internal void OnPlaySelectedButtonClicked()
        {
            if (_selectedCards.Count < 2) return;

            var cards = _selectedCards.Select(c => c.CardData).ToArray();

            foreach (var card in _selectedCards)
            {
                card.SetSelectedIndex(0);
                card.BeginReturnWatch();
                card.PlayTowards(_controller.CardTargetTransform, _controller._canvas.transform);
            }

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.PlayMultipleCards,
                Cards = cards,
                chosenWildColor = CardColor.None
            });

            _selectedCards.Clear();
            _controller.HandButtonsGameObject.SetActive(false);
        }

        #endregion


        #region Card Display

        internal void ShowDealtCards(GameCard[] cards, bool applyStartDelay = false)
        {
            _controller.StartCoroutine(ShowDealtCardsStaggered(cards, applyStartDelay ? 0.5f : 0f));
        }

        private IEnumerator ShowDealtCardsStaggered(GameCard[] cards, float startDelay)
        {
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            var views = new List<Card>();

            foreach (var card in cards)
            {
                var cardView = Object.Instantiate(_controller.CardPrefab, _controller.HandContainer);

                cardView.Setup(card);

                _handCards.Add(cardView);

                views.Add(cardView);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_controller.HandContainer);

            foreach (var cardView in views)
            {
                cardView.CaptureDealTarget();
            }

            foreach (var cardView in views)
            {
                cardView.PrepareDeal(
                    _controller.CardDrawGameObject.transform
                );
            }

            foreach (var cardView in views)
            {
                cardView.AnimateDeal();

                yield return _waitForSeconds0_12;
            }

            RefreshHandInteractability(_controller.TurnManager.IsMyTurn());
            RefreshLastCardButtonInteractable();
        }

        internal void OnDrawnCardReceived(bool canPlay, GameCard drawnCard)
        {
            if (!canPlay)
            {
                RefreshHandInteractability(false);
                RefreshLastCardButtonInteractable();

                return;
            }

            AwaitingDrawnCardDecision = true;

            _controller.GameButtonManager.SetPassButtonInteractable(true);

            foreach (var cardView in _handCards)
            {
                if (!cardView.TryGetComponent<Card>(out var card))
                {
                    continue;
                }

                var isDrawnCard = card.CardData.Id == drawnCard.Id;

                var isPlayable = _controller.PlayHandler.IsValidPlay(card.CardData);

                card.SetInteractable(isDrawnCard && isPlayable);
            }

            RefreshLastCardButtonInteractable();
        }

        internal void RemoveHandCard(uint cardId)
        {
            for (int i = 0; i < _handCards.Count; i++)
            {
                if (_handCards[i].CardData.Id != cardId)
                    continue;

                _handCards.RemoveAt(i);
                ClearCardSelection();
                break;
            }

            RefreshLastCardButtonInteractable();
            RefreshMultiSelectEligibility();
        }

        internal void ClearForRestart()
        {
            foreach (var card in _handCards)
            {
                if (card != null)
                    Object.Destroy(card.gameObject);
            }

            _handCards.Clear();
        }

        #endregion


        #region Interactability

        internal void RefreshHandInteractability(bool isMyTurn)
        {
            foreach (var card in _handCards)
            {
                var canPlay = isMyTurn && _controller.PlayHandler.IsValidPlay(card.CardData);
                card.SetInteractable(canPlay);
                card.SetHighlight(!isMyTurn
                    ? Card.HighlightState.Normal
                    : canPlay
                        ? Card.HighlightState.Playable
                        : Card.HighlightState.Disabled);
            }

            RefreshMultiSelectEligibility();
        }

        private void RefreshMultiSelectEligibility()
        {
            if (!_controller.PlaySameNumberEnabled)
            {
                foreach (var card in _handCards)
                    card.SetMultiSelectable(false);
                return;
            }

            var counts = new Dictionary<int, int>();

            foreach (var card in _handCards)
            {
                if (card.CardData.Type != CardType.Number) continue;
                counts.TryGetValue(card.CardData.FaceValue, out var count);
                counts[card.CardData.FaceValue] = count + 1;
            }

            foreach (var card in _handCards)
            {
                var eligible = card.CardData.Type == CardType.Number &&
                               counts.TryGetValue(card.CardData.FaceValue, out var count) &&
                               count >= 2;

                card.SetMultiSelectable(eligible);
            }
        }

        internal void RefreshLastCardButtonInteractable()
        {
            if (_controller.LastCardButton == null)
                return;

            var projectedCount = _handCards.Count - _selectedCards.Count;

            var canReachLastCard = projectedCount <= _controller.GameRules.LastCardCallThreshold &&
                _handCards.Exists(card => _controller.PlayHandler.IsValidPlay(card.CardData));
            var lastCard = _controller.TurnManager.IsMyTurn() && canReachLastCard && !HasLocallyCalledLastCard();

            _controller.GameButtonManager.SetCallOutButtonInteractable(lastCard);
        }

        private bool HasLocallyCalledLastCard()
        {
            if (NetworkClient.localPlayer is not { netId: var selfNetId })
                return false;

            return _controller.PlayerData.TryGetValue(selfNetId, out var data) && data.hasCalledLastCard;
        }

        #endregion
    }
}
