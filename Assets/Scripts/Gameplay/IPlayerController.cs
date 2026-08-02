using System;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// A source of player intent: "roll" or "pick this token". Knows
    /// nothing about turn phases or rules — LudoGameController decides
    /// whether an intent is legal right now. This is the seam that lets a
    /// human (mouse/keyboard via LudoBoardInputRouter) and, later, an AI
    /// opponent drive the same game loop through the same two events.
    /// </summary>
    public interface IPlayerController
    {
        event Action RollRequested;
        event Action<Token> TokenSelected;
    }
}
