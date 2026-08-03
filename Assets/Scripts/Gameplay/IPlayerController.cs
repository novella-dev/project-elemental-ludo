using System;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// A source of player intent: "roll" or "pick this token". Knows nothing
    /// about turn phases or rules — LudoGameController decides whether an
    /// intent is legal right now, and ignores intents from anyone but the
    /// active player.
    ///
    /// The events are how a controller pushes a decision out; the Begin*
    /// callbacks are how the game pulls one in. A human ignores the
    /// callbacks and just clicks whenever, while an AI does nothing until
    /// it is asked.
    /// </summary>
    public interface IPlayerController
    {
        event Action RollRequested;
        event Action<Token> TokenSelected;

        /// <summary>
        /// The token this controller is currently pointing at, before
        /// committing to it, or null for none. Purely a presentation hint so
        /// the board can show what is about to happen; a controller that
        /// decides in one step never needs to raise it.
        /// </summary>
        event Action<Token> SelectionChanged;

        /// <summary>The game is waiting for this player to roll the die.</summary>
        void BeginRollTurn(LudoTurnContext context);

        /// <summary>
        /// The game is waiting for this player to choose one of
        /// <see cref="LudoTurnContext.LegalActions"/>.
        /// </summary>
        void BeginActionTurn(LudoTurnContext context);

        /// <summary>
        /// The turn ended or was interrupted (game over, restart). Drop any
        /// decision still in flight so it can't land on a later turn.
        /// </summary>
        void CancelTurn();
    }
}
