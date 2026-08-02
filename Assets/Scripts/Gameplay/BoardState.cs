using System.Collections.Generic;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// The single source of truth for "which token is where". Token (a
    /// MonoBehaviour) keeps a Home/Track/Finished field of its own for the
    /// lifetime of Fase 0, Paso 5's extraction — after this, that field is
    /// gone: Token is identity plus visuals only, and every rule query
    /// (LudoRulesEngine, LudoMovementRules) reads state from here instead.
    ///
    /// Tokens are used only as dictionary keys (identity), never mutated
    /// through them, so Clone() is safe to hand to an AI look-ahead or a
    /// what-if simulation without touching the live scene.
    /// </summary>
    public sealed class BoardState
    {
        private readonly Dictionary<Token, LudoTokenState> tokenStates;

        public BoardState(IReadOnlyList<LudoPlayerState> players)
        {
            int capacity = 0;
            foreach (LudoPlayerState player in players)
            {
                capacity += player.Tokens.Count;
            }

            tokenStates = new Dictionary<Token, LudoTokenState>(capacity);
            foreach (LudoPlayerState player in players)
            {
                foreach (Token token in player.Tokens)
                {
                    tokenStates[token] = LudoTokenState.Home;
                }
            }
        }

        private BoardState(Dictionary<Token, LudoTokenState> tokenStates)
        {
            this.tokenStates = tokenStates;
        }

        public TokenState GetState(Token token)
        {
            return tokenStates[token].State;
        }

        public int GetRouteIndex(Token token)
        {
            return tokenStates[token].RouteIndex;
        }

        public void SetHome(Token token)
        {
            tokenStates[token] = LudoTokenState.Home;
        }

        public void SetTrack(Token token, int routeIndex)
        {
            tokenStates[token] = new LudoTokenState(TokenState.Track, Mathf.Max(0, routeIndex));
        }

        public void SetFinished(Token token, int routeIndex)
        {
            tokenStates[token] = new LudoTokenState(TokenState.Finished, Mathf.Max(0, routeIndex));
        }

        public BoardState Clone()
        {
            return new BoardState(new Dictionary<Token, LudoTokenState>(tokenStates));
        }
    }
}
