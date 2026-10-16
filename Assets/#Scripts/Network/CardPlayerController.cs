using Mirror;
using System.Collections;
using UnityEngine;

namespace CardClash
{
    public class CardPlayerController : NetworkBehaviour
    {
        #region Constants/Readonly

        private readonly WaitForSeconds _waitForSeconds0_5 = new(0.5f);

        #endregion

        #region Unity Lifecycle

        public void TryPlayCard(Card card)
        {
            if (!isLocalPlayer)
                return;

            var gameController = CardGameController.Instance;

            if (!gameController.IsMyTurn())
                return;

            if (card.CardData.Type is CardType.Wild or CardType.WildDrawFour)
            {
                card.SetInteractable(false); // prevent double-clicks while choosing

                StartCoroutine(ShowColorPickerDelayed(card, gameController));
                return;
            }

            SendPlay(card, CardColor.None);
        }

        private IEnumerator ShowColorPickerDelayed(Card card, CardGameController controller)
        {
            yield return _waitForSeconds0_5;

            controller.ShowColorPicker(
                chosenColor => SendPlay(card, chosenColor));
        }

        private void SendPlay(Card card, CardColor chosenColor)
        {
            card.BeginReturnWatch();

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.PlayCard,
                Card = card.CardData,
                chosenWildColor = chosenColor
            });

            var gameController = CardGameController.Instance;

            card.PlayTowards(gameController.CardTargetTransform, gameController._canvas.transform);
        }

        #endregion
    }
}