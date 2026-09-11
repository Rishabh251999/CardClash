namespace UNO
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
            {
                ctx.SkipNextPlayer();
            }

            ctx.AdvanceTurn();
        }
    }

    public sealed class DrawTwoCardEffect : ICardEffect
    {
        public void Apply(ICardEffectContext ctx)
        {
            ctx.ForcePlayerDraw(ctx.GetNextPlayerNetId(), 2);
            ctx.SkipNextPlayer();
            ctx.AdvanceTurn();
        }
    }

    public sealed class WildDrawFourCardEffect : ICardEffect
    {
        public void Apply(ICardEffectContext ctx)
        {
            ctx.ForcePlayerDraw(ctx.GetNextPlayerNetId(), 4);
            ctx.SkipNextPlayer();
            ctx.AdvanceTurn();
        }
    }
}
