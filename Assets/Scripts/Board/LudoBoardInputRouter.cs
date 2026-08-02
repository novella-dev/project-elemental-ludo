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
        [Tooltip("Depth of the plane used to pick bare board squares. Matches the top of the raised track tiles.")]
        [SerializeField] private float boardPlaneDepth = -0.05f;

        public event Action RollKeyPressed;
        public event Action<Dice> DiceClicked;
        public event Action<Token> TokenClicked;
        public event Action<Vector2Int> CellClicked;

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
            if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance))
            {
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
                    return;
                }
            }

            // Only the die and the tokens carry colliders, so a bare board
            // square is picked by crossing the ray with the board plane rather
            // than by hitting geometry.
            if (TryGetBoardCell(ray, out Vector2Int cell))
            {
                CellClicked?.Invoke(cell);
            }
        }

        private bool TryGetBoardCell(Ray ray, out Vector2Int cell)
        {
            cell = default;

            float denominator = ray.direction.z;
            if (Mathf.Abs(denominator) < 0.000001f)
            {
                return false;
            }

            float distance = (boardPlaneDepth - ray.origin.z) / denominator;
            if (distance < 0f)
            {
                return false;
            }

            Vector3 point = ray.origin + ray.direction * distance;
            cell = new Vector2Int(
                Mathf.RoundToInt(LudoBoardLayout.ToLogicalCoordinate(point.x)),
                Mathf.RoundToInt(LudoBoardLayout.ToLogicalCoordinate(point.y)));
            return true;
        }
    }
}
