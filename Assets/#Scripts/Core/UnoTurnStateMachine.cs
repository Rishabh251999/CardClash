using System.Collections.Generic;

namespace UNO
{
    public sealed class UnoTurnStateMachine
    {
        private readonly List<uint> _turnOrder = new();

        private int _turnIndex;
        private int _turnDirection = 1;

        public IReadOnlyList<uint> TurnOrder => _turnOrder;

        public int PlayerCount => _turnOrder.Count;

        public uint CurrentPlayerNetId => _turnOrder[_turnIndex];

        public void AddPlayer(uint netId)
        {
            _turnOrder.Add(netId);
        }

        /// <returns>True if the removed player was the current player.</returns>
        public bool RemovePlayer(uint netId)
        {
            var wasCurrentPlayer = PlayerCount > 0 && CurrentPlayerNetId == netId;

            _turnOrder.Remove(netId);

            _turnIndex = _turnOrder.Count > 0 ? _turnIndex % _turnOrder.Count : 0;

            return wasCurrentPlayer;
        }

        public void Clear()
        {
            _turnOrder.Clear();
            _turnIndex = 0;
            _turnDirection = 1;
        }

        public void ReverseDirection()
        {
            _turnDirection *= -1;
        }

        public void SkipNextPlayer()
        {
            _turnIndex = NextIndex();
        }

        public void AdvanceTurn()
        {
            _turnIndex = NextIndex();
        }

        public uint GetNextPlayerNetId()
        {
            return _turnOrder[NextIndex()];
        }

        public bool IsCurrentPlayer(uint netId)
        {
            return PlayerCount > 0 && CurrentPlayerNetId == netId;
        }

        private int NextIndex()
        {
            return (_turnIndex + _turnDirection + _turnOrder.Count) % _turnOrder.Count;
        }
    }
}