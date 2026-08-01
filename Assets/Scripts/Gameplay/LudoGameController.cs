using System;
using System.Collections;
using System.Collections.Generic;
using ElementalLudo.Board;
using ElementalLudo.DiceSystem;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalLudo.Gameplay
{
    public enum LudoTurnPhase
    {
        AwaitingRoll,
        AwaitingAction,
        Resolving,
        GameOver
    }

    public enum LudoActionType
    {
        LeaveHome,
        Move
    }

    public readonly struct LudoLegalAction
    {
        public Token Token { get; }
        public LudoActionType Type { get; }
        public int DestinationRouteIndex { get; }

        public LudoLegalAction(
            Token token,
            LudoActionType type,
            int destinationRouteIndex)
        {
            Token = token;
            Type = type;
            DestinationRouteIndex = destinationRouteIndex;
        }
    }

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

        private sealed class PlayerRuntime
        {
            public PlayerStyle Style { get; }
            public List<Token> Tokens { get; }
            public Vector2Int[] Route { get; }

            public PlayerRuntime(
                PlayerStyle style,
                List<Token> tokens,
                Vector2Int[] route)
            {
                Style = style;
                Tokens = tokens;
                Route = route;
            }
        }

        [Header("Scene References")]
        [SerializeField] private Dice dice;
        [SerializeField] private Camera inputCamera;

        [Header("Turn Behaviour")]
        [SerializeField] private bool autoExecuteSingleAction = true;
        [Min(0f)]
        [SerializeField] private float noMoveMessageDuration = 1.1f;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float movementStepDuration = 0.11f;

        [Header("Testing UI")]
        [SerializeField] private bool showRuntimePanel = true;

        private readonly List<PlayerRuntime> players = new List<PlayerRuntime>(4);
        private readonly List<LudoLegalAction> legalActions =
            new List<LudoLegalAction>(4);
        private readonly Dictionary<Token, Vector3> homePositions =
            new Dictionary<Token, Vector3>(16);

        private int activePlayerIndex;
        private int rolledValue;
        private bool actionPerformed;
        private bool autoRoll;
        private bool initialized;
        private PlayerStyle winner;
        private string statusMessage = string.Empty;
        private LudoTurnPhase phase = LudoTurnPhase.AwaitingRoll;

        public int ActivePlayerIndex => activePlayerIndex;
        public PlayerStyle ActivePlayer =>
            initialized ? players[activePlayerIndex].Style : null;
        public int RolledValue => rolledValue;
        public LudoTurnPhase Phase => phase;
        public IReadOnlyList<LudoLegalAction> LegalActions => legalActions;
        public PlayerStyle Winner => winner;
        public bool IsGameOver => winner != null;

        private void Awake()
        {
            if (dice == null)
            {
                dice = FindFirstObjectByType<Dice>();
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (dice != null)
            {
                dice.Rolled += HandleDiceRolled;
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
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.spaceKey.wasPressedThisFrame &&
                phase == LudoTurnPhase.AwaitingRoll)
            {
                RequestRoll();
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                HandleBoardClick(mouse.position.ReadValue());
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
            foreach (PlayerRuntime player in players)
            {
                foreach (Token token in player.Tokens)
                {
                    token.SendHome();
                    token.transform.position = homePositions[token];
                    token.SetInteractionState(TokenInteractionState.Normal);
                }
            }

            activePlayerIndex = 0;
            rolledValue = 0;
            actionPerformed = false;
            winner = null;
            legalActions.Clear();
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

                players.Add(new PlayerRuntime(style, playerTokens, route));
            }

            return players.Count == 4;
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
            CalculateLegalActions();

            if (legalActions.Count == 0)
            {
                phase = LudoTurnPhase.Resolving;
                SetTokenInteractionStates(false);
                statusMessage = "No valid moves.";
                StartCoroutine(EndTurnAfterNoMove());
                return;
            }

            phase = LudoTurnPhase.AwaitingAction;
            SetTokenInteractionStates(true);
            statusMessage = legalActions.Count == 1
                ? "One valid action."
                : $"Choose one of {legalActions.Count} valid actions.";

            if (legalActions.Count == 1 && autoExecuteSingleAction)
            {
                TryExecuteAction(legalActions[0]);
            }
        }

        private void CalculateLegalActions()
        {
            legalActions.Clear();
            PlayerRuntime player = players[activePlayerIndex];
            foreach (Token token in player.Tokens)
            {
                if (LudoMovementRules.CanLeaveHome(token.State, rolledValue))
                {
                    Vector2Int startCell = player.Route[0];
                    if (!IsCellBlockedByOpponent(startCell, player))
                    {
                        legalActions.Add(new LudoLegalAction(
                            token,
                            LudoActionType.LeaveHome,
                            0));
                    }

                    continue;
                }

                if (LudoMovementRules.TryGetDestination(
                        token.State,
                        token.RouteIndex,
                        rolledValue,
                        player.Route.Length,
                        out int destination))
                {
                    if (!IsPathBlocked(player, token.RouteIndex + 1, destination))
                    {
                        legalActions.Add(new LudoLegalAction(
                            token,
                            LudoActionType.Move,
                            destination));
                    }
                }
            }
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
            PlayerRuntime player = players[activePlayerIndex];
            Token token = action.Token;

            if (action.Type == LudoActionType.LeaveHome)
            {
                yield return MoveTokenTo(
                    token,
                    GetRoutePosition(player, token, 0));
                token.MoveToTrack(0);
                CaptureOpponentTokensOnCell(player, token);
                RepositionSameColorTokens(player);
                statusMessage = $"{token.name} entered the starting square.";
            }
            else
            {
                int firstStep = token.RouteIndex + 1;
                for (int routeIndex = firstStep;
                     routeIndex <= action.DestinationRouteIndex;
                     routeIndex++)
                {
                    yield return MoveTokenTo(
                        token,
                        GetRoutePosition(player, token, routeIndex));
                    token.MoveToTrack(routeIndex);
                }

                CaptureOpponentTokensOnCell(player, token);
                RepositionSameColorTokens(player);

                if (action.DestinationRouteIndex == player.Route.Length - 1)
                {
                    token.MarkFinished(action.DestinationRouteIndex);
                    statusMessage = $"{token.name} reached the goal.";
                }
                else
                {
                    statusMessage =
                        $"{token.name} moved {rolledValue} spaces.";
                }
            }

            if (LudoMovementRules.HasWon(player.Tokens))
            {
                EndGame(player);
                yield break;
            }

            EndTurn();
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

        private IEnumerator EndTurnAfterNoMove()
        {
            if (noMoveMessageDuration > Mathf.Epsilon)
            {
                yield return new WaitForSecondsRealtime(noMoveMessageDuration);
            }

            EndTurn();
        }

        private void EndTurn()
        {
            legalActions.Clear();
            rolledValue = 0;
            actionPerformed = false;
            activePlayerIndex = (activePlayerIndex + 1) % players.Count;
            phase = LudoTurnPhase.AwaitingRoll;
            dice.SetRollEnabled(true);
            SetTokenInteractionStates(false, true);
            statusMessage =
                $"{DisplayName(ActivePlayer.PlayerId)} player's turn. Roll the die.";

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

        private void EndGame(PlayerRuntime winningPlayer)
        {
            winner = winningPlayer.Style;
            legalActions.Clear();
            rolledValue = 0;
            actionPerformed = true;
            phase = LudoTurnPhase.GameOver;
            dice.SetRollEnabled(false);
            SetTokenInteractionStates(false, true);
            statusMessage =
                $"{DisplayName(winner.PlayerId)} wins! All four tokens reached the goal.";
        }

        private Vector3 GetRoutePosition(
            PlayerRuntime player,
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
            PlayerRuntime player,
            Token token,
            int routeIndex,
            Vector2Int cell)
        {
            int count = 0;
            int tokenIndex = -1;
            foreach (Token t in player.Tokens)
            {
                if (t.State == TokenState.Track && t.RouteIndex == routeIndex)
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
            if (token.State != TokenState.Track && token.State != TokenState.Finished)
            {
                return new Vector2Int(int.MinValue, int.MinValue);
            }

            foreach (PlayerRuntime player in players)
            {
                if (player.Style == token.OwnerStyle)
                {
                    return player.Route[token.RouteIndex];
                }
            }

            return new Vector2Int(int.MinValue, int.MinValue);
        }

        private void CaptureOpponentTokensOnCell(PlayerRuntime movingPlayer, Token movingToken)
        {
            Vector2Int movingCell = movingPlayer.Route[movingToken.RouteIndex];

            foreach (PlayerRuntime player in players)
            {
                if (player == movingPlayer)
                {
                    continue;
                }

                List<Token> captured = new List<Token>();
                foreach (Token token in player.Tokens)
                {
                    if (token.State != TokenState.Track)
                    {
                        continue;
                    }

                    Vector2Int tokenCell = player.Route[token.RouteIndex];
                    if (tokenCell == movingCell)
                    {
                        captured.Add(token);
                    }
                }

                foreach (Token token in captured)
                {
                    token.SendHome();
                    token.transform.position = homePositions[token];
                    token.SetInteractionState(TokenInteractionState.Normal);
                    statusMessage =
                        $"{movingToken.name} captured {token.name}!";
                }
            }
        }

        private void RepositionSameColorTokens(PlayerRuntime player)
        {
            foreach (Token token in player.Tokens)
            {
                if (token.State == TokenState.Track)
                {
                    Vector3 newPosition = GetRoutePosition(
                        player, token, token.RouteIndex);
                    token.transform.position = newPosition;
                }
            }
        }

        private static int CountSameColorTokensOnCell(
            PlayerRuntime player,
            Vector2Int cell)
        {
            int count = 0;
            foreach (Token token in player.Tokens)
            {
                if (token.State == TokenState.Track &&
                    player.Route[token.RouteIndex] == cell)
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsCellBlockedByOpponent(
            Vector2Int cell,
            PlayerRuntime movingPlayer)
        {
            foreach (PlayerRuntime player in players)
            {
                if (player == movingPlayer)
                {
                    continue;
                }

                if (CountSameColorTokensOnCell(player, cell) >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPathBlocked(
            PlayerRuntime player,
            int startIndex,
            int endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                Vector2Int cell = player.Route[i];
                if (IsCellBlockedByOpponent(cell, player))
                {
                    return true;
                }
            }

            return false;
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
            foreach (PlayerRuntime player in players)
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

        private void HandleBoardClick(Vector2 screenPosition)
        {
            if (!initialized || inputCamera == null)
            {
                return;
            }

            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return;
            }

            if (phase == LudoTurnPhase.AwaitingRoll &&
                hit.collider.GetComponentInParent<Dice>() == dice)
            {
                RequestRoll();
                return;
            }

            if (phase == LudoTurnPhase.AwaitingAction)
            {
                Token token = hit.collider.GetComponentInParent<Token>();
                TrySelectToken(token);
            }
        }

        private void OnGUI()
        {
            if (!showRuntimePanel || !initialized)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 330f, 420f), GUI.skin.box);
            GUILayout.Label("ELEMENTAL LUDO", GUI.skin.label);

            Color previousColor = GUI.color;
            GUI.color = ActivePlayer.TokenColor;
            GUILayout.Label(
                $"Current player: {DisplayName(ActivePlayer.PlayerId)}",
                GUI.skin.label);
            GUI.color = previousColor;

            if (rolledValue > 0)
            {
                GUILayout.Label($"Rolled value: {rolledValue}");
            }

            GUILayout.Label(statusMessage);
            GUILayout.Space(8f);

            string autoLabel = autoRoll ? "AUTO: ON" : "AUTO: OFF";
            if (GUILayout.Button(autoLabel))
            {
                autoRoll = !autoRoll;
            }

            if (phase == LudoTurnPhase.GameOver)
            {
                GUILayout.Label(
                    $"Winner: {DisplayName(winner.PlayerId)}",
                    GUI.skin.label);
                if (GUILayout.Button("Play Again"))
                {
                    RestartGame();
                }
            }
            else if (phase == LudoTurnPhase.AwaitingRoll)
            {
                if (GUILayout.Button("Roll Dice  (Space)"))
                {
                    RequestRoll();
                }

                GUILayout.Label("You can also click the die on the board.");
            }
            else if (phase == LudoTurnPhase.AwaitingAction)
            {
                GUILayout.Label("Legal actions:");
                for (int index = 0; index < legalActions.Count; index++)
                {
                    LudoLegalAction action = legalActions[index];
                    string description = action.Type == LudoActionType.LeaveHome
                        ? $"Take {action.Token.name} out of Home"
                        : $"Move {action.Token.name} {rolledValue} spaces";
                    if (GUILayout.Button(description))
                    {
                        TryExecuteAction(action);
                    }
                }

                GUILayout.Label("Selectable tokens are highlighted on the board.");
            }
            else
            {
                GUILayout.Label("Resolving turn...");
            }

            GUILayout.EndArea();
        }

        private static string DisplayName(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return "Unknown";
            }

            return char.ToUpperInvariant(playerId[0]) + playerId.Substring(1);
        }
    }
}
