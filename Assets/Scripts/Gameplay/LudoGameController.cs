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
        [SerializeField] private LudoReachableCellsHighlighter reachableCellsHighlighter;

        [Header("Turn Behaviour")]
        [SerializeField] private bool autoExecuteSingleAction = true;
        [Min(0f)]
        [SerializeField] private float noMoveMessageDuration = 1.1f;
        [SerializeField] private bool elementalModeEnabled;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float movementStepDuration = 0.11f;

        private const int MaxMoveHistoryEntries = 30;

        private readonly List<LudoPlayerState> players = new List<LudoPlayerState>(4);
        private readonly List<LudoLegalAction> legalActions =
            new List<LudoLegalAction>(4);
        private readonly Dictionary<Token, Vector3> homePositions =
            new Dictionary<Token, Vector3>(16);
        private readonly List<string> moveHistory = new List<string>(MaxMoveHistoryEntries);

        private IPlayerController playerController;
        private BoardState boardState;
        private int activePlayerIndex;
        private int rolledValue;
        private bool actionPerformed;
        private bool autoRoll;
        private bool initialized;
        private bool captureHappenedThisTurn;
        private int consecutiveSixes;
        private Token lastMovedToken;
        private PlayerStyle winner;
        private string statusMessage = string.Empty;
        private LudoTurnPhase phase = LudoTurnPhase.AwaitingRoll;

        public int ActivePlayerIndex => activePlayerIndex;
        public PlayerStyle ActivePlayer =>
            initialized ? players[activePlayerIndex].Style : null;
        public int RolledValue => rolledValue;
        public LudoTurnPhase Phase => phase;
        public IReadOnlyList<LudoLegalAction> LegalActions => legalActions;
        public IReadOnlyList<string> MoveHistory => moveHistory;
        public PlayerStyle Winner => winner;
        public bool IsGameOver => winner != null;
        public bool IsInitialized => initialized;
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
        }

        private void EnsurePlayerController()
        {
            if (playerControllerSource != null)
            {
                playerController = playerControllerSource as IPlayerController;
                if (playerController != null)
                {
                    return;
                }

                Debug.LogError(
                    $"{playerControllerSource.name} does not implement IPlayerController.",
                    this);
            }

            HumanPlayerController humanController =
                FindFirstObjectByType<HumanPlayerController>();
            if (humanController == null)
            {
                GameObject controllerObject = new GameObject("HumanPlayerController")
                {
                    hideFlags = HideFlags.DontSave
                };
                controllerObject.transform.SetParent(transform, false);
                humanController = controllerObject.AddComponent<HumanPlayerController>();
            }

            playerControllerSource = humanController;
            playerController = humanController;
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

            if (playerController != null)
            {
                playerController.RollRequested += HandleRollRequested;
                playerController.TokenSelected += HandleTokenSelected;
            }
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

            RestartGame();
        }

        private void OnDisable()
        {
            if (dice != null)
            {
                dice.Rolled -= HandleDiceRolled;
            }

            if (playerController != null)
            {
                playerController.RollRequested -= HandleRollRequested;
                playerController.TokenSelected -= HandleTokenSelected;
            }
        }

        private void HandleRollRequested()
        {
            if (phase == LudoTurnPhase.AwaitingRoll)
            {
                RequestRoll();
            }
        }

        private void HandleTokenSelected(Token token)
        {
            if (phase == LudoTurnPhase.AwaitingAction)
            {
                TrySelectToken(token);
            }
        }

        [ContextMenu("Restart Game")]
        public void RestartGame()
        {
            if (!initialized)
            {
                return;
            }

            StopAllCoroutines();
            foreach (LudoPlayerState player in players)
            {
                foreach (Token token in player.Tokens)
                {
                    boardState.SetHome(token);
                    token.transform.position = homePositions[token];
                    token.SetInteractionState(TokenInteractionState.Normal);
                }
            }

            activePlayerIndex = 0;
            rolledValue = 0;
            actionPerformed = false;
            captureHappenedThisTurn = false;
            consecutiveSixes = 0;
            lastMovedToken = null;
            winner = null;
            legalActions.Clear();
            moveHistory.Clear();
            ClearReachableCells();
            phase = LudoTurnPhase.AwaitingRoll;
            dice.SetRollEnabled(true);
            statusMessage =
                $"{DisplayName(ActivePlayer.PlayerId)} player's turn. Roll the die.";
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
            captureHappenedThisTurn = false;

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

            CalculateLegalActions();

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
            statusMessage = legalActions.Count == 1
                ? "One valid action."
                : $"Choose one of {legalActions.Count} valid actions.";

            HighlightReachableCells();

            if (legalActions.Count == 1 && autoExecuteSingleAction)
            {
                TryExecuteAction(legalActions[0]);
            }
        }

        private void HighlightReachableCells()
        {
            if (reachableCellsHighlighter == null)
            {
                return;
            }

            LudoPlayerState player = players[activePlayerIndex];
            HashSet<Vector2Int> reachableCells = new HashSet<Vector2Int>();
            foreach (LudoLegalAction action in legalActions)
            {
                Vector2Int cell = action.Type == LudoActionType.LeaveHome
                    ? player.Route[0]
                    : player.Route[action.DestinationRouteIndex];
                reachableCells.Add(cell);
            }

            Color highlightColor = Color.Lerp(
                ActivePlayer.TokenColor,
                Color.white,
                0.25f);
            highlightColor.a = 0.5f;
            reachableCellsHighlighter.SetCells(reachableCells, highlightColor);
        }

        private void ClearReachableCells()
        {
            if (reachableCellsHighlighter != null)
            {
                reachableCellsHighlighter.Clear();
            }
        }

        private void CalculateLegalActions()
        {
            LudoPlayerState player = players[activePlayerIndex];
            LudoRulesEngine.CalculateLegalActions(
                boardState,
                player,
                players,
                rolledValue,
                new LudoRulesContext(elementalModeEnabled),
                legalActions);
        }

        private bool TryExecuteAction(LudoLegalAction requestedAction)
        {
            if (!ValidateAction(requestedAction, out LudoLegalAction legalAction))
            {
                return false;
            }

            actionPerformed = true;
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
                    requestedAction.DestinationRouteIndex)
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
            lastMovedToken = token;

            if (action.Type == LudoActionType.LeaveHome)
            {
                yield return MoveTokenTo(
                    token,
                    GetRoutePosition(player, token, 0));
                boardState.SetTrack(token, 0);
                CaptureOpponentTokensOnCell(player, token);
                RepositionSameColorTokens(player);
                statusMessage = $"{token.name} entered the starting square.";
                LogMove($"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} sale de casa.");
                LogBarrierIfFormed(player, token, 0);
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

                CaptureOpponentTokensOnCell(player, token);
                RepositionSameColorTokens(player);

                if (action.DestinationRouteIndex == player.Route.Length - 1)
                {
                    boardState.SetFinished(token, action.DestinationRouteIndex);
                    statusMessage = $"{token.name} reached the goal.";
                    LogMove($"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} llega a la meta.");
                }
                else
                {
                    statusMessage =
                        $"{token.name} moved {rolledValue} spaces.";
                    LogMove($"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} se mueve a la casilla {action.DestinationRouteIndex}.");
                    LogBarrierIfFormed(player, token, action.DestinationRouteIndex);
                }
            }

            if (LudoMovementRules.HasWon(boardState, player.Tokens))
            {
                EndGame(player);
                yield break;
            }

            bool bonusTurn = captureHappenedThisTurn ||
                             rolledValue == 6 ||
                             (rolledValue == 5 && action.Type == LudoActionType.LeaveHome);
            EndTurn(bonusTurn);
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

                foreach (LudoPlayerState player in players)
                {
                    if (player.Style == lastMovedToken.OwnerStyle)
                    {
                        RepositionSameColorTokens(player);
                        break;
                    }
                }

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
            actionPerformed = false;
            captureHappenedThisTurn = false;

            if (bonusTurn)
            {
                statusMessage =
                    $"{DisplayName(ActivePlayer.PlayerId)} rolls again!";
            }
            else
            {
                activePlayerIndex = (activePlayerIndex + 1) % players.Count;
                consecutiveSixes = 0;
                lastMovedToken = null;
                statusMessage =
                    $"{DisplayName(ActivePlayer.PlayerId)} player's turn. Roll the die.";
            }

            phase = LudoTurnPhase.AwaitingRoll;
            dice.SetRollEnabled(true);
            SetTokenInteractionStates(false, true);

            if (autoRoll)
            {
                StartCoroutine(AutoRollAfterDelay());
            }
        }

        private IEnumerator AutoRollAfterDelay()
        {
            yield return null;
            RequestRoll();
        }

        private void EndGame(LudoPlayerState winningPlayer)
        {
            winner = winningPlayer.Style;
            legalActions.Clear();
            ClearReachableCells();
            rolledValue = 0;
            actionPerformed = true;
            phase = LudoTurnPhase.GameOver;
            dice.SetRollEnabled(false);
            SetTokenInteractionStates(false, true);
            statusMessage =
                $"{DisplayName(winner.PlayerId)} wins! All four tokens reached the goal.";
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
                position += GetSameColorCellOffset(player, token, routeIndex, cell);
            }

            return position;
        }

        private Vector3 GetSameColorCellOffset(
            LudoPlayerState player,
            Token token,
            int routeIndex,
            Vector2Int cell)
        {
            int count = 0;
            int tokenIndex = -1;
            foreach (Token t in player.Tokens)
            {
                if (boardState.GetState(t) == TokenState.Track && boardState.GetRouteIndex(t) == routeIndex)
                {
                    if (t == token)
                    {
                        tokenIndex = count;
                    }

                    count++;
                }
            }

            if (count <= 1)
            {
                return Vector3.zero;
            }

            float offsetMagnitude = 0.55f;
            float baseOffset = (tokenIndex - (count - 1) * 0.5f) * offsetMagnitude;

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

        private void CaptureOpponentTokensOnCell(LudoPlayerState movingPlayer, Token movingToken)
        {
            List<Token> captured = LudoRulesEngine.GetCapturedTokens(
                boardState,
                movingPlayer,
                players,
                movingToken,
                new LudoRulesContext(elementalModeEnabled));

            foreach (Token token in captured)
            {
                boardState.SetHome(token);
                token.transform.position = homePositions[token];
                token.SetInteractionState(TokenInteractionState.Normal);
                captureHappenedThisTurn = true;
                statusMessage =
                    $"{movingToken.name} captured {token.name}!";
                LogMove(
                    $"Token {SpanishColorName(movingToken.OwnerStyle.PlayerId)} {movingToken.TokenId} " +
                    $"captura a Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId}.");
            }
        }

        private void RepositionSameColorTokens(LudoPlayerState player)
        {
            foreach (Token token in player.Tokens)
            {
                if (boardState.GetState(token) == TokenState.Track)
                {
                    Vector3 newPosition = GetRoutePosition(
                        player, token, boardState.GetRouteIndex(token));
                    token.transform.position = newPosition;
                }
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
                                state = TokenInteractionState.Selectable;
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

        private static string SpanishColorName(string playerId)
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

        private void LogBarrierIfFormed(LudoPlayerState player, Token token, int routeIndex)
        {
            Vector2Int cell = player.Route[routeIndex];
            if (LudoRulesEngine.CountSameColorTokensOnCell(boardState, player, cell) == 2)
            {
                LogMove(
                    $"Token {SpanishColorName(token.OwnerStyle.PlayerId)} {token.TokenId} " +
                    $"forma una barrera en la casilla {routeIndex}.");
            }
        }
    }
}
