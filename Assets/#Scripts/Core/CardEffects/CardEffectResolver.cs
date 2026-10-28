using System.Collections.Generic;

namespace CardClash
{
    /// <summary>
    /// Maps CardType to its ICardEffect strategy. Falls back to
    /// DefaultCardEffect (simple advance-turn) for unmapped types.
    /// </summary>
    public sealed class CardEffectResolver
    {
        private readonly ICardEffect Default = new DefaultCardEffect();

        private readonly Dictionary<CardType, ICardEffect> _effects;

        public CardEffectResolver()
        {
            _effects = new Dictionary<CardType, ICardEffect>
            {
                { CardType.Skip, new SkipCardEffect() },
                { CardType.Reverse, new ReverseCardEffect() },
                { CardType.DrawTwo, new DrawTwoCardEffect(2) },
                { CardType.WildDrawFour, new WildDrawFourCardEffect(4) },
            };
        }

        public ICardEffect Resolve(CardType type) =>
            _effects.TryGetValue(type, out var effect) ? effect : Default;
    }
}