using System.Collections.Generic;

namespace UNO
{
    /// <summary>
    /// Maps CardType to its ICardEffect strategy. Falls back to
    /// DefaultCardEffect (simple advance-turn) for unmapped types.
    /// </summary>
    public sealed class CardEffectResolver
    {
        private static readonly ICardEffect Default = new DefaultCardEffect();

        private readonly Dictionary<CardType, ICardEffect> _effects = new()
        {
            { CardType.Reverse, new ReverseCardEffect() },
            { CardType.Skip, new SkipCardEffect() },
            { CardType.DrawTwo, new DrawTwoCardEffect() },
            { CardType.WildDrawFour, new WildDrawFourCardEffect() },
        };

        public ICardEffect Resolve(CardType type)
        {
            return _effects.TryGetValue(type, out var effect) ? effect : Default;
        }
    }
}
