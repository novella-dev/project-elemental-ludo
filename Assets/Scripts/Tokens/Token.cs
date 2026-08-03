using UnityEngine;

namespace ElementalLudo.Tokens
{
    public enum TokenState
    {
        Home,
        Track,
        Finished,

        /// <summary>
        /// Captured under Hardcore rules: out of the game for good, never
        /// legal to move and occupying no square.
        /// </summary>
        Eliminated
    }

    public enum TokenInteractionState
    {
        Normal,

        /// <summary>Could be moved this turn.</summary>
        Selectable,

        /// <summary>Picked by the player and awaiting confirmation.</summary>
        Selected,

        Disabled
    }

    [SelectionBase]
    [DisallowMultipleComponent]
    public sealed class Token : MonoBehaviour
    {
        [SerializeField] private int tokenId;
        [SerializeField] private PlayerStyle ownerStyle;
        [SerializeField] private TokenVisual visual;

        public int TokenId => tokenId;
        public PlayerStyle OwnerStyle => ownerStyle;
        public TokenInteractionState InteractionState { get; private set; }

        private void OnEnable()
        {
            RefreshVisual();
        }

        private void OnValidate()
        {
            RefreshVisual();
        }

        public void Initialize(int id, PlayerStyle style)
        {
            tokenId = id;
            ownerStyle = style;
            RefreshVisual();
        }

        public void SetInteractionState(TokenInteractionState interactionState)
        {
            InteractionState = interactionState;
            if (visual != null)
            {
                visual.SetInteractionState(interactionState);
            }
        }

        private void RefreshVisual()
        {
            if (visual == null)
            {
                visual = GetComponentInChildren<TokenVisual>(true);
            }

            if (visual == null)
            {
                return;
            }

            if (ownerStyle != null)
            {
                visual.SetStyle(ownerStyle);
            }
            else
            {
                visual.SetStyle(null);
            }

            visual.SetInteractionState(InteractionState);
        }
    }
}
