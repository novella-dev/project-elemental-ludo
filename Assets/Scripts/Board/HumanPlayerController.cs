using System;
using System.Collections.Generic;
using ElementalLudo.DiceSystem;
using ElementalLudo.Gameplay;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Board
{
    /// <summary>
    /// IPlayerController backed by mouse/keyboard, via LudoBoardInputRouter.
    /// Collapses the router's RollKeyPressed and DiceClicked into a single
    /// RollRequested intent — from here on, "how" the player asked to roll
    /// doesn't matter, only that they did.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HumanPlayerController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private LudoBoardInputRouter inputRouter;

        // What this turn allows, so a click on a highlighted square can be
        // turned back into the token that would move there.
        private readonly List<LudoLegalAction> pendingActions =
            new List<LudoLegalAction>(4);

        private BoardState pendingBoard;
        private LudoPlayerState pendingPlayer;
        private IReadOnlyList<LudoPlayerState> pendingAllPlayers;

        public event Action RollRequested;
        public event Action<Token> TokenSelected;

        private void Awake()
        {
            EnsureInputRouter();
        }

        private void EnsureInputRouter()
        {
            if (inputRouter != null)
            {
                return;
            }

            inputRouter = FindFirstObjectByType<LudoBoardInputRouter>();
            if (inputRouter != null)
            {
                return;
            }

            GameObject inputRouterObject = new GameObject("BoardInputRouter")
            {
                hideFlags = HideFlags.DontSave
            };
            inputRouterObject.transform.SetParent(transform, false);
            inputRouter = inputRouterObject.AddComponent<LudoBoardInputRouter>();
        }

        private void OnEnable()
        {
            if (inputRouter != null)
            {
                inputRouter.RollKeyPressed += HandleRollRequested;
                inputRouter.DiceClicked += HandleDiceClicked;
                inputRouter.TokenClicked += HandleTokenClicked;
                inputRouter.CellClicked += HandleCellClicked;
            }
        }

        private void OnDisable()
        {
            if (inputRouter != null)
            {
                inputRouter.RollKeyPressed -= HandleRollRequested;
                inputRouter.DiceClicked -= HandleDiceClicked;
                inputRouter.TokenClicked -= HandleTokenClicked;
                inputRouter.CellClicked -= HandleCellClicked;
            }
        }

        // A human doesn't wait to be asked to roll — they click whenever the
        // board lets them, and LudoGameController drops anything that isn't
        // the active player's to give.
        public void BeginRollTurn(LudoTurnContext context)
        {
        }

        /// <summary>
        /// Keeps this turn's options so a square can be clicked instead of a
        /// token. The action list is copied rather than referenced, because
        /// the game controller rebuilds its own while validating a move.
        /// </summary>
        public void BeginActionTurn(LudoTurnContext context)
        {
            pendingActions.Clear();
            foreach (LudoLegalAction action in context.LegalActions)
            {
                pendingActions.Add(action);
            }

            pendingBoard = context.Board;
            pendingPlayer = context.Player;
            pendingAllPlayers = context.AllPlayers;
        }

        public void CancelTurn()
        {
            pendingActions.Clear();
            pendingBoard = null;
            pendingPlayer = null;
            pendingAllPlayers = null;
        }

        private void HandleRollRequested()
        {
            RollRequested?.Invoke();
        }

        private void HandleDiceClicked(Dice clickedDice)
        {
            RollRequested?.Invoke();
        }

        private void HandleTokenClicked(Token token)
        {
            // Its own movable token: the click plainly means "move this one".
            foreach (LudoLegalAction action in pendingActions)
            {
                if (action.Token == token)
                {
                    TokenSelected?.Invoke(token);
                    return;
                }
            }

            // Otherwise the player was most likely aiming at the square the
            // token is standing on. This is what makes captures clickable:
            // aim at a highlighted square holding a rival and the ray hits
            // that rival, not the board.
            if (TryGetTokenCell(token, out Vector2Int occupiedCell) &&
                TryFindMoverForCell(occupiedCell, out Token mover))
            {
                TokenSelected?.Invoke(mover);
                return;
            }

            // Nothing resolvable — hand it over anyway and let the controller
            // reject it, exactly as before.
            TokenSelected?.Invoke(token);
        }

        private void HandleCellClicked(Vector2Int cell)
        {
            if (TryFindMoverForCell(cell, out Token mover))
            {
                TokenSelected?.Invoke(mover);
            }
        }

        /// <summary>Which of this turn's tokens, if any, would land on <paramref name="cell"/>.</summary>
        private bool TryFindMoverForCell(Vector2Int cell, out Token mover)
        {
            mover = null;
            if (pendingPlayer == null)
            {
                return false;
            }

            foreach (LudoLegalAction action in pendingActions)
            {
                int destinationIndex = action.Type == LudoActionType.LeaveHome
                    ? 0
                    : action.DestinationRouteIndex;
                if (pendingPlayer.Route[destinationIndex] == cell)
                {
                    mover = action.Token;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetTokenCell(Token token, out Vector2Int cell)
        {
            cell = default;
            if (token == null || pendingBoard == null || pendingAllPlayers == null)
            {
                return false;
            }

            foreach (LudoPlayerState player in pendingAllPlayers)
            {
                // Owner lookup first: a stray Token that isn't part of the
                // game has no entry in the board state to read.
                if (player.Style != token.OwnerStyle)
                {
                    continue;
                }

                if (pendingBoard.GetState(token) != TokenState.Track)
                {
                    return false;
                }

                cell = player.Route[pendingBoard.GetRouteIndex(token)];
                return true;
            }

            return false;
        }
    }
}
