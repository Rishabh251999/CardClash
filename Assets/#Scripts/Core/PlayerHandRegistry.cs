using System.Collections.Generic;
using System;

namespace CardClash
{
    /// <summary>
    /// Server-authoritative registry of connected players, their hands,
    /// and their last-drawn-card tracking. Pure C# — no Mirror/Unity
    /// dependency beyond the PlayerEntry/GameCard types, so lookups and
    /// mutations are centralized and unit-testable.
    /// </summary>
    public sealed class PlayerHandRegistry
    {
        private readonly Dictionary<uint, PlayerEntry> _players = new();
        private readonly Dictionary<uint, List<GameCard>> _hands = new();
        private readonly Dictionary<uint, byte?> _lastDrawnCardId = new();

        public IReadOnlyDictionary<uint, PlayerEntry> Players => _players;

        public int PlayerCount => _players.Count;

        public void AddPlayer(uint netId, PlayerEntry entry)
        {
            _players[netId] = entry;
            _hands[netId] = new List<GameCard>();
        }

        public void RemovePlayer(uint netId)
        {
            _players.Remove(netId);
            _hands.Remove(netId);
            _lastDrawnCardId.Remove(netId);
        }

        public void Clear()
        {
            _players.Clear();
            _hands.Clear();
            _lastDrawnCardId.Clear();
        }

        public bool TryGetEntry(uint netId, out PlayerEntry entry) =>
            _players.TryGetValue(netId, out entry);

        public IReadOnlyList<GameCard> GetHand(uint netId) =>
            _hands.TryGetValue(netId, out var hand) ? hand : Array.Empty<GameCard>();

        public int GetHandCount(uint netId) =>
            _hands.TryGetValue(netId, out var hand) ? hand.Count : 0;

        public void AddCardsToHand(uint netId, IEnumerable<GameCard> cards)
        {
            _hands[netId].AddRange(cards);
        }

        public bool TryRemoveCard(uint netId, GameCard card)
        {
            if (!_hands.TryGetValue(netId, out var hand))
                return false;

            var index = hand.FindIndex(c => c.Id == card.Id);

            if (index < 0)
                return false;

            hand.RemoveAt(index);
            return true;
        }

        public void SetLastDrawnCardId(uint netId, byte cardId)
        {
            _lastDrawnCardId[netId] = cardId;
        }

        public void ClearLastDrawnCardId(uint netId)
        {
            _lastDrawnCardId.Remove(netId);
        }

        public bool WasLastDrawnCard(uint netId, byte cardId)
        {
            return _lastDrawnCardId.TryGetValue(netId, out var id) && id == cardId;
        }

        public IEnumerable<KeyValuePair<uint, List<GameCard>>> AllHands => _hands;
    }
}