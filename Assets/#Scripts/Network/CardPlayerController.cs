using Mirror;

namespace CardClash
{
    public class CardPlayerController : NetworkBehaviour
    {
        #region Unity Lifecycle

        public void TryPlayCard(Card card)
        {
            if (!isLocalPlayer)
                return;

            if (!CardGameController.Instance.IsMyTurn())
                return;

            if (card.CardData.Type is CardType.Wild or CardType.WildDrawFour)
            {
                card.SetInteractable(false); // prevent double-clicks while choosing
                CardGameController.Instance.ShowColorPicker(
                    chosenColor => SendPlay(card, CardGameController.Instance, chosenColor));
                return;
            }

            SendPlay(card, CardGameController.Instance, CardColor.None);
        }

        private void SendPlay(Card card, CardGameController instance, CardColor chosenColor)
        {
            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.PlayCard,
                Card = card.CardData,
                chosenWildColor = chosenColor
            });

            card.PlayTowards(instance.CardTargetTransform, instance._canvas.transform, () => Destroy(card.gameObject));
        }

        #endregion
    }
}