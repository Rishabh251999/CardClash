using System.Collections.Generic;
using UnityEngine;

namespace CardClash
{
    [CreateAssetMenu(fileName = "CardVisualDatabase", menuName = "CardClash/Card Visual Database")]
    public class CardVisualDatabaseSO : ScriptableObject
    {
        [Header("Number Cards (0-9 per color)")]
        [SerializeField] private List<NumberCardSprite> _numberCards = new();

        [Header("Action Cards (Skip / Reverse / DrawTwo per color)")]
        [SerializeField] private List<ActionCardSprite> _actionCards = new();

        [Header("Wild Cards (colorless)")]
        [SerializeField] private Sprite _wildSprite;
        [SerializeField] private Sprite _wildDrawFourSprite;

        private Dictionary<(CardColor, byte), Sprite> _numberLookup;
        private Dictionary<(CardColor, CardType), Sprite> _actionLookup;

        private void BuildLookups()
        {
            _numberLookup = new Dictionary<(CardColor, byte), Sprite>();
            foreach (var entry in _numberCards)
                _numberLookup[(entry.Color, entry.FaceValue)] = entry.Sprite;

            _actionLookup = new Dictionary<(CardColor, CardType), Sprite>();
            foreach (var entry in _actionCards)
                _actionLookup[(entry.Color, entry.Type)] = entry.Sprite;
        }

        public Sprite GetSprite(GameCard card)
        {
            if (_numberLookup == null || _actionLookup == null)
                BuildLookups();

            switch (card.Type)
            {
                case CardType.Number:
                    return _numberLookup.TryGetValue((card.Color, card.FaceValue), out var numSprite)
                        ? numSprite : null;

                case CardType.Skip:
                case CardType.Reverse:
                case CardType.DrawTwo:
                    return _actionLookup.TryGetValue((card.Color, card.Type), out var actionSprite)
                        ? actionSprite : null;

                case CardType.Wild:
                    return _wildSprite;

                case CardType.WildDrawFour:
                    return _wildDrawFourSprite;

                default:
                    return null;
            }
        }

        private void OnValidate()
        {
            _numberLookup = null;
            _actionLookup = null;
        }
    }
}