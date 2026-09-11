using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CardClash
{
    [RequireComponent(typeof(NetworkMatch))]
    public class CardGameController : NetworkBehaviour, ICardEffectContext
    {

        #region Constants / Colors
        private readonly Color32 _redColor = new(234, 50, 60, 255);
        private readonly Color32 _blueColor = new(0, 152, 220, 255);
        private readonly Color32 _yellowColor = new(255, 200, 37, 255);
        private readonly Color32 _greenColor = new(51, 152, 75, 255);

        private readonly WaitForSeconds _waitForSeconds0_12 = new(0.12f);
        private readonly WaitForSeconds _waitForSeconds0_75 = new WaitForSeconds(0.75f);

        #endregion


        #region Singleton

        public static CardGameController Instance { get; private set; }

        #endregion


        #region Inspector - Script References

        [Header("Script References")]
        [SerializeField] private Card _cardPrefab;
        [SerializeField] private GamePlayerGUI _playerGUIPrefab;
        [SerializeField] private NotificationView _notificationView;
        [SerializeField] private CardGameEndManager _gameEndManager;

        #endregion


        #region Inspector - GUI References

        [Space(5)]
        [Header("GUI References")]

        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _passTurnButton;

        [FormerlySerializedAs("_unoButton")]
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
        [SerializeField] private TextMeshProUGUI _currentPlayerNameText;

        [Space(2.5f)]

        [SerializeField] private CanvasGroup _pausePanel;
        [SerializeField] private CanvasGroup _countdownPanel;
        [SerializeField] private CanvasGroup _colorPickerPanel;

        private Button _cardDrawButton;
        private CanvasGroup _canvasGroup;
        private CanvasGroup _passButtonCanvasGroup;

        #endregion


        #region Inspector - GameObject References

        [Space(5f)]
        [Header("Gameobject References")]

        public GameObject _canvas;

        [SerializeField] private GameObject _hand;
        [SerializeField] private GameObject _cardDrawGameobject;

        #endregion


        #region Inspector - Transform References

        [Space(5f)]
        [Header("Transform References")]

        [SerializeField] private Transform _cardTargetTransform;

        private Transform _handContainer;

        public Transform CardTargetTransform => _cardTargetTransform;

        #endregion


        #region Inspector - Game Settings

        [SerializeField] private int _cardPerPlayer;
        [SerializeField] private float _turnTimeLimit = 15f;
        [SerializeField] private float _gameTimeLimit = 300f;
        [SerializeField] private float _playMoveDuration = 0.3f;
        [SerializeField] private float _countdownStepDuration = 1f;

        public float PlayMoveDuration => _playMoveDuration;

        #endregion


        #region Runtime Collections

        private readonly List<GamePlayerGUI> _playerGUIs = new();

        private readonly Dictionary<uint, int> _netIdToGuiIndex = new();

        private readonly SyncDictionary<uint, PlayerGameInfo> _playerData = new();

        #endregion


        #region Runtime State

        private CardDeck _deck;

        private readonly CardTurnStateMachine _turnState = new();
        private readonly PlayerHandRegistry _playerRegistry = new();
        private readonly CardEffectResolver _cardEffects = new();

        private Action<CardColor> _onColorChosen;

        private Coroutine _countdownCoroutine;
        private Coroutine _gameTimerCoroutine;
        private Coroutine _turnTimerCoroutine; 
        private Coroutine _notificationHideCoroutine;

        private bool _isTimerRunningLocally;
        private bool _isGameRunningLocally;
        private bool _awaitingDrawnCardDecision;

        #endregion


        #region Inspector - Notification Settings

        [SerializeField]
        private float _notificationDisplayDuration = 2f;

        #endregion


        #region Network Synced State

        [SyncVar(hook = nameof(OnCurrentPlayerChanged))]
        private uint _currentPlayerNetId;

        [SyncVar(hook = nameof(OnTopDiscardChanged))]
        private GameCard _syncedTopDiscard;

        [SyncVar(hook = nameof(OnTurnStartTimeChanged))]
        private double _turnStartTime;

        [SyncVar(hook = nameof(OnGameStartTimeChanged))]
        private double _gameStartTime;

        #endregion


        #region Unity Lifecycle

        private void Awake()
        {
            _playerData.OnChange += OnPlayerDataChanged;

            _canvasGroup = _canvas.GetComponent<CanvasGroup>();
            _cardDrawButton = _cardDrawGameobject.GetComponent<Button>();
            _handContainer = _hand.transform;
            _passButtonCanvasGroup = _passTurnButton.GetComponent<CanvasGroup>();

            _topDiscardImage.color = new(1, 1, 1, 0);
        }

        private void OnDestroy()
        {
            _playerData.OnChange -= OnPlayerDataChanged;

            if (_turnTimerCoroutine is { })
                StopCoroutine(_turnTimerCoroutine);

            if (_gameTimerCoroutine is { })
                StopCoroutine(_gameTimerCoroutine);

            if (_notificationHideCoroutine is { })
                StopCoroutine(_notificationHideCoroutine);
        }

        private void Update()
        {
            if (!_isTimerRunningLocally || _turnTimerRingImage == null)
                return;

            var turnTimeElapsed = NetworkTime.time - _turnStartTime;

            var normalized = _turnTimeLimit > 0f
                ? 1f - (float)(turnTimeElapsed / _turnTimeLimit)
                : 0f;

            _turnTimerRingImage.fillAmount = Mathf.Clamp01(normalized);

            if (normalized <= 0f)
                _isTimerRunningLocally = false;

            if (_isGameRunningLocally && _gameTimerText != null)
            {
                var gameTimeElapsed = NetworkTime.time - _gameStartTime;

                var remaining = Mathf.Max(0f, _gameTimeLimit - (float)gameTimeElapsed);

                var minutes = Mathf.FloorToInt(remaining / 60f);
                var seconds = Mathf.FloorToInt(remaining % 60f);

                _gameTimerText.SetText($"{minutes:00}:{seconds:00}");

                if (remaining <= 0f)
                    _isGameRunningLocally = false;
            }
        }

        private void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        #endregion


        #region Network Lifecycle

        public override void OnStartClient()
        {
            Instance = this;

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _quitButton.onClick.AddListener(OnClickQuitButton);

            _pauseButton.onClick.AddListener(() => OpenAndClosePausePanel(true));

            _resumeButton.onClick.AddListener(() => OpenAndClosePausePanel(false));

            _cardDrawButton.onClick.AddListener(OnDrawButtonClicked);
            _passTurnButton.onClick.AddListener(OnPassButtonClicked);
            _lastCardButton.onClick.AddListener(OnLastCardButtonClicked);

            _passButtonCanvasGroup.alpha = 0f;
            _passButtonCanvasGroup.interactable = false;

            _redColorButton.onClick.AddListener(OnRedColorClicked);
            _yellowColorButton.onClick.AddListener(OnYellowColorClicked);
            _greenColorButton.onClick.AddListener(OnGreenColorClicked);
            _blueColorButton.onClick.AddListener(OnBlueColorClicked);

            SetCanvasGroupVisible(_pausePanel, false);
            SetCanvasGroupVisible(_countdownPanel, false);
            SetCanvasGroupVisible(_colorPickerPanel, false);
        }

        public override void OnStopClient()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            _cardDrawButton.onClick.RemoveListener(OnDrawButtonClicked);
            _passTurnButton.onClick.RemoveListener(OnPassButtonClicked);
            _lastCardButton.onClick.RemoveListener(OnLastCardButtonClicked);

            _redColorButton.onClick.RemoveListener(OnRedColorClicked);
            _yellowColorButton.onClick.RemoveListener(OnYellowColorClicked);
            _greenColorButton.onClick.RemoveListener(OnGreenColorClicked);
            _blueColorButton.onClick.RemoveListener(OnBlueColorClicked);

            _quitButton.onClick.RemoveListener(OnClickQuitButton);

            _pauseButton.onClick.RemoveListener(() => OpenAndClosePausePanel(true));

            _resumeButton.onClick.RemoveListener(() => OpenAndClosePausePanel(false));

            Instance = null;
        }

        #endregion


        #region UI - General

        private void OnClickQuitButton()
        {
            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.QuitMatch
            });
        }

        private void OpenAndClosePausePanel(bool value)
        {
            SetCanvasGroupVisible(_pausePanel, value);
        }

        #endregion


        #region Client Input - Draw / Pass

        [Client]
        private void OnDrawButtonClicked()
        {
            if (!IsMyTurn())
                return;

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.DrawCard
            });
        }

        [Client]
        private void OnPassButtonClicked()
        {
            if (!_awaitingDrawnCardDecision)
                return;

            _awaitingDrawnCardDecision = false;

            _passButtonCanvasGroup.alpha = 0f;
            _passButtonCanvasGroup.interactable = false;

            RefreshHandInteractability(false);

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.PassTurn
            });
        }

        [Client]
        private void OnLastCardButtonClicked()
        {
            if (!IsMyTurn())
                return;

            NetworkClient.Send(new ServerDeckMessage
            {
                serverDeckOperation = ServerDeckOperation.CallLastCard
            });
        }

        #endregion


        #region Client Input - Wild Color

        [Client]
        public void ShowColorPicker(Action<CardColor> onColorChosen)
        {
            _onColorChosen = onColorChosen;

            SetCanvasGroupVisible(_colorPickerPanel, true);
        }

        [Client]
        private void OnRedColorClicked()
        {
            HandleColorPicked(CardColor.Red);
        }

        [Client]
        private void OnYellowColorClicked()
        {
            HandleColorPicked(CardColor.Yellow);
        }

        [Client]
        private void OnGreenColorClicked()
        {
            HandleColorPicked(CardColor.Green);
        }

        [Client]
        private void OnBlueColorClicked()
        {
            HandleColorPicked(CardColor.Blue);
        }

        [Client]
        private void HandleColorPicked(CardColor color)
        {
            SetCanvasGroupVisible(_colorPickerPanel, false);

            var callback = _onColorChosen;

            _onColorChosen = null;

            callback?.Invoke(color);
        }

        #endregion


        #region Server - Player Management

        [Server]
        public void AddPlayer(NetworkConnectionToClient conn, PlayerRoomInfo info)
        {
            uint netId = conn.identity.netId;

            _playerRegistry.AddPlayer(netId, new PlayerEntry
            {
                Conn = conn,
                RoomInfo = info
            });

            _turnState.AddPlayer(netId);

            _playerData[netId] = new PlayerGameInfo
            {
                connectionId = netId,
                cardCount = 0,
                playerName = info.playerName,
                isOwner = info.isOwner
            };
        }

        [Server]
        public void UpdatePlayerCardCount(NetworkConnectionToClient conn, int newCount)
        {
            uint netId = conn.identity.netId;

            if (!_playerData.ContainsKey(netId))
                return;

            var data = _playerData[netId];

            data.cardCount = newCount;

            _playerData[netId] = data;
        }

        public void HandlePlayerQuit(NetworkConnectionToClient conn)
        {
            var netId = conn.identity.netId;

            if (!_playerRegistry.Players.ContainsKey(netId))
                return;

            var quittingPlayerName = _playerRegistry.Players[netId].RoomInfo.playerName;

            var wasCurrentPlayer = _turnState.RemovePlayer(netId);

            _playerRegistry.RemovePlayer(netId);
            _playerData.Remove(netId);

            if (wasCurrentPlayer && _turnState.PlayerCount > 0)
            {
                _currentPlayerNetId = _turnState.CurrentPlayerNetId;

                _turnStartTime = NetworkTime.time;
            }

            // Tell the quitting client to go back to the lobby.
            conn.Send(new ClientRoomMessage
            {
                clientRoomOperation = ClientRoomOperation.Left
            });

            // Remove their player object without disconnecting them.
            if (conn.identity != null)
            {
                NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);
            }

            foreach (var entry in _playerRegistry.Players.Values)
            {
                entry.Conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.PlayerQuit
                });
            }

            ShowNotification("Player left", quittingPlayerName, Color.black);

            if (_turnState.PlayerCount <= 1)
            {
                //TO DO...
            }
        }

        [Server]
        public void EndMatch()
        {
            if (_turnTimerCoroutine is { })
            {
                StopCoroutine(_turnTimerCoroutine);

                _turnTimerCoroutine = null;
            }

            if (_gameTimerCoroutine is { })
            {
                StopCoroutine(_gameTimerCoroutine);
                _gameTimerCoroutine = null;
            }

            foreach (var entry in _playerRegistry.Players.Values)
            {
                entry.Conn.Send(new ClientRoomMessage
                {
                    clientRoomOperation = ClientRoomOperation.MatchEndedByOwner,

                    errorMessage = "The room owner has left. The match has ended."
                });

                if (entry.Conn.identity != null)
                {
                    NetworkServer.RemovePlayerForConnection(entry.Conn, RemovePlayerOptions.Destroy);
                }
            }

            _playerRegistry.Clear();
            _turnState.Clear();
            _playerData.Clear();
        }

        #endregion


        #region Server - Game Setup

        [Server]
        public void StartGame(CardDeck deck, int cardsPerPlayer)
        {
            _deck = deck;

            _cardPerPlayer = cardsPerPlayer;

            FlipFirstCard();

            StartCoroutine(StartGameSequence());
        }

        [Server]
        private IEnumerator StartGameSequence()
        {
            // Allow SyncDictionary to flush first.
            yield return null;

            RpcPlayStartCountdown();

            DealCards(_cardPerPlayer);

            var countdownDuration = _countdownStepDuration * 4;

            const float dealStartDelay = 0.5f;

            var dealAnimationDuration = dealStartDelay + (_cardPerPlayer - 1) * 0.12f + _playMoveDuration;

            var waitDuration = Mathf.Max(countdownDuration, dealAnimationDuration);

            yield return new WaitForSeconds(waitDuration);

            StartGameTimer();

            SetNextTurn(_turnState.TurnOrder[0]);
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
            yield return new WaitForSeconds(_gameTimeLimit);

            Debug.Log("[Server] Game time limit reached. Ending match.");

            EndMatchByTimeout();
        }

        [Server]
        private void DealCards(int count)
        {
            foreach (var (netId, entry) in _playerRegistry.Players)
            {
                List<GameCard> hand = new();

                _deck.DrawMultiple(count, hand);

                _playerRegistry.AddCardsToHand(netId, hand);

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
        private void FlipFirstCard()
        {
            GameCard firstCard;

            do
            {
                if (!_deck.TryDraw(out firstCard))
                    return;

                if (firstCard.Type != CardType.Number)
                {
                    _deck.ReturnToDraw(firstCard);

                    _deck.Shuffle();
                }
                else
                {
                    break;
                }

            } while (true);

            _deck.Discard(firstCard);

            SetTopDiscard(firstCard, _deck.DrawPileCount + 1);
        }

        #endregion


        #region Server - Turn Management

        [Server]
        private void SetNextTurn(uint netID)
        {
            _currentPlayerNetId = netID;

            RestartTurnTimer();
        }

        [Server]
        private void RestartTurnTimer()
        {
            if (_turnTimerCoroutine is { })
            {
                StopCoroutine(_turnTimerCoroutine);
            }

            _turnStartTime = NetworkTime.time;

            _turnTimerCoroutine = StartCoroutine(TurnTimerRoutine(_currentPlayerNetId));
        }

        [Server]
        private IEnumerator TurnTimerRoutine(uint netIdForThisTurn)
        {
            yield return new WaitForSeconds(_turnTimeLimit );

            if (_currentPlayerNetId != netIdForThisTurn)
                yield break;

            Debug.Log($"[Turn] Time expired for netId {netIdForThisTurn}. Forcing pass.");

            ForcePlayerDraw(netIdForThisTurn, 1);

            if (_playerData.TryGetValue(netIdForThisTurn, out var data))
                ShowNotification("Time's up!", $"{data.playerName} drew a card", Color.black);

            AdvanceTurn();
        }

        [Server]
        public void AdvanceTurn()
        {
            _turnState.AdvanceTurn();

            SetNextTurn(_turnState.CurrentPlayerNetId);
        }

        [Server]
        public void ReverseTurnDirection()
        {
            _turnState.ReverseDirection();
        }

        [Server]
        public void SkipNextPlayer()
        {
            _turnState.SkipNextPlayer();
        }

        [Server]
        public bool IsCurrentPlayer(NetworkConnectionToClient conn)
        {
            return conn.identity.netId == _currentPlayerNetId;
        }

        [Server]
        private uint GetNextPlayerNetId()
        {
            return _turnState.GetNextPlayerNetId();
        }


        int ICardEffectContext.PlayerCount => _turnState.PlayerCount;

        void ICardEffectContext.ReverseTurnDirection() => ReverseTurnDirection();

        void ICardEffectContext.SkipNextPlayer() => SkipNextPlayer();

        void ICardEffectContext.AdvanceTurn() => AdvanceTurn();

        uint ICardEffectContext.GetNextPlayerNetId() => GetNextPlayerNetId();

        void ICardEffectContext.ForcePlayerDraw(uint targetNetId, int count) => ForcePlayerDraw(targetNetId, count);

        #endregion


        #region Server - Card Play

        [Server]
        public void HandlePlayerCard(NetworkConnectionToClient conn, GameCard card, CardColor chosenWildColor)
        {
            var netId = conn.identity.netId;

            if (!IsLegalPlay(card))
            {
                Debug.LogWarning($"[Server] Rejected illegal play " + $"from netId {netId}: {card}");

                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,

                    errorMessage = $"Illegal play: {card} does not match " + $"the current discard/stack requirement."
                });

                return;
            }

            if (card.Type is CardType.Wild or CardType.WildDrawFour)
            {
                if (chosenWildColor is CardColor.None)
                {
                    Debug.LogWarning($"[Server] Rejected wild play " + $"from netId {netId}: no color chosen.");

                    conn.Send(new ClientDeckMessage
                    {
                        clientDeckOperation = ClientDeckOperation.Error,

                        errorMessage = "You must choose a color " + "for the Wild card."
                    });

                    return;
                }

                card.Color = chosenWildColor;

                var chosenColorUnityColor = chosenWildColor switch
                {
                    CardColor.Red => (Color)_redColor,
                    CardColor.Blue => (Color)_blueColor,
                    CardColor.Green => (Color)_greenColor,
                    CardColor.Yellow => (Color)_yellowColor,
                    _ => Color.white
                };

                ShowNotification("Color changes to:", chosenWildColor.ToString(), chosenColorUnityColor); 
            }

            if (!TryRemoveFromHand(netId, card))
            {
                Debug.LogWarning($"[Server] Rejected play from netId " + $"${netId}: card {card} not found " + $"in tracked hand.");

                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,

                    errorMessage = $"Illegal play: {card} " + $"is not in your hand."
                });

                return;
            }

            _deck.Discard(card);

            SetTopDiscard(card, _deck.DrawPileCount + 1);

            var data = _playerData[netId];

            data.cardCount -= 1;

            var wasDrawnCard = _playerRegistry.WasLastDrawnCard(netId, card.Id);

            _playerRegistry.ClearLastDrawnCardId(netId);

            if (wasDrawnCard && data.cardCount == 1)
            {
                data.hasCalledLastCard = true;
            }

            _playerData[netId] = data;

            conn.Send(new ClientDeckMessage
            {
                clientDeckOperation = ClientDeckOperation.CardPlayed,

                TopDiscardCard = card,

                DrawPileCount = _deck.DrawPileCount
            });


            if (data.cardCount <= 0)
            {
                StartCoroutine(HandlePlayerWin());
                return;
            }

            if (data.cardCount == 1)
            {
                if (!data.hasCalledLastCard)
                {
                    PenalizeMissedLastCardCall(netId);
                }

                else if (wasDrawnCard)
                {
                    ShowNotification("LAST CARD!", data.playerName, Color.red);
                }
            }


            _cardEffects.Resolve(card.Type).Apply(this);
        }

        [Server]
        private bool IsLegalPlay(GameCard card)
        {
            return IsPlayableAgainstTop(card);
        }

        [Server]
        private bool TryRemoveFromHand(uint netId,GameCard card)
        {
            return _playerRegistry.TryRemoveCard(netId, card);
        }

        [Server]
        private bool IsPlayableAgainstTop(GameCard card)
        {
            if (_deck.TopDiscard is not { } topCard)
                return true;

            return card.Type switch
            {
                CardType.Wild
                    or CardType.WildDrawFour => true,

                _ when card.Color ==
                       topCard.Color => true,

                _ when card.Type ==
                       topCard.Type => true,

                CardType.Number
                    when topCard.Type
                        is CardType.Number
                    && card.FaceValue ==
                       topCard.FaceValue => true,

                _ => false
            };
        }

        [Server]
        private void SetTopDiscard(GameCard card, int drawCount)
        {
            _syncedTopDiscard = card;

            RpcShowTopDiscard(
                card,
                drawCount
            );
        }

        [Server]
        public void HandleLastCardCall(NetworkConnectionToClient conn)
        {
            var netId = conn.identity.netId;

            if (!_playerData.TryGetValue(netId, out var data))
                return;

            if (_playerRegistry.GetHandCount(netId) > 2)
            {
                conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.Error,

                    errorMessage = "You can only call LAST CARD when you have 2 cards or fewer."
                });

                return;
            }

            if (data.hasCalledLastCard)
                return;

            data.hasCalledLastCard = true;

            _playerData[netId] = data;

            foreach (var entry in _playerRegistry.Players.Values)
            {
                entry.Conn.Send(new ClientDeckMessage
                {
                    clientDeckOperation = ClientDeckOperation.LastCardCalled
                });
            }

            ShowNotification("LAST CARD!", data.playerName, Color.red);
        }

        [Server]
        private void ResetLastCardCall(uint netId)
        {
            if (!_playerData.TryGetValue(netId, out var data))
                return;

            if (!data.hasCalledLastCard)
                return;

            data.hasCalledLastCard = false;

            _playerData[netId] = data;
        }

        [Server]
        private void PenalizeMissedLastCardCall(uint netId)
        {
            Debug.Log($"[Server] netId {netId} failed to call LAST CARD. Applying penalty draw.");

            ForcePlayerDraw(netId, 1);

            ResetLastCardCall(netId);

            if (_playerData.TryGetValue(netId, out var data))
            {
                ShowNotification("Missed LAST CARD!", $"{data.playerName} drew 1 card", Color.black);
            }
        }

        #endregion


        #region Server - Card Drawing

        [Server]
        private void ForcePlayerDraw(uint targetNetId, int count)
        {
            if (!_playerRegistry.Players.TryGetValue(targetNetId, out var entry))
            {
                Debug.LogWarning($"[Server] ForcePlayerDraw: no connection found for netId {targetNetId}.");

                return;
            }

            var drawn = new List<GameCard>();

            _deck.DrawMultiple(count, drawn);

            _playerRegistry.AddCardsToHand(targetNetId, drawn);

            var data = _playerData[targetNetId];

            data.cardCount += drawn.Count;

            if (data.cardCount != 1)
                ResetLastCardCall(targetNetId);

            _playerData[targetNetId] = data;

            entry.Conn.Send(new ClientDeckMessage
            {
                    clientDeckOperation = ClientDeckOperation.CardDrawn,

                    Cards = drawn.ToArray(),

                    DrawPileCount = _deck.DrawPileCount,

                    CanPlayDrawnCard = false
                }
            );
        }

        [Server]
        public void HandleDrawCard(NetworkConnectionToClient conn)
        {
            var netId = conn.identity.netId;

            if (!_deck.TryDraw(out GameCard drawnCard))
            {
                Debug.LogWarning("[Server] Draw pile empty.");

                return;
            }

            _playerRegistry.AddCardsToHand(netId, new[] { drawnCard });

            var data = _playerData[netId];

            data.cardCount++;

            _playerData[netId] = data;

            if (data.cardCount != 1)
                ResetLastCardCall(netId);

            var canPlay = IsPlayableAgainstTop(drawnCard);

            conn.Send(new ClientDeckMessage
            {
                    clientDeckOperation = ClientDeckOperation.CardDrawn,

                    Cards = new[] { drawnCard },

                    DrawPileCount =  _deck.DrawPileCount,

                    CanPlayDrawnCard = canPlay
            });

            if (canPlay)
            {
                _playerRegistry.SetLastDrawnCardId(netId, drawnCard.Id);
            }

            else
            {
                _playerRegistry.ClearLastDrawnCardId(netId);

                AdvanceTurn();
            }

            Debug.Log($"[Server] {data} drew a card. Hand size: {data.cardCount}");
        }

        [Server]
        public void HandlePassTurn(NetworkConnectionToClient conn)
        {
            if (!IsCurrentPlayer(conn))
                return;

            var netId = conn.identity.netId;

            _playerRegistry.ClearLastDrawnCardId(netId);

            Debug.Log("[Server] Player passed after drawing.");

            AdvanceTurn();
        }

        #endregion


        #region Server - Notification

        [Server]
        private void ShowNotification(string text1, string text2, Color color)
        {
            RpcShowNotification(text1, text2, color);

            if (_notificationHideCoroutine is { })
            {
                StopCoroutine(_notificationHideCoroutine);
                _notificationHideCoroutine = null;
            }

            _notificationHideCoroutine = StartCoroutine(HideNotificationAfterDelay(_notificationDisplayDuration));
        }

        [Server]
        private IEnumerator HideNotificationAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            _notificationHideCoroutine = null;
            RpcHideNotification();
        }

        #endregion


        #region Client - Notification

        [ClientRpc]
        private void RpcShowNotification(string text1, string text2, Color color) => _notificationView.Show(text1, text2, color);

        [ClientRpc]
        private void RpcHideNotification() => _notificationView.Hide();

        #endregion


        #region Server - Game End

        [Server]
        private IEnumerator HandlePlayerWin()
        {
            yield return _waitForSeconds0_75;

            if (_turnTimerCoroutine is { })
            {
                StopCoroutine(_turnTimerCoroutine);
                _turnTimerCoroutine = null;
            }

            if (_gameTimerCoroutine is { })
            {
                StopCoroutine(_gameTimerCoroutine);
                _gameTimerCoroutine = null;
            }

            var finalScores = CalculateFinalScores();   // <-- called here

            RpcShowGameEndScreen(finalScores.ToArray());

            _playerRegistry.Clear();
            _turnState.Clear();
            _playerData.Clear();
        }

        [Server]
        private void EndMatchByTimeout()
        {
            if (_turnTimerCoroutine is { })
            {
                StopCoroutine(_turnTimerCoroutine);
                _turnTimerCoroutine = null;
            }

            _gameTimerCoroutine = null;

            var finalScores = CalculateFinalScores();  

            RpcShowGameEndScreen(finalScores.ToArray());

            _playerRegistry.Clear();
            _turnState.Clear();
            _playerData.Clear();
        }

        [Server]
        private List<GameEndPlayerInfo> CalculateFinalScores()
        {
            List<GameEndPlayerInfo> finalScores = new();

            foreach(var i in _playerRegistry.AllHands)
            {
                var winnerScore = 0;

                foreach (var j in i.Value)
                {
                    var points = j.Type switch
                    {
                        CardType.Number => 7,
                        CardType.Skip => 20,
                        CardType.Reverse => 20,
                        CardType.DrawTwo => 20,
                        CardType.Wild => 50,
                        CardType.WildDrawFour => 50,
                        _ => 0
                    };

                    winnerScore += points;
                }

                var playerName = _playerData.TryGetValue(i.Key, out var info) ? info.playerName : "Unknown";

                finalScores.Add(new GameEndPlayerInfo
                    {
                        PlayerName = playerName,
                        PlayerScore = winnerScore
                    }
                );
            }

            return finalScores;
        }

        #endregion


        #region Client - Game End

        [ClientRpc]
        private void RpcShowGameEndScreen(GameEndPlayerInfo[] standings)
        {
            _gameEndManager.ShowGameEndScreen(standings);
        }

        #endregion


        #region Client - Card Display

        [Client]
        public void ShowDealtCards(
            GameCard[] cards,
            bool applyStartDelay = false)
        {
            StartCoroutine(
                ShowDealtCardsStaggered(
                    cards,
                    applyStartDelay
                        ? 0.5f
                        : 0f
                )
            );
        }

        private IEnumerator ShowDealtCardsStaggered(
            GameCard[] cards,
            float startDelay)
        {
            if (startDelay > 0f)
            {
                yield return
                    new WaitForSeconds(startDelay);
            }

            var views = new List<Card>();

            foreach (var card in cards)
            {
                var cardView =
                    Instantiate(
                        _cardPrefab,
                        _handContainer
                    );

                cardView.Setup(card);

                views.Add(cardView);
            }

            LayoutRebuilder
                .ForceRebuildLayoutImmediate(
                    (RectTransform)_handContainer
                );

            foreach (var cardView in views)
            {
                cardView.CaptureDealTarget();
            }

            foreach (var cardView in views)
            {
                cardView.PrepareDeal(
                    _cardDrawGameobject.transform
                );
            }

            foreach (var cardView in views)
            {
                cardView.AnimateDeal();

                yield return
                    _waitForSeconds0_12;
            }

            RefreshHandInteractability(
                IsMyTurn()
            );
        }

        [Client]
        public void OnDrawnCardReceived(
            bool canPlay,
            GameCard drawnCard)
        {
            if (!canPlay)
            {
                RefreshHandInteractability(
                    false
                );

                return;
            }

            _awaitingDrawnCardDecision = true;

            _passButtonCanvasGroup.alpha = 1f;
            _passButtonCanvasGroup.interactable = true;

            foreach (Transform item
                     in _handContainer)
            {
                if (!item.TryGetComponent<Card>(
                        out var card))
                {
                    continue;
                }

                var isDrawnCard =
                    card.CardData.Id ==
                    drawnCard.Id;

                var isPlayable =
                    IsValidPlay(
                        card.CardData
                    );

                card.SetInteractable(
                    isDrawnCard &&
                    isPlayable
                );
            }
        }

        #endregion


        #region Client - Countdown

        [ClientRpc]
        private void RpcPlayStartCountdown()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            _countdownCoroutine = StartCoroutine(StartCountdownRoutine());
        }

        [Client]
        private IEnumerator StartCountdownRoutine()
        {
            SetCanvasGroupVisible(_countdownPanel, true);

            string[] steps =
            {
                "3",
                "2",
                "1",
                "GO!"
            };

            foreach (var step in steps)
            {
                yield return StartCoroutine(PlayCountdownStep(step));
            }

            SetCanvasGroupVisible(_countdownPanel, false);

            _countdownCoroutine = null;
        }

        [Client]
        private IEnumerator PlayCountdownStep(string text)
        {
            if (_countdownText == null)
                yield break;

            const float startScale = 1.6f;
            const float endScale = 1f;

            _countdownText.text = text;

            var rect = _countdownText.rectTransform;

            rect.localScale = Vector3.one * startScale;

            var elapsed = 0f;

            while (elapsed <  _countdownStepDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / _countdownStepDuration);

                float scale = Mathf.Lerp(startScale, endScale, 1f - (1f - t) * (1f - t));

                rect.localScale = Vector3.one * scale;

                yield return null;
            }

            rect.localScale = Vector3.one * endScale;
        }

        #endregion


        #region Client RPC - Card State

        [ClientRpc]
        private void RpcShowTopDiscard(GameCard card, int drawCount)
        {
            _syncedTopDiscard = card;

            if (Card.CardSprites.TryGetValue(card.Id, out var sprite))
            {
                _topDiscardImage.color = new Color(1f, 1f, 1f, 1f);

                _topDiscardImage.sprite = sprite;
            }

            _drawPileCountText.text = $"{drawCount} left";
        }

        #endregion


        #region Sync Callbacks

        private void OnCurrentPlayerChanged(uint oldNetId, uint newNetId)
        {
            _awaitingDrawnCardDecision = false;

            _passButtonCanvasGroup.alpha = 0f;
            _passButtonCanvasGroup.interactable = false;

            if (NetworkClient.localPlayer is not { netId: var selfNetId })
            {
                return;
            }

            if (newNetId == selfNetId)
            {
                _currentPlayerNameText.SetText("you");

                _canvasGroup.interactable = true;

                _canvasGroup.blocksRaycasts = true;

                RefreshHandInteractability(true);
            }
            else
            {
                if (_playerData.TryGetValue(newNetId, out var currentPlayerInfo))
                {
                    _currentPlayerNameText.SetText(currentPlayerInfo.playerName);
                }

                _canvasGroup.interactable = false;

                _canvasGroup.blocksRaycasts = true;

                RefreshHandInteractability(false);
            }
        }

        private void OnTopDiscardChanged(GameCard oldCard, GameCard newCard)
        {
            _syncedTopDiscard = newCard;

            var isWildCard = newCard.Type is CardType.Wild or CardType.WildDrawFour;

            if (NetworkClient.localPlayer is { netId: var selfNetId } && _currentPlayerNetId == selfNetId)
            {
                RefreshHandInteractability(true);
            }
        }

        private void OnTurnStartTimeChanged(double oldValue, double newValue)
        {
            var isMyTurn =NetworkClient.localPlayer is { netId: var selfNetId } && _currentPlayerNetId == selfNetId;

            _isTimerRunningLocally = isMyTurn;

            if (_turnTimerRingImage != null)
            {
                _turnTimerRingImage.fillAmount = 1f;
            }
        }

        private void OnGameStartTimeChanged(double oldValue, double newValue)
        {
            _isGameRunningLocally = true;

            var timer = $"{Mathf.FloorToInt(_gameTimeLimit / 60f):00}:" +
                    $"{Mathf.FloorToInt(_gameTimeLimit % 60f):00}";

            _gameTimerText.SetText($"{timer}");
        }

        private void OnPlayerDataChanged(SyncIDictionary<uint, PlayerGameInfo>.Operation op, uint netId, PlayerGameInfo data)
        {
            if (!_netIdToGuiIndex.TryGetValue(netId, out int index))
            {
                return;
            }

            if (_playerData.TryGetValue(netId, out var updatedData))
            {
                _playerGUIs[index].UpdateCardCount(updatedData.cardCount);
            }
        }

        #endregion


        #region Client - Turn / Card Validation

        [Client]
        public bool IsMyTurn()
        {
            return NetworkClient.localPlayer
                       is { netId: var netId }
                   && _currentPlayerNetId ==
                   netId;
        }

        public void RefreshHandInteractability(
            bool isMyTurn)
        {
            foreach (Transform item
                     in _handContainer)
            {
                if (!item.TryGetComponent<Card>(
                        out var card))
                {
                    continue;
                }

                card.SetInteractable(
                    isMyTurn &&
                    IsValidPlay(
                        card.CardData
                    )
                );
            }
        }

        private bool IsValidPlay(GameCard card)
        {
            if (card.Type is CardType.Wild
                || card.Type is CardType.WildDrawFour)
            {
                return true;
            }

            if (card.Color ==
                _syncedTopDiscard.Color)
            {
                return true;
            }

            if (card.Type is CardType.Number
                && _syncedTopDiscard.Type
                is CardType.Number)
            {
                return card.FaceValue ==
                       _syncedTopDiscard.FaceValue;
            }

            return card.Type ==
                   _syncedTopDiscard.Type;
        }

        #endregion


        #region Utility

        private Color ToUnityColor(
            CardColor color)
        {
            return color switch
            {
                CardColor.Red =>
                    _redColor,

                CardColor.Yellow =>
                    _yellowColor,

                CardColor.Green =>
                    _greenColor,

                CardColor.Blue =>
                    _blueColor,

                _ =>
                    Color.white
            };
        }

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