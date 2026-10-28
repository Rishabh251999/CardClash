namespace CardClash
{
    public sealed class DefaultCardEffect : ICardEffect
    {
        public void Apply(ICardEffectContext ctx)
        {
            ctx.AdvanceTurn();
        }
    }

    public sealed class SkipCardEffect : ICardEffect
    {
        public void Apply(ICardEffectContext ctx)
        {
            ctx.SkipNextPlayer();
            ctx.AdvanceTurn();
        }
    }

    public sealed class ReverseCardEffect : ICardEffect
    {
        public void Apply(ICardEffectContext ctx)
        {
            ctx.ReverseTurnDirection();

            if (ctx.PlayerCount == 2)
                ctx.SkipNextPlayer();

            ctx.AdvanceTurn();
        }
    }

    public sealed class DrawTwoCardEffect : ICardEffect
    {
        private readonly int _penalty;
        public DrawTwoCardEffect(int penalty) => _penalty = penalty;

        public void Apply(ICardEffectContext ctx)
        {
            ctx.AddPendingDraw(CardType.DrawTwo, _penalty);
            ctx.AdvanceTurn();
        }
    }

    public sealed class WildDrawFourCardEffect : ICardEffect
    {
        private readonly int _penalty;
        public WildDrawFourCardEffect(int penalty) => _penalty = penalty;

        public void Apply(ICardEffectContext ctx)
        {
            ctx.AddPendingDraw(CardType.WildDrawFour, _penalty);
            ctx.AdvanceTurn();
        }
    }
}
