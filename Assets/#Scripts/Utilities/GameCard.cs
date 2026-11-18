using System;

namespace CardClash
{
    #region Card

    public enum CardColor : byte
    {
        None = 0,
        Red = 1,
        Yellow = 2,
        Green = 3,
        Blue = 4,
    }

    public enum CardType : byte
    {
        Number = 0,
        Skip = 1,
        Reverse = 2,
        DrawTwo = 3,
        Wild = 4,
        WildDrawFour = 5
    }

    [Serializable]
    public struct GameCard
    {
        public byte Id;

        public CardColor Color;

        public CardType Type;

        public byte FaceValue;

        public override readonly string ToString()
        {
            string colorStr = Color == CardColor.None ? "" : $"{Color} ";
            string typeStr = Type == CardType.Number
                ? FaceValue.ToString()
                : TypeToString(Type);
            return $"{colorStr}{typeStr}";
        }

        private readonly string TypeToString(CardType type) => type switch
        {
            CardType.Skip => "Skip",
            CardType.Reverse => "Reverse",
            CardType.DrawTwo => "Draw",
            CardType.Wild => "Wild",
            CardType.WildDrawFour => "WildDrawFour",
            _ => "Unknown"
        };
    }

    #endregion
}
