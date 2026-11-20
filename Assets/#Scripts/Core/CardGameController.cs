using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardClash
{
    /// <summary>
    /// Main Mirror <see cref="NetworkBehaviour"/> composition root for a match.
    /// Owns all SyncVars/SyncObjects, Commands, and RPCs (Mirror requires these
    /// to be physically declared on the NetworkBehaviour-derived class), the
    /// Unity/Mirror lifecycle, and orchestration that glues the specialized
    /// helper classes together:
    /// <see cref="TurnManager"/> (turn progression/timer/pending draws),
    /// <see cref="CardPlayHandler"/> (play validation/resolution),
    /// <see cref="CardHandController"/> (client hand view),
    /// <see cref="PlayerManager"/> (player registry/GUI roster/scoring),
    /// <see cref="GameUIController"/> (buttons/panels/color picker),
    /// <see cref="GameTimerController"/> (timer UI/countdown), and
    /// <see cref="GameEffectsController"/> (notifications/visual/sound effects).
    /// </summary>
    [RequireComponent(typeof(NetworkMatch))]
    public class CardGameController : NetworkBehaviour, ICardEffectContext
    {
        #region Constants / Colors
        private readonly Color32 _redColor = new(255, 90, 110, 255);    // #FF5A6E
        private readonly Color32 _blueColor = new(74, 138, 223, 255);   // #4A8ADF
        private readonly Color32 _yellowColor = new(255, 211, 61, 255); // #FFD33D
        private readonly Color32 _greenColor = new(47, 224, 138, 255); // #2FE08A

        private readonly WaitForSeconds _waitForSeconds0_75 = new(0.75f);
        private readonly WaitForSeconds _waitForSeconds0_5 = new(0.125f);

        #endregion


        #region Singleton

        public static CardGameController Instance { get; private set; }

        #endregion


        #region Script References

        [Header("Script References")]
        [SerializeField] private Card _cardPrefab;
        [SerializeField] private DeckRulesSO _deckRules;
        [SerializeField] private GameRulesSO _gameRules;
        [SerializeField] private SkipCard _skipFlipbook;
        [SerializeField] private GamePlayerGUI _playerGUIPrefab;
        [SerializeField] private ReverseCard _reverseFlipbook;
        [SerializeField] private NotificationView _notificationView;
        [SerializeField] private CardGameEndManager _gameEndManager;
        [SerializeField] private GameButtonManager _gameButtonManager;
        [SerializeField] private CardVisualDatabaseSO _cardVisualDatabase;

        #endregion


        #region Inspector - GUI References

        [Space(5)]
        [Header("GUI References")]

        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _passTurnButton;
        [SerializeField] private Button _playSelectedButton;

        [SerializeField] private Button _lastCardButton;
        [SerializeField] private Button _redColorButton;
        [SerializeField] private Button _blueColorButton;
        [SerializeField] private Button _greenColorButton;
        [SerializeField] private Button _yellowColorButton;

        [Space(2.5f)]

        [SerializeField] private Image _topDiscardImage;
        [SerializeField] private Image _turnTimerRingImage;

        [Space(2.5f)]

        [SerializeField] private TextMeshProUGUI _countdownText;
        [SerializeField] private TextMeshProUGUI _gameTimerText;
        [SerializeField] private TextMeshProUGUI _drawPileCountText;

        private Button _cardDrawButton;

        private CanvasGroup _canvasGroup;

        #endregion


        #region Inspector - GameObject References

        [Space(5f)]
        [Header("Gameobject References")]

        public GameObject _canvas;

        [SerializeField] private GameObject _hand;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _countdownPanel;
        [SerializeField] private GameObject _colorPickerPanel;
        [SerializeField] private GameObject _cardDrawGameobject;
        [SerializeField] private GameObject _handButtonsGameobject;

        #endregion


        #region Inspector - Transform References

        [Space(5f)]
        [Header("Transform References")]

        [SerializeField] private Transform _playerGUIsParent;
        [SerializeField] private Transform _cardTargetTransform;

        private Transform _handContainer;

        public Transform CardTargetTransform => _cardTargetTransform;

        #endregion


        #region Inspector - Game Settings

        [SerializeField] private float _playMoveDuration = 0.3f;
        [SerializeField] private float _countdownStepDuration = 1f;

        public float PlayMoveDuration => _playMoveDuration;

        #endregion


        #region Runtime Collections

        private readonly SyncList<uint> _playerOrder = new();

        private readonly SyncDictionary<uint, PlayerGameInfo> _playerData = new();

        private int _cardPerPlayer;

        #endregion


        #region Runtime State

        private CardDeck _deck;

        private CardEffectResolver _cardEffects;

        private Coroutine _gameTimerCoroutine;

        private bool _matchEnded;
        private bool _noPowerCardFinish;
        public bool PlaySameNumberEnabled => _playSameNumber;
        internal bool HasPendingDraw => _pendingDraw.Count > 0;

        #endregion


        #region Helper Instances

        private TurnManager _turnManager;
        private PlayerManager _playerManager;
        private CardPlayHandler _playHandler;
        private CardHandController _handController;
        private GameUIController _uiController;
        private GameTimerController _timerController;
        private GameEffectsController _effectsController;

        #endregion


        #region Network Synced State

        [SyncVar(hook = nameof(OnCurrentPlayerChanged))]
        private uint _currentPlayerNetId;

        [SyncVar(hook = nameof(OnPendingDrawChanged))]
        private PendingDrawState _pendingDraw;

        [SyncVar(hook = nameof(OnTopDiscardChanged))]
        private GameCard _syncedTopDiscard;

        [SyncVar(hook = nameof(OnTurnStartTimeChanged))]
        private double _turnStartTime;

        [SyncVar(hook = nameof(OnGameStartTimeChanged))]
        private double _gameStartTime;

        [SyncVar]
        private bool _allowStacking = true;
        [SyncVar]
        private bool _playSameNumber;

        [SyncVar]
        private int _maxCardsPerTurn = 1;


        #endregion


        #region Unity Lifecycle

        private void Awake()
        {
            _cardEffects = new();

            _turnManager = new(this);
            _playerManager = new(this);
            _playHandler = new(this);
            _handController = new(this);
            _uiController = new(this);
            _timerController = new(this);
            _effectsController = new(this);

            _canvasGroup = _canvas.GetComponent<CanvasGroup>();
            _cardDrawButton = _cardDrawGameobject.GetComponent<Button>();

            _handContainer = _hand.transform;

            _topDiscardImage.color = new(1, 1, 1, 0);

            AudioManager.Instance.StopMusic();
        }

        private void OnDestroy()
        {
            _playerData.OnChange -= _playerManager.HandlePlayerDataChanged;

            _turnManager.StopTurnTimer();

            if (_gameTimerCoroutine is { })
                StopCoroutine(_gameTimerCoroutine);

            _effectsController.StopNotificationTimer();
        }

        [ClientCallback]
        private void Update()
        {
            _timerController.Tick();
        }

        #endregion


        #region Network Lifecycle

        public override void OnStartClient()
        {
            Instance = this;

            _playerData.OnChange += _playerManager.HandlePlayerDataChanged;

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _handButtonsGameobject.SetActive(false);

            _uiController.WireButtons();
        }

        public override void OnStopClient()
        {
            _playerData.OnChange -= _playerManager.HandlePlayerDataChanged;

            _timerController.StopCountdownIfRunning();

            _uiController.UnwireButtons();

            Instance = null;
        }

        #endregion


        #region Client Input - Wild Color

        [Client]
        public void ShowColorPicker(Action<CardColor> onColorChosen, Card pendingCard) =>
            _uiController.ShowColorPicker(onColorChosen, pendingCard);

        #endregion


        #region Client Input - Multi-Select

        public void ToggleCardSelection(Card card) => _handController.ToggleCardSelection(card);

        #endregion


        #region Server - Player Management

        [Server]
        public void AddPlayer(NetworkConnectionToClient conn, PlayerRoomInfo info) =>
            _playerManager.AddPlayer(conn, info);

        [Server]
        public void UpdatePlayerCardCount(NetworkConnectionToClient conn, int newCount) =>
            _playerManager.UpdatePlayerCardCount(conn, newCount);

        [Server]
        public void HandlePlayerQuit(NetworkConnectionToClient conn)
        {
            if (conn == null || conn.identity == null)
            {
                return;
            }

            var netId = conn.identity.netId;

            if (!_playerManager.Registry.Players.ContainsKey(netId))
            {
                return;
            }

            if (conn.identity.TryGetComponent<NetworkMatch>(out var quittingPlayerMatch))
                quittingPlayerMatch.matchId = Guid.Empty;

            var wasCurrentPlayer = _turnManager.RemovePlayer(netId);

            _playerManager.RemovePlayer(netId);

            if (wasCurrentPlayer && _turnManager.PlayerCount > 0)
            {
                CurrentPlayerNetId = _turnManager.CurrentPlayerNetIdFromState;
                TurnStartTime = NetworkTime.time;
            }

            if (conn.identity != null)
            {
                NetworkServer.Destroy(conn.identity.gameObject);
            }

            if (_turnManager.PlayerCount < _gameRules.MinPlayers)
            {
                EndMatch();
                return;
            }

            _playerOrder.Remove(netId);
        }

        [Server]
        public void EndMatch()
        {
            _turnManager.StopTurnTimer();

            if (_gameTimerCoroutine is { })
            {
                StopCoroutine(_gameTimerCoroutine);
                _gameTimerCoroutine = null;
            }

            foreach (var entry in _playerManager.Registry.Players.Values)
            {
                entry.Conn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.MatchEndedByOwner,

                    errorMessage = "The match has ended because there aren't enough players left."
                });

                if (entry.Conn.identity != null)
                {
                    NetworkServer.RemovePlayerForConnection(entry.Conn, RemovePlayerOptions.Destroy);
                }
            }

            _playerManager.Clear();
            _turnManager.Clear();
            _playerData.Clear();
        }

        #endregion


        #region Server - Game Setup

        [Server]
        public void StartGame(CardDeck deck, int cardsPerPlayer, bool allowStacking, bool noPowerCardFinish, bool playSameNumber, int maxCardsPerTurn)
        {
            _deck = deck;

            _cardPerPlayer = cardsPerPlayer;
            _allowStacking = allowStacking;
            _noPowerCardFinish = noPowerCardFinish;
            _playSameNumber = playSameNumber;
            _maxCardsPerTurn = maxCardsPerTurn; 

            _pendingDraw = default;

            var firstDiscard = FlipFirstCard();

            StartCoroutine(StartGameSequence(firstDiscard));
        }

        [Server]
        private IEnumerator StartGameSequence(GameCard firstDiscard)
        {
            // Allow SyncDictionary to flush first.
            yield return _waitForSeconds0_5;

            RpcPlayStartCountdown();

            DealCards(_cardPerPlayer);

            SetTopDiscard(firstDiscard, _deck.DrawPileCount);

            var countdownDuration = _countdownStepDuration * 4;
            const float dealStartDelay = 0.5f;
            var dealAnimationDuration = dealStartDelay + (_cardPerPlayer - 1) * 0.12f + _playMoveDuration;
            var waitDuration = Mathf.Max(countdownDuration, dealAnimationDuration);

            yield return new WaitForSeconds(waitDuration);

            StartGameTimer();
            _turnManager.SetNextTurn(_turnManager.TurnOrder[0]);
        }

        [Server]
        private void StartGameTimer()
        {
            if (_gameTimerCoroutine is { })
                StopCoroutine(_gameTimerCoroutine);

            _gameStartTime = NetworkTime.time;

            _gameTimerCoroutine = StartCoroutine(GameTimerRoutine());
        }

        [Server]
        private IEnumerator GameTimerRoutine()
        {
            yield return new WaitForSeconds(_gameRules.GameTimeLimit);

            EndMatchByTimeout();
        }

        [Server]
        private void DealCards(int count)
        {
            foreach (var (netId, entry) in _playerManager.Registry.Players)
            {
                List<GameCard> hand = new();

                _deck.DrawMultiple(count, hand);

                _playerManager.Registry.AddCardsToHand(netId, hand);

                var data = _playerData[netId];

                data.cardCount = hand.Count;

                _playerData[netId] = data;

                entry.Conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation =
                        ClientDeckOperation.CardDealt,

                    Cards = hand.ToArray(),

                    DrawPileCount = _deck.DrawPileCount
                });
            }
        }

        [Server]
        private GameCard FlipFirstCard()
        {
            while (_deck.TryDraw(out var firstCard))
            {
                if (firstCard.Type == CardType.Number)
                {
                    _deck.Discard(firstCard);
                    return firstCard; // no +1, no premature broadcast
                }

                _deck.ReturnToDraw(firstCard);
                _deck.Shuffle();
            }

            return default;
        }

        #endregion


        #region Server - Turn Management

        [Server]
        public void AdvanceTurn() => _turnManager.AdvanceTurn();

        [Server]
        public void ReverseTurnDirection() => _turnManager.ReverseTurnDirection();

        [Server]
        public void SkipNextPlayer() => _turnManager.SkipNextPlayer();

        [Server]
        public bool IsCurrentPlayer(NetworkConnectionToClient conn) => _turnManager.IsCurrentPlayer(conn);

        int ICardEffectContext.PlayerCount => _turnManager.PlayerCount;

        void ICardEffectContext.ReverseTurnDirection() => ReverseTurnDirection();

        void ICardEffectContext.SkipNextPlayer() => SkipNextPlayer();

        void ICardEffectContext.AdvanceTurn() => AdvanceTurn();

        uint ICardEffectContext.GetNextPlayerNetId() => _turnManager.GetNextPlayerNetId();

        void ICardEffectContext.ForcePlayerDraw(uint targetNetId, int count) => _turnManager.ForcePlayerDraw(targetNetId, count);

        void ICardEffectContext.AddPendingDraw(CardType type, int count) => _turnManager.AddPendingDraw(type, count);

        #endregion


        #region Server - Card Play

        [Server]
        public void HandlePlayerCard(NetworkConnectionToClient conn, GameCard card, CardColor chosenWildColor, bool applyEffect = true) =>
            _playHandler.HandlePlayerCard(conn, card, chosenWildColor, applyEffect);

        [Server]
        public void HandlePlayerCards(NetworkConnectionToClient conn, GameCard[] cards, CardColor chosenWildColor) =>
            _playHandler.HandlePlayerCards(conn, cards, chosenWildColor);

        [Server]
        internal void SetTopDiscard(GameCard card, int drawCount)
        {
            _syncedTopDiscard = card;

            RpcShowTopDiscard(card, drawCount);
        }

        [Server]
        public void HandleLastCardCall(NetworkConnectionToClient conn) => _playHandler.HandleLastCardCall(conn);

        #endregion


        #region Server - Card Drawing

        [TargetRpc]
        internal void TargetPlayPenaltyDrawSound(NetworkConnectionToClient target) =>
            _effectsController.PlayPenaltyDrawSound();

        [Server]
        public void HandleDrawCard(NetworkConnectionToClient conn) => _turnManager.HandleDrawCard(conn);

        [Server]
        public void HandlePassTurn(NetworkConnectionToClient conn) => _turnManager.HandlePassTurn(conn);

        #endregion


        #region Server - Notification

        [Server]
        internal void ShowNotification(NotificationType notificationType, string text)
        {
            RpcShowNotification(notificationType, text);

            _effectsController.ResetNotificationHideTimer();
        }

        #endregion


        #region Client - Notification

        [ClientRpc]
        private void RpcShowNotification(NotificationType notificationType, string text) =>
            _effectsController.ShowNotificationVisual(notificationType, text);

        [ClientRpc]
        internal void RpcHideNotification() => _effectsController.HideNotificationVisual();

        #endregion


        #region Server - Game End

        [Server]
        private IEnumerator HandlePlayerWin()
        {
            _matchEnded = true;

            yield return _waitForSeconds0_75;

            _turnManager.StopTurnTimer();

            if (_gameTimerCoroutine is { })
            {
                StopCoroutine(_gameTimerCoroutine);
                _gameTimerCoroutine = null;
            }

            var finalScores = _playerManager.CalculateFinalScores(out var winnerNetId); 

            RpcShowGameEndScreen(finalScores.ToArray());

            PlayEndGameSounds(winnerNetId);

            NotifyOwnerCanRestart();
        }

        [Server]
        internal void TriggerPlayerWin() => StartCoroutine(HandlePlayerWin());

        [Server]
        private void EndMatchByTimeout()
        {
            _matchEnded = true;

            _turnManager.StopTurnTimer();

            _gameTimerCoroutine = null;

            var finalScores = _playerManager.CalculateFinalScores(out var winnerNetId);

            RpcShowGameEndScreen(finalScores.ToArray());

            PlayEndGameSounds(winnerNetId);

            NotifyOwnerCanRestart();
        }

        [Server]
        private void PlayEndGameSounds(uint winnerNetId)
        {
            foreach (var entry in _playerManager.Registry.Players)
            {
                var conn = entry.Value.Conn;

                if (conn == null)
                    continue;

                // This is where TargetPlayWinSound / TargetPlayLoseSound get invoked,
                // one per connected player, routed individually via TargetRpc.
                if (entry.Key == winnerNetId)
                    TargetPlayWinSound(conn);
                else
                    TargetPlayLoseSound(conn);
            }
        }

        [Server]
        private void NotifyOwnerCanRestart()
        {
            var owner = _playerManager.Registry.Players.Values.FirstOrDefault(e => e.RoomInfo.isOwner);

            if (owner?.Conn != null)
                TargetShowPlayAgainButton(owner.Conn);

        }

        [TargetRpc]
        private void TargetShowPlayAgainButton(NetworkConnectionToClient target) => _gameEndManager.ShowPlayAgainButton();

        #endregion


        #region Client Game End

        [TargetRpc]
        private void TargetPlayWinSound(NetworkConnectionToClient target) => _effectsController.PlayGameEndSfx("win");

        [TargetRpc]
        private void TargetPlayLoseSound(NetworkConnectionToClient target) => _effectsController.PlayGameEndSfx("lose");

        #endregion


        #region Server - Restart

        [Command(requiresAuthority = false)]
        public void CmdRequestPlayAgain(NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null)
                return;

            var netId = sender.identity.netId;

            if (!_playerManager.Registry.Players.TryGetValue(netId, out var entry) || !entry.RoomInfo.isOwner)
                return;

            RestartMatch();

            _matchEnded = false;
        }

        [Server]
        private void RestartMatch()
        {
            _pendingDraw = default;

            _playerManager.Registry.ClearHands();
            _turnManager.ResetTurnIndex();

            foreach(var netId in _playerData.Keys.ToList())
            {
                var data = _playerData[netId];
                data.cardCount = 0;
                data.hasCalledLastCard = false;
                _playerData[netId] = data;
            }

            RpcResetForRestart();

            var deck = new CardDeck();
            deck.BuildDeck(_deckRules);

            StartGame(deck, _cardPerPlayer, _allowStacking, 
                _noPowerCardFinish, _playSameNumber, _maxCardsPerTurn);
        }

        #endregion


        #region Client - Restart

        [ClientRpc]
        private void RpcResetForRestart()
        {
            _gameEndManager.HideGameEndScreen();

            _handController.ClearForRestart();

            _gameButtonManager.SetPassButtonInteractable(false);

            _turnManager.ResetDrawTracking();
        }

        #endregion


        #region Client - Game End

        [ClientRpc]
        private void RpcShowGameEndScreen(GameEndPlayerInfo[] standings)
        {
            _uiController.CancelPendingColorPicker(cancelPendingCardPlay: false);

            _gameEndManager.ShowGameEndScreen(standings);
        }

        #endregion


        #region Client - Card Display

        [Client]
        public void ShowDealtCards(GameCard[] cards, bool applyStartDelay = false) =>
            _handController.ShowDealtCards(cards, applyStartDelay);

        [Client]
        public void OnDrawnCardReceived(bool canPlay, GameCard drawnCard) =>
            _handController.OnDrawnCardReceived(canPlay, drawnCard);

        [Client]
        public void RemoveHandCard(uint cardId) => _handController.RemoveHandCard(cardId);

        #endregion


        #region Client - Countdown

        [ClientRpc]
        private void RpcPlayStartCountdown()
        {
            if (this == null || !gameObject.activeInHierarchy)
                return;

            _timerController.PlayStartCountdown();
        }

        #endregion


        #region Client RPC - Card State

        [ClientRpc]
        internal void RpcShowTopDiscard(GameCard card, int drawCount)
        {
            if (this == null || !gameObject.activeInHierarchy)
                return;

            _syncedTopDiscard = card;

            _effectsController.ShowTopDiscardVisual(card, drawCount);
        }

        [ClientRpc]
        internal void RpcPlayDiscardEffect(GameCard card, int turnDirection)
        {
            if (this == null || !gameObject.activeInHierarchy)
                return;

            _effectsController.PlayDiscardEffectVisual(card, turnDirection);
        }

        [ClientRpc]
        internal void RpcPlaySkipEffect(uint skippedNetId, Color tint)
        {
            if (this == null || !gameObject.activeInHierarchy)
                return;

            _effectsController.PlaySkipEffectVisual(skippedNetId, tint);
        }

        #endregion


        #region Sync Callbacks

        private void OnCurrentPlayerChanged(uint oldNetId, uint newNetId)
        {
            _handController.AwaitingDrawnCardDecision = false;

            _gameButtonManager.SetPassButtonInteractable(false);

            _handController.ClearCardSelection();

            if (_playerManager.GetPlayerGUI(oldNetId) is { } previousPlayerGUI)
                previousPlayerGUI.ResetTurnTimer();

            if (NetworkClient.localPlayer is not { netId: var selfNetId })
                return;

            var isSelf = newNetId == selfNetId;

            if (!isSelf)
                _uiController.CancelPendingColorPicker(cancelPendingCardPlay: true);

            _cardDrawButton.interactable = isSelf;

            _handController.RefreshHandInteractability(isSelf);
            _handController.RefreshLastCardButtonInteractable();
        }

        private void OnTopDiscardChanged(GameCard oldCard, GameCard newCard)
        {
            _syncedTopDiscard = newCard;

            if (NetworkClient.localPlayer is { netId: var selfNetId } && _currentPlayerNetId == selfNetId)
            {
                _handController.RefreshHandInteractability(true);
            }
        }

        private void OnTurnStartTimeChanged(double oldValue, double newValue) =>
            _timerController.HandleTurnStartTimeChanged(oldValue, newValue);

        private void OnGameStartTimeChanged(double oldValue, double newValue) =>
            _timerController.HandleGameStartTimeChanged(oldValue, newValue);

        private void OnPendingDrawChanged(PendingDrawState oldValue, PendingDrawState newValue)
        {
            if(NetworkClient.localPlayer is { netId: var selfNetId } && _currentPlayerNetId == selfNetId)
            {
                _handController.RefreshHandInteractability(true);
            }
        }

        #endregion


        #region Client - Turn / Card Validation

        [Client]
        public bool IsMyTurn() => _turnManager.IsMyTurn();

        public void RefreshHandInteractability(bool isMyTurn) => _handController.RefreshHandInteractability(isMyTurn);

        [Client]
        public void RefreshLastCardButtonInteractable() => _handController.RefreshLastCardButtonInteractable();

        #endregion


        #region Utility

        internal Color ToUnityColor(CardColor color)
        {
            return color switch
            {
                CardColor.Red => _redColor,
                CardColor.Yellow => _yellowColor,
                CardColor.Green => _greenColor,
                CardColor.Blue => _blueColor,
                _ => Color.white
            };
        }

        #endregion


        #region Internal Accessors

        // Exposes inspector-serialized fields, SyncVars/SyncObjects, and
        // controller-private runtime state to the helper classes above, since
        // Mirror's weaver requires SyncVar fields/writes and [Command]/[ClientRpc]/
        // [TargetRpc] methods to remain physically declared on this
        // NetworkBehaviour-derived class. Helper classes never touch private
        // fields directly — only through these accessors.

        internal TurnManager TurnManager => _turnManager;
        internal PlayerManager PlayerManager => _playerManager;
        internal CardPlayHandler PlayHandler => _playHandler;
        internal CardHandController HandController => _handController;
        internal GameUIController UIController => _uiController;

        internal Card CardPrefab => _cardPrefab;
        internal GameRulesSO GameRules => _gameRules;
        internal NotificationView NotificationView => _notificationView;
        internal CardVisualDatabaseSO CardVisualDatabase => _cardVisualDatabase;
        internal SkipCard SkipFlipbook => _skipFlipbook;
        internal ReverseCard ReverseFlipbook => _reverseFlipbook;
        internal GameButtonManager GameButtonManager => _gameButtonManager;

        internal Button QuitButton => _quitButton;
        internal Button PauseButton => _pauseButton;
        internal Button ResumeButton => _resumeButton;
        internal Button PassTurnButton => _passTurnButton;
        internal Button PlaySelectedButton => _playSelectedButton;
        internal Button LastCardButton => _lastCardButton;
        internal Button RedColorButton => _redColorButton;
        internal Button BlueColorButton => _blueColorButton;
        internal Button GreenColorButton => _greenColorButton;
        internal Button YellowColorButton => _yellowColorButton;
        internal Button CardDrawButton => _cardDrawButton;

        internal Image TopDiscardImage => _topDiscardImage;
        internal Image TurnTimerRingImage => _turnTimerRingImage;

        internal TextMeshProUGUI CountdownText => _countdownText;
        internal TextMeshProUGUI GameTimerText => _gameTimerText;
        internal TextMeshProUGUI DrawPileCountText => _drawPileCountText;

        internal GameObject PausePanel => _pausePanel;
        internal GameObject CountdownPanel => _countdownPanel;
        internal GameObject ColorPickerPanel => _colorPickerPanel;
        internal GameObject CardDrawGameObject => _cardDrawGameobject;
        internal GameObject HandButtonsGameObject => _handButtonsGameobject;

        internal Transform PlayerGUIsParent => _playerGUIsParent;
        internal Transform HandContainer => _handContainer;

        internal GamePlayerGUI PlayerGUIPrefab => _playerGUIPrefab;

        internal float CountdownStepDuration => _countdownStepDuration;

        internal CardDeck Deck => _deck;
        internal CardEffectResolver CardEffects => _cardEffects;
        internal bool MatchEnded => _matchEnded;
        internal bool NoPowerCardFinish => _noPowerCardFinish;
        internal bool AllowStacking => _allowStacking;
        internal int MaxCardsPerTurn => _maxCardsPerTurn;

        internal SyncList<uint> PlayerOrder => _playerOrder;
        internal SyncDictionary<uint, PlayerGameInfo> PlayerData => _playerData;

        internal uint CurrentPlayerNetId
        {
            get => _currentPlayerNetId;
            set => _currentPlayerNetId = value;
        }

        internal PendingDrawState PendingDraw
        {
            get => _pendingDraw;
            set => _pendingDraw = value;
        }

        internal GameCard SyncedTopDiscard => _syncedTopDiscard;

        internal double TurnStartTime
        {
            get => _turnStartTime;
            set => _turnStartTime = value;
        }

        internal double GameStartTime => _gameStartTime;

        #endregion
    }


    #region Supporting Classes

    public class PlayerEntry
    {
        public NetworkConnectionToClient Conn;
        public PlayerRoomInfo RoomInfo;
    }

    #endregion
}
