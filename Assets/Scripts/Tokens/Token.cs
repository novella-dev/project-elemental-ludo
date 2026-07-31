using UnityEngine;

namespace ElementalLudo.Tokens
{
    public enum TokenState
    {
        Home,
        Track,
        Finished
    }

    public enum TokenInteractionState
    {
        Normal,
        Selectable,
        Disabled
    }

    [SelectionBase]
    [DisallowMultipleComponent]
    public sealed class Token : MonoBehaviour
    {
        [SerializeField] private int tokenId;
        [SerializeField] private PlayerStyle ownerStyle;
        [SerializeField] private TokenState state = TokenState.Home;
        [SerializeField] private int routeIndex = -1;
        [SerializeField] private TokenVisual visual;

        public int TokenId => tokenId;
        public PlayerStyle OwnerStyle => ownerStyle;
        public TokenState State => state;
        public int RouteIndex => routeIndex;
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
            state = TokenState.Home;
            routeIndex = -1;
            RefreshVisual();
        }

        public void MoveToTrack(int newRouteIndex)
        {
            state = TokenState.Track;
            routeIndex = Mathf.Max(0, newRouteIndex);
        }

        public void SendHome()
        {
            state = TokenState.Home;
            routeIndex = -1;
        }

        public void MarkFinished(int finalRouteIndex)
        {
            state = TokenState.Finished;
            routeIndex = Mathf.Max(0, finalRouteIndex);
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
                visual.SetColor(ownerStyle.TokenColor);
            }
            else
            {
                visual.UseNeutralColor();
            }

            visual.SetInteractionState(InteractionState);
        }
    }
}
