using System;
using System.Collections;
using System.Collections.Generic;
using ElementalLudo.Board;
using ElementalLudo.DiceSystem;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LudoGameController : MonoBehaviour
    {
        private static readonly string[] DefaultTurnOrder =
        {
            "red",
            "blue",
            "yellow",
            "green"
        };

        [Header("Scene References")]
        [SerializeField] private Dice dice;
        [Tooltip("Must implement IPlayerController (e.g. HumanPlayerController). Left empty, a HumanPlayerController is found or created automatically.")]
        [SerializeField] private MonoBehaviour playerControllerSource;
        [Tooltip("Must implement IPlayerController (e.g. AIPlayerController). Left empty, an AIPlayerController is found or created automatically.")]
        [SerializeField] private MonoBehaviour aiControllerSource;
        [SerializeField] private LudoReachableCellsHighlighter reachableCellsHighlighter;
        [SerializeField] private LudoTokenGroundMarkers tokenGroundMarkers;
        [SerializeField] private LudoBoardPresenter boardPresenter;
        [SerializeField] private LudoCombatArena combatArena;

        [Header("Mode")]
        [Tooltip("Offered as the default when the start menu opens. The menu is what actually decides the match.")]
        [SerializeField] private LudoGameMode defaultMode = LudoGameMode.Adventure;

        [Header("Turn Behaviour")]
        [SerializeField] private bool autoExecuteSingleAction = true;
        [Min(0f)]
        [SerializeField] private float noMoveMessageDuration = 1.1f;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float movementStepDuration = 0.11f;

        [Header("Adventure Combat")]
        [Min(0f)]
        [Tooltip("How long the final result stays on screen. AI-versus-AI duels never show and ignore all of these.")]
        [SerializeField] private float combatDisplayDuration = 2.2f;
        [Min(0f)]
        [Tooltip("Pause before each side starts its turn, so the throw reads as an event.")]
        [SerializeField] private float combatThrowDelay = 0.6f;
        [Min(0f)]
        [Tooltip("Pause between each AI reroll, so the player can follow what it kept.")]
        [SerializeField] private float combatRerollDelay = 0.45f;

        private const int MaxMoveHistoryEntries = 30;
        private const float SharedCellOffsetMagnitude = 0.55f;

        private readonly List<LudoPlayerState> players = new List<LudoPlayerState>(4);
        private readonly List<LudoLegalAction> legalActions =
            new List<LudoLegalAction>(4);
        private readonly Dictionary<Token, Vector3> homePositions =
            new Dictionary<Token, Vector3>(16);
        private readonly List<string> moveHistory = new List<string>(MaxMoveHistoryEntries);
        private readonly List<Token> trackTokenBuffer = new List<Token>(16);
        private readonly List<LudoHighlightCell> highlightBuffer =
            new List<LudoHighlightCell>(4);

        // One controller per seat, indexed alongside `players`. Hot-seat
        // points every entry at the same human controller.
        private readonly List<IPlayerController> playerControllers =
            new List<IPlayerController>(4);
        private readonly List<ControllerSubscription> subscriptions =
            new List<ControllerSubscription>(4);

        private IPlayerController humanController;
        private IPlayerController aiController;
        private BoardState boardState;
        private bool awaitingSetup;

        private LudoMatchSettings settings;

        // Seeded from the match settings, but still flippable from the debug
        // panel mid-game, so it can't just be read off `settings`.
        private bool elementalModeEnabled;

        /// <summary>Seat the human holds, or -1 in hot-seat where they all are.</summary>
        private int humanSeatIndex = -1;
        private bool humanDefeated;

        // Results of the capture step, which is a coroutine now that a duel
        // can pause it, so it can't just return them.
        private bool captureAwardedBonus;
        private bool captureRepelledAttacker;
        private bool combatVisible;
        private LudoCombatReport combatReport;
        private LudoCombatSession combatSession;
        private bool combatTurnConfirmed;
        private int activePlayerIndex;
        private int rolledValue;
        private int pendingBonusDistance;
        private bool actionPerformed;
        private bool autoRoll;
        private bool initialized;
        private bool extraRollAfterRewards;
        private int consecutiveSixes;
        private Token lastMovedToken;

        /// <summary>Token the active seat is pointing at, before committing.</summary>
        private Token selectedToken;
        private PlayerStyle winner;
        private string statusMessage = string.Empty;
        private LudoTurnPhase phase = LudoTurnPhase.AwaitingRoll;

        public int ActivePlayerIndex => activePlayerIndex;
        public PlayerStyle ActivePlayer =>
            initialized ? players[activePlayerIndex].Style : null;
        public int RolledValue => rolledValue;
        /// <summary>The exact distance currently offered by the die or reward.</summary>
        public int ActionMoveDistance => pendingBonusDistance > 0
            ? pendingBonusDistance
            : rolledValue;
        public bool IsBonusMove => pendingBonusDistance > 0;
        public LudoTurnPhase Phase => phase;
        public IReadOnlyList<LudoLegalAction> LegalActions => legalActions;
        public IReadOnlyList<string> MoveHistory => moveHistory;
        public PlayerStyle Winner => winner;
        public bool IsGameOver => winner != null || humanDefeated;

        /// <summary>Hardcore only: the player ran out of tokens.</summary>
        public bool HumanDefeated => humanDefeated;

        /// <summary>True while a duel the player is involved in is on screen.</summary>
        public bool IsCombatVisible => combatVisible;
        public LudoCombatReport CombatReport => combatReport;

        /// <summary>The duel in progress, or null once it has resolved.</summary>
        public LudoCombatSession CombatSession => combatSession;

        public bool IsInitialized => initialized;
        public bool IsDiceRolling => dice != null && dice.IsRolling;
        public LudoGameMode DefaultMode => defaultMode;
        public LudoMatchSettings Settings => settings;

        /// <summary>True while the game is held waiting for an element pick.</summary>
        public bool AwaitingSetup => awaitingSetup;

        /// <summary>Seats in turn order, for UI that needs each one's element and color.</summary>
        public IReadOnlyList<LudoPlayerState> Players => players;

        /// <summary>False while an AI seat is taking its turn.</summary>
        public bool IsActiveSeatHuman =>
            initialized && ActiveController != null && ActiveController == humanController;
        public string StatusMessage => statusMessage;
        public bool AutoRoll
        {
            get => autoRoll;
            set => autoRoll = value;
        }
        public bool ElementalModeEnabled
        {
            get => elementalModeEnabled;
            set => elementalModeEnabled = value;
        }

        private void Awake()
        {
            if (dice == null)
            {
                dice = FindFirstObjectByType<Dice>();
            }

            EnsurePlayerController();
            EnsureReachableCellsHighlighter();
            EnsureTokenGroundMarkers();
        }

        /// <summary>
        /// Creates the board presenter if the scene has none. Without this the
        /// mode's look was silently never applied, since nothing in the scene
        /// carries this component.
        /// </summary>
        private void EnsureBoardPresenter()
        {
            if (boardPresenter != null)
            {
                return;
            }

            boardPresenter = FindFirstObjectByType<LudoBoardPresenter>();
            if (boardPresenter != null)
            {
                return;
            }

            GameObject presenterObject = new GameObject("BoardPresenter")
            {
                hideFlags = HideFlags.DontSave
            };
            presenterObject.transform.SetParent(transform, false);
            boardPresenter = presenterObject.AddComponent<LudoBoardPresenter>();
        }

        private void EnsureCombatArena()
        {
            if (combatArena == null)
            {
                combatArena = FindFirstObjectByType<LudoCombatArena>();
            }

            if (combatArena == null)
            {
                GameObject arenaObject = new GameObject("CombatArena")
                {
                    hideFlags = HideFlags.DontSave
                };
                arenaObject.transform.SetParent(transform, false);
                combatArena = arenaObject.AddComponent<LudoCombatArena>();
            }

            // Re-pointed on every duel rather than only on creation: the arena
            // outlives a match, and a delegate lost to a domain reload would
            // leave the dice looking clickable but inert.
            combatArena.RerollRequested = index => RequestCombatReroll(index);
        }

        private void EnsureTokenGroundMarkers()
        {
            if (tokenGroundMarkers != null)
            {
                return;
            }

            tokenGroundMarkers = FindFirstObjectByType<LudoTokenGroundMarkers>();
            if (tokenGroundMarkers != null)
            {
                return;
            }

            GameObject markersObject = new GameObject("TokenGroundMarkers")
            {
                hideFlags = HideFlags.DontSave
            };
            markersObject.transform.SetParent(transform, false);
            tokenGroundMarkers = markersObject.AddComponent<LudoTokenGroundMarkers>();
        }

        private void EnsurePlayerController()
        {
            if (playerControllerSource != null)
            {
                humanController = playerControllerSource as IPlayerController;
                if (humanController != null)
                {
                    return;
                }

                Debug.LogError(
                    $"{playerControllerSource.name} does not implement IPlayerController.",
                    this);
            }

            HumanPlayerController found =
                FindFirstObjectByType<HumanPlayerController>();
            if (found == null)
            {
                GameObject controllerObject = new GameObject("HumanPlayerController")
                {
                    hideFlags = HideFlags.DontSave
                };
                controllerObject.transform.SetParent(transform, false);
                found = controllerObject.AddComponent<HumanPlayerController>();
            }

            playerControllerSource = found;
            humanController = found;
        }

        private void EnsureReachableCellsHighlighter()
        {
            if (reachableCellsHighlighter != null)
            {
                return;
            }

            reachableCellsHighlighter = FindFirstObjectByType<LudoReachableCellsHighlighter>();
            if (reachableCellsHighlighter != null)
            {
                return;
            }

            GameObject highlightObject = new GameObject("ReachableCellsHighlighter")
            {
                hideFlags = HideFlags.DontSave
            };
            highlightObject.transform.SetParent(transform, false);
            reachableCellsHighlighter = highlightObject.AddComponent<LudoReachableCellsHighlighter>();
        }

        private void OnEnable()
        {
            if (dice != null)
            {
                dice.Rolled += HandleDiceRolled;
            }

            SubscribePlayerControllers();
        }

        private void Start()
        {
            initialized = TryBuildPlayers();
            if (!initialized)
            {
                if (dice != null)
                {
                    dice.SetRollEnabled(false);
                }

                enabled = false;
                return;
            }

            // Seats can't be handed out before the menu says who is playing
            // what, so nothing starts until StartMatch arrives.
            awaitingSetup = true;
            dice.SetRollEnabled(false);
            statusMessage = "Elige un modo de juego.";
        }

        /// <summary>
        /// Drops back to the start menu, abandoning the match in progress.
        /// </summary>
        public void ReturnToMenu()
        {
            if (!initialized)
            {
                return;
            }

            StopAllCoroutines();
            dice.CancelRoll();
            CancelPendingDecisions();
            AbortCombat();

            awaitingSetup = true;
            phase = LudoTurnPhase.AwaitingRoll;
            rolledValue = 0;
            pendingBonusDistance = 0;
            actionPerformed = false;
            extraRollAfterRewards = false;
            selectedToken = null;
            legalActions.Clear();
            ClearReachableCells();
            dice.SetRollEnabled(false);
            statusMessage = "Elige un modo de juego.";
        }

        /// <summary>
        /// Begins a match. Hot-seat gives every seat to the human; the rest
        /// give one seat to the human and the others to the AI.
        /// </summary>
        public void StartMatch(LudoMatchSettings matchSettings)
        {
            if (!initialized)
            {
                return;
            }

            settings = matchSettings;
            elementalModeEnabled = matchSettings.ElementalRules;

            EnsureBoardPresenter();
            boardPresenter.Apply(matchSettings.UsesClassicBoard);

            if (matchSettings.HasAIOpponents)
            {
                EnsureAIController();
                if (aiController is AIPlayerController tunableAI)
                {
                    tunableAI.Difficulty = matchSettings.Difficulty;
                }

                AssignSeatsForSinglePlayer(matchSettings.PlayerSeat);
            }
            else
            {
                humanSeatIndex = -1;
                AssignAllSeatsTo(humanController);
            }

            SubscribePlayerControllers();
            awaitingSetup = false;
            RestartGame();
        }

        private void AssignSeatsForSinglePlayer(LudoElement humanElement)
        {
            int humanSeat = -1;
            for (int index = 0; index < players.Count; index++)
            {
                if (players[index].Element == humanElement)
                {
                    humanSeat = index;
                    break;
                }
            }

            if (humanSeat < 0)
            {
                Debug.LogError(
                    $"No player is configured with element {humanElement}; " +
                    "giving the human the first seat instead.",
                    this);
                humanSeat = 0;
            }

            humanSeatIndex = humanSeat;
            UnsubscribePlayerControllers();
            playerControllers.Clear();
            for (int index = 0; index < players.Count; index++)
            {
                playerControllers.Add(index == humanSeat ? humanController : aiController);
            }
        }

        private void EnsureAIController()
        {
            if (aiController != null)
            {
                return;
            }

            if (aiControllerSource != null)
            {
                aiController = aiControllerSource as IPlayerController;
                if (aiController != null)
                {
                    return;
                }

                Debug.LogError(
                    $"{aiControllerSource.name} does not implement IPlayerController.",
                    this);
            }

            AIPlayerController found = FindFirstObjectByType<AIPlayerController>();
            if (found == null)
            {
                GameObject controllerObject = new GameObject("AIPlayerController")
                {
                    hideFlags = HideFlags.DontSave
                };
                controllerObject.transform.SetParent(transform, false);
                found = controllerObject.AddComponent<AIPlayerController>();
            }

            aiControllerSource = found;
            aiController = found;
        }

        private void OnDisable()
        {
            if (dice != null)
            {
                dice.Rolled -= HandleDiceRolled;
            }

            UnsubscribePlayerControllers();
        }

        /// <summary>The controller holding the seat whose turn it is.</summary>
        private IPlayerController ActiveController =>
            initialized && activePlayerIndex < playerControllers.Count
                ? playerControllers[activePlayerIndex]
                : null;

        /// <summary>
        /// Points every seat at one controller — the hot-seat default, where
        /// a single human plays all four colors.
        /// </summary>
        private void AssignAllSeatsTo(IPlayerController controller)
        {
            UnsubscribePlayerControllers();
            playerControllers.Clear();
            for (int index = 0; index < players.Count; index++)
            {
                playerControllers.Add(controller);
            }
        }

        private void SubscribePlayerControllers()
        {
            if (!initialized)
            {
                return;
            }

            // Seats can share a controller instance, so subscribe once per
            // distinct one and sort out who it belongs to when it fires.
            foreach (IPlayerController controller in playerControllers)
            {
                if (controller == null || IsSubscribed(controller))
                {
                    continue;
                }

                IPlayerController source = controller;
                Action roll = () => HandleRollRequested(source);
                Action<Token> select = token => HandleTokenSelected(source, token);
                Action<Token> pointAt = token => HandleSelectionChanged(source, token);
                controller.RollRequested += roll;
                controller.TokenSelected += select;
                controller.SelectionChanged += pointAt;
                subscriptions.Add(
                    new ControllerSubscription(controller, roll, select, pointAt));
            }
        }

        private bool IsSubscribed(IPlayerController controller)
        {
            foreach (ControllerSubscription subscription in subscriptions)
            {
                if (subscription.Controller == controller)
                {
                    return true;
                }
            }

            return false;
        }

        private void UnsubscribePlayerControllers()
        {
            foreach (ControllerSubscription subscription in subscriptions)
            {
                subscription.Controller.RollRequested -= subscription.Roll;
                subscription.Controller.TokenSelected -= subscription.Select;
                subscription.Controller.SelectionChanged -= subscription.PointAt;
            }

            subscriptions.Clear();
        }

        /// <summary>
        /// Intent only counts from whoever holds the active seat, so a human
        /// clicking during an AI turn can't play that turn for it.
        /// </summary>
        private void HandleRollRequested(IPlayerController source)
        {
            if (source == ActiveController && phase == LudoTurnPhase.AwaitingRoll)
            {
                RequestRoll();
            }
        }

        private void HandleTokenSelected(IPlayerController source, Token token)
        {
            if (source == ActiveController && phase == LudoTurnPhase.AwaitingAction)
            {
                TrySelectToken(token);
            }
        }

        /// <summary>
        /// A controller is pointing at a move without committing yet. Purely
        /// presentation: light its destination up and dim the alternatives.
        /// </summary>
        private void HandleSelectionChanged(IPlayerController source, Token token)
        {
            if (source != ActiveController)
            {
                return;
            }

            selectedToken = token;
            if (phase == LudoTurnPhase.AwaitingAction)
            {
                HighlightReachableCells();
                SetTokenInteractionStates(true);
            }
        }

        private LudoTurnContext BuildTurnContext()
        {
            return new LudoTurnContext(
                boardState,
                players[activePlayerIndex],
                players,
                rolledValue,
                ActionMoveDistance,
                IsBonusMove,
                legalActions,
                BuildRulesContext());
        }

        private void NotifyRollTurn()
        {
            ActiveController?.BeginRollTurn(BuildTurnContext());
        }

        private void NotifyActionTurn()
        {
            ActiveController?.BeginActionTurn(BuildTurnContext());
        }

        /// <summary>
        /// Drops any decision a controller still has in flight, so it can't
        /// land on a turn that no longer exists.
        /// </summary>
        private void CancelPendingDecisions()
        {
            foreach (ControllerSubscription subscription in subscriptions)
            {
                subscription.Controller.CancelTurn();
            }
        }

        [ContextMenu("Restart Game")]
        public void RestartGame()
        {
            if (!initialized || awaitingSetup)
            {
                return;
            }

            StopAllCoroutines();

            // StopAllCoroutines only covers this component; a roll animation
            // lives on the Dice and would otherwise announce its result into
            // the freshly restarted game.
            dice.CancelRoll();
            CancelPendingDecisions();
            AbortCombat();

            foreach (LudoPlayerState player in players)
            {
                foreach (Token token in player.Tokens)
                {
                    // Hardcore hides tokens as it eliminates them, so bring
                    // them all back before resetting.
                    token.gameObject.SetActive(true);
                    boardState.SetHome(token);
                    token.transform.position = homePositions[token];
                    token.SetInteractionState(TokenInteractionState.Normal);
                }
            }

            activePlayerIndex = 0;
            rolledValue = 0;
            pendingBonusDistance = 0;
            actionPerformed = false;
            extraRollAfterRewards = false;
            consecutiveSixes = 0;
            lastMovedToken = null;
            selectedToken = null;
            winner = null;
            humanDefeated = false;
            legalActions.Clear();
            moveHistory.Clear();
            ClearReachableCells();

            // Everyone is back home, so nothing is standing on the track.
            if (tokenGroundMarkers != null)
            {
                tokenGroundMarkers.Clear();
            }

            phase = LudoTurnPhase.AwaitingRoll;
            dice.SetRollEnabled(true);
            SyncDiceAccent();
            statusMessage =
                $"{DisplayName(ActivePlayer.PlayerId)} player's turn. Roll the die.";
            NotifyRollTurn();
        }

        public bool RequestRoll()
        {
            if (!initialized ||
                phase != LudoTurnPhase.AwaitingRoll ||
                actionPerformed)
            {
                return false;
            }

            return dice.TryRoll();
        }

        public bool TrySelectToken(Token token)
        {
            if (!initialized ||
                token == null ||
                phase != LudoTurnPhase.AwaitingAction ||
                actionPerformed)
            {
                return false;
            }

            for (int index = 0; index < legalActions.Count; index++)
            {
                if (legalActions[index].Token == token)
                {
                    return TryExecuteAction(legalActions[index]);
                }
            }

            return false;
        }

        private bool TryBuildPlayers()
        {
            if (dice == null)
            {
                Debug.LogError("Ludo game setup failed: no Dice was found.", this);
                return false;
            }

            Token[] sceneTokens = FindObjectsByType<Token>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            players.Clear();
            homePositions.Clear();

            foreach (string playerId in DefaultTurnOrder)
            {
                List<Token> playerTokens = new List<Token>(4);
                PlayerStyle style = null;
                foreach (Token token in sceneTokens)
                {
                    if (token.OwnerStyle == null ||
                        !string.Equals(
                            token.OwnerStyle.PlayerId,
                            playerId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    style = token.OwnerStyle;
                    playerTokens.Add(token);
                }

                playerTokens.Sort((left, right) =>
                    left.TokenId.CompareTo(right.TokenId));
                if (style == null || playerTokens.Count != 4)
                {
                    Debug.LogError(
                        $"Ludo game setup failed: player '{playerId}' must own exactly four tokens.",
                        this);
                    return false;
                }

                HashSet<int> tokenIds = new HashSet<int>();
                foreach (Token token in playerTokens)
                {
                    if (!tokenIds.Add(token.TokenId))
                    {
                        Debug.LogError(
                            $"Ludo game setup failed: player '{playerId}' has duplicate token IDs.",
                            this);
                        return false;
                    }

                    homePositions[token] = token.transform.position;
                }

                if (!LudoBoardRoutes.TryGetRoute(playerId, out Vector2Int[] route))
                {
                    Debug.LogError(
                        $"Ludo game setup failed: player '{playerId}' has no route.",
                        this);
                    return false;
                }

                players.Add(new LudoPlayerState(style, playerTokens, route));
            }

            if (players.Count != 4)
            {
                return false;
            }

            boardState = new BoardState(players);
            return true;
        }

        private void HandleDiceRolled(int value)
        {
            if (!initialized || phase != LudoTurnPhase.AwaitingRoll)
            {
                return;
            }

            rolledValue = Mathf.Clamp(value, 1, 6);
            dice.SetRollEnabled(false);
            actionPerformed = false;
            pendingBonusDistance = 0;
            extraRollAfterRewards = false;

            LogMove($"Turno de {SpanishColorName(ActivePlayer.PlayerId)}. Tira el dado... {rolledValue}");

            if (rolledValue == 6)
            {
                consecutiveSixes++;
                if (consecutiveSixes >= 3)
                {
                    StartCoroutine(ApplyThreeSixesPenalty());
                    return;
                }
            }
            else
            {
                consecutiveSixes = 0;
            }

            bool barrierBreakForced = CalculateLegalActions();

            if (legalActions.Count == 0)
            {
                phase = LudoTurnPhase.Resolving;
                SetTokenInteractionStates(false);
                statusMessage = "No valid moves.";
                StartCoroutine(EndTurnAfterDelay(false));
                return;
            }

            phase = LudoTurnPhase.AwaitingAction;
            SetTokenInteractionStates(true);

            if (barrierBreakForced)
            {
                statusMessage = "¡6! Debes romper tu barrera.";
                LogMove($"Turno de {SpanishColorName(ActivePlayer.PlayerId)}: el 6 obliga a romper la barrera.");
            }
            else
            {
                statusMessage = legalActions.Count == 1
                    ? "One valid action."
                    : $"Choose one of {legalActions.Count} valid actions.";
            }

            HighlightReachableCells();

            // Only hand the decision over if one is actually still owed —
            // auto-execute may have already resolved the turn.
            if (legalActions.Count == 1 &&
                autoExecuteSingleAction &&
                TryExecuteAction(legalActions[0]))
            {
                return;
            }

            NotifyActionTurn();
        }

        private void HighlightReachableCells()
        {
            if (reachableCellsHighlighter == null)
            {
                return;
            }

            LudoPlayerState player = players[activePlayerIndex];
            Color baseColor = Color.Lerp(ActivePlayer.TokenColor, Color.white, 0.35f);
            bool hasSelection = selectedToken != null;

            highlightBuffer.Clear();
            foreach (LudoLegalAction action in legalActions)
            {
                int routeIndex = action.Type == LudoActionType.LeaveHome
                    ? 0
                    : action.DestinationRouteIndex;

                Color color;
                if (!hasSelection)
                {
                    // Nothing picked yet: every option reads the same.
                    color = baseColor;
                    color.a = 0.8f;
                }
                else if (action.Token == selectedToken)
                {
                    color = Color.Lerp(baseColor, Color.white, 0.25f);
                    color.a = 0.92f;
                }
                else
                {
                    // Still available, just not the one being pointed at.
                    color = Color.Lerp(baseColor, Color.gray, 0.45f);
                    color.a = 0.3f;
                }

                highlightBuffer.Add(
                    new LudoHighlightCell(player.Route[routeIndex], color));
            }

            reachableCellsHighlighter.SetCells(highlightBuffer);
        }

        /// <summary>
        /// The rule flags in force. Elemental mode is read from the runtime
        /// field rather than the settings, so the debug toggle still works
        /// mid-match; permadeath comes from the mode and can't be flipped.
        /// </summary>
        private LudoRulesContext BuildRulesContext()
        {
            return new LudoRulesContext(elementalModeEnabled, settings.Permadeath);
        }

        /// <summary>Tints the die with whoever is about to roll it.</summary>
        private void SyncDiceAccent()
        {
            if (dice != null && ActivePlayer != null)
            {
                dice.SetAccentColor(ActivePlayer.TokenColor);
            }
        }

        private void ClearReachableCells()
        {
            if (reachableCellsHighlighter != null)
            {
                reachableCellsHighlighter.Clear();
            }
        }

        /// <summary>Returns whether rolling a 6 just forced a barrier break (never true for a bonus move).</summary>
        private bool CalculateLegalActions()
        {
            LudoPlayerState player = players[activePlayerIndex];
            if (pendingBonusDistance > 0)
            {
                LudoRulesEngine.CalculateBonusActions(
                    boardState,
                    player,
                    players,
                    pendingBonusDistance,
                    BuildRulesContext(),
                    legalActions);
                return false;
            }

            LudoRulesEngine.CalculateLegalActions(
                boardState,
                player,
                players,
                rolledValue,
                BuildRulesContext(),
                legalActions,
                out bool barrierBreakForced);
            return barrierBreakForced;
        }

        public int GetActionMoveDistance(LudoLegalAction action)
        {
            return action.MoveDistance;
        }

        private bool TryExecuteAction(LudoLegalAction requestedAction)
        {
            if (!ValidateAction(requestedAction, out LudoLegalAction legalAction))
            {
                return false;
            }

            actionPerformed = true;
            selectedToken = null;
            phase = LudoTurnPhase.Resolving;
            SetTokenInteractionStates(false);
            StartCoroutine(ExecuteAction(legalAction));
            return true;
        }

        private bool ValidateAction(
            LudoLegalAction requestedAction,
            out LudoLegalAction legalAction)
        {
            legalAction = default;
            if (!initialized ||
                phase != LudoTurnPhase.AwaitingAction ||
                actionPerformed ||
                requestedAction.Token == null ||
                requestedAction.Token.OwnerStyle != ActivePlayer)
            {
                return false;
            }

            CalculateLegalActions();
            foreach (LudoLegalAction candidate in legalActions)
            {
                if (candidate.Token == requestedAction.Token &&
                    candidate.Type == requestedAction.Type &&
                    candidate.DestinationRouteIndex ==
                    requestedAction.DestinationRouteIndex &&
                    candidate.MoveDistance == requestedAction.MoveDistance)
                {
                    legalAction = candidate;
                    return true;
                }
            }

            return false;
        }

        private IEnumerator ExecuteAction(LudoLegalAction action)
        {
            LudoPlayerState player = players[activePlayerIndex];
            Token token = action.Token;
            bool wasBonusMove = pendingBonusDistance > 0;
            int moveDistance = GetActionMoveDistance(action);
            bool earnedCaptureBonus = false;
            bool reachedGoal = false;
            lastMovedToken = token;

            // Where to put the token back if it loses a duel. Captured before
            // anything moves, since the walk overwrites it.
            bool cameFromHome = action.Type == LudoActionType.LeaveHome;
            int originRouteIndex = cameFromHome
                ? 0
                : boardState.GetRouteIndex(token);

            if (!wasBonusMove)
            {
                extraRollAfterRewards =
                    rolledValue == 6 ||
                    (rolledValue == 5 && action.Type == LudoActionType.LeaveHome);
            }

            if (action.Type == LudoActionType.LeaveHome)
            {
                yield return MoveTokenTo(
                    token,
                    GetRoutePosition(player, token, 0));
                boardState.SetTrack(token, 0);
                yield return ResolveLanding(player, token, true, originRouteIndex);
                earnedCaptureBonus = captureAwardedBonus;
                RepositionTrackTokens();

                if (!captureRepelledAttacker)
                {
                    statusMessage = $"{token.name} entered the starting square.";
                    LogMove(
                        $"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} " +
                        $"sale de casa a {DescribeCell(player, 0)}.");
                    LogBarrierIfFormed(player, token, 0);
                }
            }
            else
            {
                int firstStep = boardState.GetRouteIndex(token) + 1;
                for (int routeIndex = firstStep;
                     routeIndex <= action.DestinationRouteIndex;
                     routeIndex++)
                {
                    yield return MoveTokenTo(
                        token,
                        GetRoutePosition(player, token, routeIndex));
                    boardState.SetTrack(token, routeIndex);
                }

                yield return ResolveLanding(player, token, false, originRouteIndex);
                earnedCaptureBonus = captureAwardedBonus;
                RepositionTrackTokens();

                if (captureRepelledAttacker)
                {
                    // Pushed back, so it neither reached the goal nor formed
                    // anything worth reporting where it briefly stood.
                }
                else if (action.DestinationRouteIndex == player.Route.Length - 1)
                {
                    boardState.SetFinished(token, action.DestinationRouteIndex);
                    reachedGoal = true;
                    statusMessage = $"{token.name} reached the goal.";
                    LogMove($"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} llega a la meta.");
                }
                else
                {
                    statusMessage =
                        $"{token.name} moved {moveDistance} spaces.";
                    LogMove(
                        $"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} " +
                        $"se mueve a {DescribeCell(player, action.DestinationRouteIndex)}.");
                    LogBarrierIfFormed(player, token, action.DestinationRouteIndex);
                }
            }

            // The action that was just resolved no longer owns the bonus
            // slot. A capture or goal below may immediately fill it again.
            pendingBonusDistance = 0;

            if (LudoMovementRules.HasWon(boardState, player.Tokens, BuildRulesContext()))
            {
                EndGame(player);
                yield break;
            }

            // A permanent capture may have just knocked somebody out.
            if (settings.Permadeath && TryResolveEliminations())
            {
                yield break;
            }

            if (earnedCaptureBonus)
            {
                BeginBonusMove(LudoMovementRules.CaptureBonusDistance);
                yield break;
            }

            if (reachedGoal)
            {
                BeginBonusMove(LudoMovementRules.GoalBonusDistance);
                yield break;
            }

            EndTurn(extraRollAfterRewards);
        }

        private void BeginBonusMove(int moveDistance)
        {
            pendingBonusDistance = moveDistance;
            selectedToken = null;
            actionPerformed = false;
            CalculateLegalActions();

            string reason = moveDistance == LudoMovementRules.CaptureBonusDistance
                ? "capture"
                : "goal";

            if (legalActions.Count == 0)
            {
                pendingBonusDistance = 0;
                phase = LudoTurnPhase.Resolving;
                SetTokenInteractionStates(false);
                ClearReachableCells();
                statusMessage =
                    $"No token can move all {moveDistance} bonus spaces.";
                LogMove(
                    $"Premio de {moveDistance} por {reason}: no hay movimiento válido.");
                StartCoroutine(EndTurnAfterDelay(extraRollAfterRewards));
                return;
            }

            phase = LudoTurnPhase.AwaitingAction;
            SetTokenInteractionStates(true);
            statusMessage =
                $"{DisplayName(ActivePlayer.PlayerId)} must move one token " +
                $"exactly {moveDistance} spaces ({reason} bonus).";
            LogMove($"Premio de {moveDistance} casillas por {reason}.");
            HighlightReachableCells();

            if (legalActions.Count == 1 &&
                autoExecuteSingleAction &&
                TryExecuteAction(legalActions[0]))
            {
                return;
            }

            NotifyActionTurn();
        }

        private IEnumerator MoveTokenTo(Token token, Vector3 destination)
        {
            if (movementStepDuration <= Mathf.Epsilon)
            {
                token.transform.position = destination;
                yield break;
            }

            Vector3 start = token.transform.position;
            float elapsed = 0f;
            while (elapsed < movementStepDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / movementStepDuration);
                float easedProgress =
                    progress * progress * (3f - 2f * progress);
                token.transform.position = Vector3.Lerp(
                    start,
                    destination,
                    easedProgress);
                yield return null;
            }

            token.transform.position = destination;
        }

        private IEnumerator ApplyThreeSixesPenalty()
        {
            phase = LudoTurnPhase.Resolving;
            SetTokenInteractionStates(false);
            ClearReachableCells();
            consecutiveSixes = 0;

            if (lastMovedToken != null && boardState.GetState(lastMovedToken) != TokenState.Home)
            {
                boardState.SetHome(lastMovedToken);
                lastMovedToken.transform.position = homePositions[lastMovedToken];
                lastMovedToken.SetInteractionState(TokenInteractionState.Normal);

                // The owner lookup this used to do is unnecessary now: pulling
                // a token off the board can re-centre a rival that was sharing
                // its square, so everyone gets re-seated.
                RepositionTrackTokens();

                statusMessage = "Three sixes in a row! Last moved token returns home.";
            }
            else
            {
                statusMessage = "Three sixes in a row! Turn passes.";
            }

            lastMovedToken = null;
            yield return new WaitForSecondsRealtime(noMoveMessageDuration);
            EndTurn(false);
        }

        private IEnumerator EndTurnAfterDelay(bool bonusTurn)
        {
            if (noMoveMessageDuration > Mathf.Epsilon)
            {
                yield return new WaitForSecondsRealtime(noMoveMessageDuration);
            }

            EndTurn(bonusTurn);
        }

        private void EndTurn(bool bonusTurn)
        {
            legalActions.Clear();
            ClearReachableCells();
            rolledValue = 0;
            pendingBonusDistance = 0;
            actionPerformed = false;
            extraRollAfterRewards = false;
            selectedToken = null;

            if (bonusTurn)
            {
                statusMessage =
                    $"{DisplayName(ActivePlayer.PlayerId)} rolls again!";
            }
            else
            {
                activePlayerIndex = NextActiveSeat(activePlayerIndex);
                consecutiveSixes = 0;
                lastMovedToken = null;
                statusMessage =
                    $"{DisplayName(ActivePlayer.PlayerId)} player's turn. Roll the die.";
            }

            phase = LudoTurnPhase.AwaitingRoll;
            dice.SetRollEnabled(true);
            SyncDiceAccent();
            SetTokenInteractionStates(false, true);
            NotifyRollTurn();

            if (autoRoll)
            {
                StartCoroutine(AutoRollAfterDelay());
            }
        }

        /// <summary>
        /// Next seat in turn order, skipping anyone knocked out. Bounded by
        /// the seat count, so a board where everyone is out returns the
        /// current seat rather than looping forever.
        /// </summary>
        private int NextActiveSeat(int fromIndex)
        {
            for (int step = 1; step <= players.Count; step++)
            {
                int candidate = (fromIndex + step) % players.Count;
                if (!settings.Permadeath ||
                    !LudoMovementRules.IsEliminated(boardState, players[candidate].Tokens))
                {
                    return candidate;
                }
            }

            return fromIndex;
        }

        private IEnumerator AutoRollAfterDelay()
        {
            yield return null;
            RequestRoll();
        }

        private void EndGame(LudoPlayerState winningPlayer)
        {
            winner = winningPlayer.Style;
            CancelPendingDecisions();
            legalActions.Clear();
            ClearReachableCells();
            rolledValue = 0;
            pendingBonusDistance = 0;
            extraRollAfterRewards = false;
            actionPerformed = true;
            phase = LudoTurnPhase.GameOver;
            dice.SetRollEnabled(false);
            SetTokenInteractionStates(false, true);
            statusMessage =
                settings.Permadeath
                    ? $"{DisplayName(winner.PlayerId)} wins!"
                    : $"{DisplayName(winner.PlayerId)} wins! All four tokens reached the goal.";
        }

        private Vector3 GetRoutePosition(
            LudoPlayerState player,
            Token token,
            int routeIndex)
        {
            Vector2Int cell = player.Route[routeIndex];
            Vector2 worldCell = LudoBoardLayout.ToWorld(cell);
            Vector3 position = new Vector3(
                worldCell.x,
                worldCell.y,
                homePositions[token].z);

            if (routeIndex == player.Route.Length - 1)
            {
                Vector3 logicalOffset =
                    GetGoalOffset(player.Style.PlayerId, token.TokenId);
                Vector2 worldOffset = LudoBoardLayout.ToWorld(logicalOffset);
                position += new Vector3(worldOffset.x, worldOffset.y, 0f);
            }
            else
            {
                position += GetSharedCellOffset(token, cell);
            }

            return position;
        }

        /// <summary>
        /// Spreads every token sharing <paramref name="cell"/> so they don't
        /// stack. Counts across all players, not just one: on a safe cell two
        /// different colours can sit together without capturing, and comparing
        /// route indices would never spot that, since each colour numbers the
        /// same square differently.
        ///
        /// Every token on the cell walks the same ordered list, so each one
        /// works out the same layout and claims a different slot.
        /// </summary>
        private Vector3 GetSharedCellOffset(Token token, Vector2Int cell)
        {
            int count = 0;
            int tokenIndex = -1;

            foreach (LudoPlayerState occupant in players)
            {
                foreach (Token other in occupant.Tokens)
                {
                    if (boardState.GetState(other) != TokenState.Track ||
                        occupant.Route[boardState.GetRouteIndex(other)] != cell)
                    {
                        continue;
                    }

                    if (other == token)
                    {
                        tokenIndex = count;
                    }

                    count++;
                }
            }

            // tokenIndex < 0 happens mid-walk, before the board state catches
            // up with the animation: stay centred until it settles.
            if (count <= 1 || tokenIndex < 0)
            {
                return Vector3.zero;
            }

            float baseOffset = (tokenIndex - (count - 1) * 0.5f) * SharedCellOffsetMagnitude;

            // Spread across the track, not along it.
            if (Mathf.Abs(cell.x) > Mathf.Abs(cell.y))
            {
                return new Vector3(0f, baseOffset, 0f);
            }

            return new Vector3(baseOffset, 0f, 0f);
        }

        private Vector2Int GetTokenLogicalCell(Token token)
        {
            TokenState state = boardState.GetState(token);
            if (state != TokenState.Track && state != TokenState.Finished)
            {
                return new Vector2Int(int.MinValue, int.MinValue);
            }

            foreach (LudoPlayerState player in players)
            {
                if (player.Style == token.OwnerStyle)
                {
                    return player.Route[boardState.GetRouteIndex(token)];
                }
            }

            return new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>
        /// Resolves whatever the moving token landed on. A coroutine because
        /// Adventure turns a capture into a duel that has to be watched;
        /// results come back through captureAwardedBonus and
        /// captureRepelledAttacker rather than a return value.
        /// </summary>
        private IEnumerator ResolveLanding(
            LudoPlayerState movingPlayer,
            Token movingToken,
            bool cameFromHome,
            int originRouteIndex)
        {
            captureAwardedBonus = false;
            captureRepelledAttacker = false;

            List<Token> captured = LudoRulesEngine.GetCapturedTokens(
                boardState,
                movingPlayer,
                players,
                movingToken,
                BuildRulesContext());

            if (captured.Count == 0)
            {
                yield break;
            }

            if (settings.Mode == LudoGameMode.Adventure)
            {
                Token defenderToken = captured[0];
                LudoCombatOutcome outcome;

                if (IsHumanInvolvedInCapture(captured))
                {
                    yield return PlayDuel(movingToken, defenderToken);
                    outcome = combatReport.Outcome;
                }
                else
                {
                    // Nobody is watching, so the whole duel collapses into one
                    // call — the same code both sides would have played by
                    // hand, just without the waiting.
                    outcome = LudoCombatResolver.Resolve(
                        movingToken,
                        defenderToken,
                        settings.ElementalRules);
                    combatReport = new LudoCombatReport(
                        movingToken,
                        defenderToken,
                        outcome);
                }

                LogCombat(movingToken, defenderToken, outcome);

                if (!outcome.AttackerWins)
                {
                    yield return RepelAttacker(
                        movingPlayer,
                        movingToken,
                        cameFromHome,
                        originRouteIndex);
                    captureRepelledAttacker = true;
                    yield break;
                }
            }

            captureAwardedBonus = ApplyCaptures(movingPlayer, movingToken, captured);
        }

        /// <summary>
        /// Plays a duel the player is part of, one side at a time: the
        /// attacker finishes its hand, then the defender plays knowing the
        /// score to beat. Human sides wait for input; AI sides spend their
        /// rerolls on a timer so the player can follow what happened.
        /// </summary>
        private IEnumerator PlayDuel(Token attackerToken, Token defenderToken)
        {
            PlayerStyle humanStyle = humanSeatIndex >= 0 && humanSeatIndex < players.Count
                ? players[humanSeatIndex].Style
                : null;

            combatSession = new LudoCombatSession(
                attackerToken,
                defenderToken,
                attackerToken.OwnerStyle == humanStyle,
                defenderToken.OwnerStyle == humanStyle,
                settings.ElementalRules);

            combatVisible = true;
            statusMessage = "¡Duelo de dados!";
            EnsureCombatArena();
            combatArena.Show(combatSession);

            while (combatSession.Phase != LudoCombatPhase.Resolved)
            {
                yield return new WaitForSecondsRealtime(combatThrowDelay);

                if (combatSession.IsHumanTurn)
                {
                    combatTurnConfirmed = false;
                    // Held here until the player runs out of rerolls or says
                    // they're done; the UI flips the flag.
                    yield return new WaitUntil(() => combatTurnConfirmed);
                }
                else
                {
                    LudoCombatHand hand = combatSession.CurrentHand;
                    while (hand != null && hand.CanReroll)
                    {
                        int index = LudoCombatResolver.SuggestReroll(hand.Dice);
                        if (index < 0 || !hand.TryReroll(index))
                        {
                            break;
                        }

                        yield return new WaitForSecondsRealtime(combatRerollDelay);
                    }
                }

                combatSession.EndCurrentTurn();
            }

            combatReport = combatSession.BuildReport();
            yield return new WaitForSecondsRealtime(combatDisplayDuration);

            combatArena.Hide();
            combatVisible = false;
            combatSession = null;
        }

        /// <summary>
        /// Tears the duel down without finishing it. StopAllCoroutines kills
        /// PlayDuel wherever it stands, so restarting or quitting mid-duel
        /// would otherwise leave the arena camera live and the board's parked.
        /// </summary>
        private void AbortCombat()
        {
            if (combatArena != null)
            {
                combatArena.Hide();
            }

            combatSession = null;
            combatVisible = false;
            combatTurnConfirmed = false;
        }

        /// <summary>Called by the UI to spend one of the player's rerolls.</summary>
        public bool RequestCombatReroll(int dieIndex)
        {
            return combatSession != null &&
                   combatSession.IsHumanTurn &&
                   combatSession.TryReroll(dieIndex);
        }

        /// <summary>Called by the UI when the player is done with their hand.</summary>
        public void ConfirmCombatHand()
        {
            if (combatSession != null && combatSession.IsHumanTurn)
            {
                combatTurnConfirmed = true;
            }
        }

        /// <summary>The attacker survives but is pushed back where it came from.</summary>
        private IEnumerator RepelAttacker(
            LudoPlayerState movingPlayer,
            Token movingToken,
            bool cameFromHome,
            int originRouteIndex)
        {
            string attacker =
                $"Token {SpanishColorName(movingToken.OwnerStyle.PlayerId)} {movingToken.TokenId}";

            if (cameFromHome)
            {
                boardState.SetHome(movingToken);
                movingToken.transform.position = homePositions[movingToken];
                statusMessage = $"{movingToken.name} was repelled back home.";
                LogMove($"{attacker} pierde el duelo y vuelve a casa.");
            }
            else
            {
                boardState.SetTrack(movingToken, originRouteIndex);
                yield return MoveTokenTo(
                    movingToken,
                    GetRoutePosition(movingPlayer, movingToken, originRouteIndex));
                statusMessage = $"{movingToken.name} was repelled.";
                LogMove(
                    $"{attacker} pierde el duelo y retrocede a " +
                    $"{DescribeCell(movingPlayer, originRouteIndex)}.");
            }

            movingToken.SetInteractionState(TokenInteractionState.Normal);
        }

        private bool IsHumanInvolvedInCapture(List<Token> captured)
        {
            if (humanSeatIndex < 0 || humanSeatIndex >= players.Count)
            {
                return false;
            }

            if (activePlayerIndex == humanSeatIndex)
            {
                return true;
            }

            PlayerStyle humanStyle = players[humanSeatIndex].Style;
            foreach (Token token in captured)
            {
                if (token.OwnerStyle == humanStyle)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogCombat(
            Token attackerToken,
            Token defenderToken,
            LudoCombatOutcome outcome)
        {
            string attacker =
                $"{SpanishColorName(attackerToken.OwnerStyle.PlayerId)} {attackerToken.TokenId}";
            string defender =
                $"{SpanishColorName(defenderToken.OwnerStyle.PlayerId)} {defenderToken.TokenId}";
            string winner = outcome.AttackerWins ? attacker : defender;

            LogMove(
                $"Duelo: {attacker} [{LudoCombatInfo.Describe(outcome.Attacker)}] vs " +
                $"{defender} [{LudoCombatInfo.Describe(outcome.Defender)}] → gana {winner}.");
        }

        private bool ApplyCaptures(
            LudoPlayerState movingPlayer,
            Token movingToken,
            List<Token> captured)
        {
            bool awardsCaptureBonus = captured.Count > 0 &&
                !LudoBoardRoutes.IsSafeCell(
                    movingPlayer.Route[boardState.GetRouteIndex(movingToken)]);

            foreach (Token token in captured)
            {
                string attacker =
                    $"Token {SpanishColorName(movingToken.OwnerStyle.PlayerId)} {movingToken.TokenId}";
                string victim =
                    $"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId}";

                if (settings.Permadeath)
                {
                    // Gone for good: hiding the object also takes its collider
                    // out of the way, so it can no longer be clicked.
                    boardState.SetEliminated(token);
                    token.gameObject.SetActive(false);
                    statusMessage = $"{movingToken.name} destroyed {token.name}!";
                    LogMove($"{attacker} ELIMINA a {victim}. No volverá.");
                    continue;
                }

                boardState.SetHome(token);
                token.transform.position = homePositions[token];
                token.SetInteractionState(TokenInteractionState.Normal);
                statusMessage = $"{movingToken.name} captured {token.name}!";
                LogMove($"{attacker} captura a {victim}.");
            }

            return awardsCaptureBonus;
        }

        /// <summary>
        /// Hardcore bookkeeping after a permanent capture. Ends the match if
        /// the player is wiped out, or if only one seat still has tokens.
        /// Returns whether the match ended.
        /// </summary>
        private bool TryResolveEliminations()
        {
            if (humanSeatIndex >= 0 &&
                LudoMovementRules.IsEliminated(boardState, players[humanSeatIndex].Tokens))
            {
                EndGameAsDefeat();
                return true;
            }

            LudoPlayerState survivor = null;
            int aliveCount = 0;
            foreach (LudoPlayerState player in players)
            {
                if (!LudoMovementRules.IsEliminated(boardState, player.Tokens))
                {
                    aliveCount++;
                    survivor = player;
                }
            }

            if (aliveCount == 1 && survivor != null)
            {
                EndGame(survivor);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Hardcore loss: nobody won, the player simply ran out of tokens.
        /// </summary>
        private void EndGameAsDefeat()
        {
            humanDefeated = true;
            winner = null;
            CancelPendingDecisions();
            legalActions.Clear();
            ClearReachableCells();
            rolledValue = 0;
            pendingBonusDistance = 0;
            extraRollAfterRewards = false;
            actionPerformed = true;
            selectedToken = null;
            phase = LudoTurnPhase.GameOver;
            dice.SetRollEnabled(false);
            SetTokenInteractionStates(false, true);
            statusMessage = "You lost every token.";
            LogMove("Te has quedado sin fichas. Fin de la partida.");
        }

        /// <summary>
        /// Re-seats every token on the track. Has to cover all players, not
        /// just the one that moved: arriving on an occupied square shifts
        /// whoever was already standing there, and leaving one re-centres
        /// whoever stays behind.
        /// </summary>
        private void RepositionTrackTokens()
        {
            trackTokenBuffer.Clear();

            foreach (LudoPlayerState player in players)
            {
                foreach (Token token in player.Tokens)
                {
                    if (boardState.GetState(token) == TokenState.Track)
                    {
                        token.transform.position = GetRoutePosition(
                            player, token, boardState.GetRouteIndex(token));
                        trackTokenBuffer.Add(token);
                    }
                }
            }

            // Same trigger points: whoever is out on the board just changed.
            if (tokenGroundMarkers != null)
            {
                tokenGroundMarkers.SetTokens(trackTokenBuffer);
            }
        }

        private static Vector3 GetGoalOffset(string playerId, int tokenId)
        {
            float tokenOffset = (tokenId - 1.5f) * 0.14f;
            if (string.Equals(playerId, "red", StringComparison.OrdinalIgnoreCase))
            {
                return new Vector3(tokenOffset, 0.48f, 0f);
            }

            if (string.Equals(playerId, "blue", StringComparison.OrdinalIgnoreCase))
            {
                return new Vector3(0.48f, -tokenOffset, 0f);
            }

            if (string.Equals(playerId, "yellow", StringComparison.OrdinalIgnoreCase))
            {
                return new Vector3(-tokenOffset, -0.48f, 0f);
            }

            return new Vector3(-0.48f, tokenOffset, 0f);
        }

        private void SetTokenInteractionStates(
            bool showLegalActions,
            bool resetToNormal = false)
        {
            foreach (LudoPlayerState player in players)
            {
                foreach (Token token in player.Tokens)
                {
                    TokenInteractionState state = resetToNormal
                        ? TokenInteractionState.Normal
                        : TokenInteractionState.Disabled;

                    if (showLegalActions)
                    {
                        foreach (LudoLegalAction action in legalActions)
                        {
                            if (action.Token == token)
                            {
                                state = token == selectedToken
                                    ? TokenInteractionState.Selected
                                    : TokenInteractionState.Selectable;
                                break;
                            }
                        }
                    }

                    token.SetInteractionState(state);
                }
            }
        }

        public static string DisplayName(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return "Unknown";
            }

            return char.ToUpperInvariant(playerId[0]) + playerId.Substring(1);
        }

        /// <summary>
        /// Keeps a controller together with the exact delegates it was
        /// subscribed with, so they can be removed again later.
        /// </summary>
        private readonly struct ControllerSubscription
        {
            public IPlayerController Controller { get; }
            public Action Roll { get; }
            public Action<Token> Select { get; }
            public Action<Token> PointAt { get; }

            public ControllerSubscription(
                IPlayerController controller,
                Action roll,
                Action<Token> select,
                Action<Token> pointAt)
            {
                Controller = controller;
                Roll = roll;
                Select = select;
                PointAt = pointAt;
            }
        }

        public static string SpanishColorName(string playerId)
        {
            if (string.Equals(playerId, "red", StringComparison.OrdinalIgnoreCase))
            {
                return "Rojo";
            }

            if (string.Equals(playerId, "blue", StringComparison.OrdinalIgnoreCase))
            {
                return "Azul";
            }

            if (string.Equals(playerId, "yellow", StringComparison.OrdinalIgnoreCase))
            {
                return "Amarillo";
            }

            if (string.Equals(playerId, "green", StringComparison.OrdinalIgnoreCase))
            {
                return "Verde";
            }

            return DisplayName(playerId);
        }

        private void LogMove(string message)
        {
            moveHistory.Insert(0, message);
            if (moveHistory.Count > MaxMoveHistoryEntries)
            {
                moveHistory.RemoveAt(moveHistory.Count - 1);
            }
        }

        /// <summary>
        /// How a square reads in the log: the number painted on the board, or
        /// a plain description for the squares that carry no number.
        /// </summary>
        private static string DescribeCell(LudoPlayerState player, int routeIndex)
        {
            return LudoBoardRoutes.TryGetCellLabel(player.Route[routeIndex], out int label)
                ? $"la casilla {label}"
                : "su pasillo final";
        }

        private void LogBarrierIfFormed(LudoPlayerState player, Token token, int routeIndex)
        {
            Vector2Int cell = player.Route[routeIndex];

            // Only the shared path is worth reporting: two of your own tokens
            // sitting together in your own final lane block nobody.
            if (!LudoBoardRoutes.TryGetCellLabel(cell, out int label))
            {
                return;
            }

            if (LudoRulesEngine.CountSameColorTokensOnCell(boardState, player, cell) == 2)
            {
                LogMove(
                    $"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} " +
                    $"forma una barrera en la casilla {label}.");
            }
        }
    }
}
