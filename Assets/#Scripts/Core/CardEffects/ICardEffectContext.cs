namespace CardClash
{
    /// <summary>
    /// Minimal surface a card effect needs to manipulate turn order and
    /// force draws. Implemented by CardGameController so effects don't need
    /// to know about Mirror/NetworkBehaviour internals.
    /// </summary>
    public interface ICardEffectContext
    {
        int PlayerCount { get; }

        void ReverseTurnDirection();

        void SkipNextPlayer();

        void AdvanceTurn();

        uint GetNextPlayerNetId();

        void ForcePlayerDraw(uint targetNetId, int count);
    }
}
