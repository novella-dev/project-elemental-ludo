using System;
using ElementalLudo.DiceSystem;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalLudo.Board
{
    /// <summary>
    /// Resolves raw mouse/keyboard input into board-level intent (a key
    /// press, a die click, a token click) without knowing what any of it
    /// means for the current turn. Callers subscribe and decide what to do
    /// in their own phase/rules context — this is what lets touch, gamepad,
    /// or a scripted "AI clicks a token" flow reuse the same events later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoBoardInputRouter : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        [SerializeField] private float raycastMaxDistance = 1000f;

        public event Action RollKeyPressed;
        public event Action<Dice> DiceClicked;
        public event Action<Token> TokenClicked;

        private void Awake()
        {
            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                RollKeyPressed?.Invoke();
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                HandleBoardClick(mouse.position.ReadValue());
            }
        }

        private void HandleBoardClick(Vector2 screenPosition)
        {
            if (inputCamera == null)
            {
                return;
            }

            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance))
            {
                return;
            }

            Dice clickedDice = hit.collider.GetComponentInParent<Dice>();
            if (clickedDice != null)
            {
                DiceClicked?.Invoke(clickedDice);
                return;
            }

            Token clickedToken = hit.collider.GetComponentInParent<Token>();
            if (clickedToken != null)
            {
                TokenClicked?.Invoke(clickedToken);
            }
        }
    }
}
