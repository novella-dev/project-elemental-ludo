using System;
using System.Collections;
using System.Collections.Generic;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Drives the seats the human isn't playing. Decides the instant it is
    /// asked — the turn context wraps the live board, so holding it across a
    /// wait would risk reading a board that moved on — and only delays the
    /// announcement, to keep the turn readable.
    ///
    /// A single instance can serve every AI seat: nothing is remembered
    /// between turns, and each decision is taken purely from the context it
    /// was handed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AIPlayerController : MonoBehaviour, IPlayerController
    {
        // Normal-difficulty weights. Capturing dwarfs everything else, then
        // going home, then board control (barriers/safety), with a small
        // constant nudge toward advancing so equal-looking moves still
        // prefer progress.
        private const float CaptureWeight = 100f;
        private const float GoalWeight = 70f;
        private const float LeaveHomeWeight = 35f;
        private const float BarrierWeight = 28f;
        private const float SafeCellWeight = 12f;
        private const float ThreatPenalty = -22f;
        private const float BreakOwnBarrierPenalty = -10f;
        private const float ProgressWeight = 0.4f;

        [SerializeField] private LudoAIDifficulty difficulty = LudoAIDifficulty.Normal;
        [Min(0f)]
        [Tooltip("Pause before the AI rolls, so the turn is readable.")]
        [SerializeField] private float rollDelay = 0.5f;
        [Min(0f)]
        [Tooltip("Pause before the AI commits to a move.")]
        [SerializeField] private float actionDelay = 0.55f;

        private readonly List<LudoLegalAction> candidates = new List<LudoLegalAction>(4);

        private Coroutine pending;

        public event Action RollRequested;
        public event Action<Token> TokenSelected;

        // The AI commits in one step, so it never points at a move first.
#pragma warning disable 67
        public event Action<Token> SelectionChanged;
#pragma warning restore 67

        public LudoAIDifficulty Difficulty
        {
            get => difficulty;
            set => difficulty = value;
        }

        public void BeginRollTurn(LudoTurnContext context)
        {
            CancelTurn();

            if (isActiveAndEnabled)
            {
                pending = StartCoroutine(RollAfterDelay());
                return;
            }

            // Disabled object: better to act immediately than to stall the game.
            RollRequested?.Invoke();
        }

        public void BeginActionTurn(LudoTurnContext context)
        {
            CancelTurn();

            Token choice = ChooseToken(context);
            if (choice == null)
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                pending = StartCoroutine(SelectAfterDelay(choice));
                return;
            }

            TokenSelected?.Invoke(choice);
        }

        public void CancelTurn()
        {
            if (pending == null)
            {
                return;
            }

            StopCoroutine(pending);
            pending = null;
        }

        private void OnDisable()
        {
            // Unity already stopped it; drop the stale handle.
            pending = null;
        }

        private IEnumerator RollAfterDelay()
        {
            yield return Wait(rollDelay);
            pending = null;
            RollRequested?.Invoke();
        }

        private IEnumerator SelectAfterDelay(Token token)
        {
            yield return Wait(actionDelay);
            pending = null;
            TokenSelected?.Invoke(token);
        }

        private static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f)
            {
                yield return new WaitForSecondsRealtime(seconds);
            }
            else
            {
                // Always give up at least a frame, so announcing a decision
                // never re-enters the turn logic that asked for it.
                yield return null;
            }
        }

        private Token ChooseToken(LudoTurnContext context)
        {
            if (context.LegalActions == null || context.LegalActions.Count == 0)
            {
                return null;
            }

            return difficulty == LudoAIDifficulty.Easy
                ? ChooseEasy(context)
                : ChooseNormal(context);
        }

        /// <summary>
        /// Gets tokens onto the board when it can, and otherwise just picks
        /// a move at random. Any capture it makes is a coincidence.
        /// </summary>
        private Token ChooseEasy(LudoTurnContext context)
        {
            candidates.Clear();
            foreach (LudoLegalAction action in context.LegalActions)
            {
                if (action.Type == LudoActionType.LeaveHome)
                {
                    candidates.Add(action);
                }
            }

            if (candidates.Count == 0)
            {
                foreach (LudoLegalAction action in context.LegalActions)
                {
                    candidates.Add(action);
                }
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)].Token;
        }

        /// <summary>
        /// Scores every option and takes the best, breaking ties at random so
        /// equally good turns don't always play out identically.
        /// </summary>
        private Token ChooseNormal(LudoTurnContext context)
        {
            candidates.Clear();
            float bestScore = float.NegativeInfinity;

            foreach (LudoLegalAction action in context.LegalActions)
            {
                float score = ScoreAction(context, action);
                if (score > bestScore + 0.0001f)
                {
                    bestScore = score;
                    candidates.Clear();
                    candidates.Add(action);
                }
                else if (score > bestScore - 0.0001f)
                {
                    candidates.Add(action);
                }
            }

            if (candidates.Count == 0)
            {
                return context.LegalActions[0].Token;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)].Token;
        }

        private float ScoreAction(LudoTurnContext context, LudoLegalAction action)
        {
            LudoPlayerState player = context.Player;
            int destinationIndex = action.Type == LudoActionType.LeaveHome
                ? 0
                : action.DestinationRouteIndex;
            Vector2Int destinationCell = player.Route[destinationIndex];
            bool reachesGoal = destinationIndex == player.Route.Length - 1;

            float score = destinationIndex * ProgressWeight;

            if (action.Type == LudoActionType.LeaveHome)
            {
                score += LeaveHomeWeight;
            }

            if (reachesGoal)
            {
                // Nothing is captured or blocked at the goal, so the rest of
                // the board reasoning doesn't apply.
                return score + GoalWeight;
            }

            // Ask the real rules what this move would do, on a throwaway copy.
            BoardState simulated = context.Board.Clone();
            simulated.SetTrack(action.Token, destinationIndex);

            List<Token> captured = LudoRulesEngine.GetCapturedTokens(
                simulated,
                player,
                context.AllPlayers,
                action.Token,
                context.Rules);
            score += captured.Count * CaptureWeight;

            if (LudoRulesEngine.CountSameColorTokensOnCell(simulated, player, destinationCell) >= 2)
            {
                score += BarrierWeight;
            }

            if (LudoBoardRoutes.IsSafeCell(destinationCell))
            {
                score += SafeCellWeight;
            }

            if (IsCellThreatened(simulated, context, destinationCell))
            {
                score += ThreatPenalty;
            }

            if (action.Type == LudoActionType.Move)
            {
                Vector2Int fromCell = player.Route[context.Board.GetRouteIndex(action.Token)];
                if (LudoRulesEngine.CountSameColorTokensOnCell(context.Board, player, fromCell) >= 2)
                {
                    score += BreakOwnBarrierPenalty;
                }
            }

            return score;
        }

        /// <summary>
        /// Whether any opponent could land on <paramref name="cell"/> on their
        /// next roll. Accounts for the elemental rules that change who can be
        /// taken where, but deliberately ignores barriers blocking the
        /// opponent's path — that keeps this a quick read of the danger rather
        /// than a full search.
        /// </summary>
        private static bool IsCellThreatened(
            BoardState board,
            LudoTurnContext context,
            Vector2Int cell)
        {
            bool elemental = context.Rules.ElementalModeEnabled;
            bool safeCell = LudoBoardRoutes.IsSafeCell(cell);
            bool selfIsPlant = elemental && context.Player.Element == LudoElement.Plant;

            foreach (LudoPlayerState opponent in context.AllPlayers)
            {
                if (opponent == context.Player)
                {
                    continue;
                }

                // Only fire ignores safe cells.
                bool ignoresSafeCells = elemental && opponent.Element == LudoElement.Fire;
                if (safeCell && !ignoresSafeCells)
                {
                    continue;
                }

                // Water can never take a plant token.
                if (selfIsPlant && elemental && opponent.Element == LudoElement.Water)
                {
                    continue;
                }

                // Lightning always moves one further than it rolls.
                bool lightning = elemental && opponent.Element == LudoElement.Lightning;
                int minimumDistance = lightning ? 2 : 1;
                int maximumDistance = lightning ? 7 : 6;

                foreach (Token token in opponent.Tokens)
                {
                    if (board.GetState(token) != TokenState.Track)
                    {
                        continue;
                    }

                    int from = board.GetRouteIndex(token);
                    for (int distance = minimumDistance; distance <= maximumDistance; distance++)
                    {
                        if (LudoMovementRules.TryGetDestination(
                                TokenState.Track,
                                from,
                                distance,
                                opponent.Route.Length,
                                out int destination) &&
                            opponent.Route[destination] == cell)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
