using Mirror;
using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    public class CardDeck
    {
        #region Runtime Collections

        private readonly List<GameCard> _drawPile = new(108);
        private readonly List<GameCard> _discardPile = new(108);

        #endregion

        #region Runtime State

        public int DrawPileCount => _drawPile.Count;
        public int DiscardPileCount => _discardPile.Count;
        public GameCard? TopDiscard => _discardPile.Count > 0 ? _discardPile[^1] : null;

        #endregion

        #region Unity Lifecycle

        private GameCard Make(ref byte id, CardColor color, CardType type, byte faceValue) =>
            new() { Id = id++, Color = color, Type = type, FaceValue = faceValue };

        #endregion

        #region Server Methods


        [Server]
        public void BuildDeck()
        {
            _drawPile.Clear();
            _discardPile.Clear();

            byte id = 0;

            CardColor[] colors = { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };

            foreach (var item in colors)
            {
                _drawPile.Add(Make(ref id, item, CardType.Number, 0));

                for (byte i = 1; i <= 9; i++)
                {
                    _drawPile.Add(Make(ref id, item, CardType.Number, i));
                    _drawPile.Add(Make(ref id, item, CardType.Number, i));
                }

                for (byte i = 0; i < 2; i++)
                {
                    _drawPile.Add(Make(ref id, item, CardType.Skip, 20));
                    _drawPile.Add(Make(ref id, item, CardType.Reverse, 20));
                    _drawPile.Add(Make(ref id, item, CardType.DrawTwo, 20));
                }
            }

            for (byte i = 0; i < 4; i++)
            {
                _drawPile.Add(Make(ref id, CardColor.None, CardType.Wild, 50));
                _drawPile.Add(Make(ref id, CardColor.None, CardType.WildDrawFour, 50));
            }

            Shuffle();
        }

        [Server]
        public void Shuffle()
        {
            int n = _drawPile.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                (_drawPile[n], _drawPile[k]) = (_drawPile[k], _drawPile[n]);
            }
        }

        [Server]
        public void ReturnToDraw(GameCard card) => _drawPile.Add(card);

        [Server]
        public int DrawMultiple(int count, List<GameCard> hand)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryDraw(out GameCard card)) break;
                hand.Add(card);
                drawn++;
            }
            return drawn;
        }

        [Server]
        public bool TryDraw(out GameCard card)
        {
            if (_drawPile.Count == 0)
                ReshuffleDiscardIntoDraw();

            if (_drawPile.Count == 0)
            {
                card = default;
                Debug.LogWarning("[CardDeck] Both piles empty — cannot draw.");
                return false;
            }

            card = _drawPile[0];
            _drawPile.RemoveAt(0);
            return true;
        }

        [Server]
        public void Discard(GameCard card) => _discardPile.Add(card);

        [Server]
        private void ReshuffleDiscardIntoDraw()
        {
            if (_discardPile.Count <= 1) return;

            GameCard top = _discardPile[^1];
            _discardPile.RemoveAt(_discardPile.Count - 1);

            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            _discardPile.Add(top);

            Shuffle();
            Debug.Log($"[CardDeck] Reshuffled {_drawPile.Count} cards from discard into draw pile.");
        }

        #endregion
    }
}
