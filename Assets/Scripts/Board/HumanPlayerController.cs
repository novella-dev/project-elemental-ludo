using System;
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
            }
        }

        private void OnDisable()
        {
            if (inputRouter != null)
            {
                inputRouter.RollKeyPressed -= HandleRollRequested;
                inputRouter.DiceClicked -= HandleDiceClicked;
                inputRouter.TokenClicked -= HandleTokenClicked;
            }
        }

        // A human doesn't wait to be asked — they click whenever the board
        // lets them, and LudoGameController drops anything that isn't the
        // active player's to give. So there is nothing to do on these.
        public void BeginRollTurn(LudoTurnContext context)
        {
        }

        public void BeginActionTurn(LudoTurnContext context)
        {
        }

        public void CancelTurn()
        {
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
            TokenSelected?.Invoke(token);
        }
    }
}
