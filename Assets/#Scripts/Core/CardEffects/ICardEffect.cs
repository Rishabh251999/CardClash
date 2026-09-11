namespace CardClash
{
    /// <summary>
    /// Strategy interface for resolving what happens to turn order/draw
    /// state when a given card type is played.
    /// </summary>
    public interface ICardEffect
    {
        void Apply(ICardEffectContext ctx);
    }
}
