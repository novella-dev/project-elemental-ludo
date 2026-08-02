using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// The former "Testing UI" OnGUI panel, pulled out of
    /// LudoGameController. Only reads the controller's public state and
    /// forwards button presses back into its public API — no rules or
    /// turn logic live here. Meant to be replaced by real UGUI/UI Toolkit
    /// later without touching the controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoDebugView : MonoBehaviour
    {
        [SerializeField] private LudoGameController controller;
        [SerializeField] private bool showPanel = true;

        private void Awake()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<LudoGameController>();
            }
        }

        private void OnGUI()
        {
            if (!showPanel || controller == null || !controller.IsInitialized)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 330f, 420f), GUI.skin.box);
            GUILayout.Label("ELEMENTAL LUDO", GUI.skin.label);

            Color previousColor = GUI.color;
            GUI.color = controller.ActivePlayer.TokenColor;
            GUILayout.Label(
                $"Current player: {LudoGameController.DisplayName(controller.ActivePlayer.PlayerId)}",
                GUI.skin.label);
            GUI.color = previousColor;

            if (controller.RolledValue > 0)
            {
                GUILayout.Label($"Rolled value: {controller.RolledValue}");
            }

            GUILayout.Label(controller.StatusMessage);
            GUILayout.Space(8f);

            string autoLabel = controller.AutoRoll ? "AUTO: ON" : "AUTO: OFF";
            if (GUILayout.Button(autoLabel))
            {
                controller.AutoRoll = !controller.AutoRoll;
            }

            if (controller.Phase == LudoTurnPhase.GameOver)
            {
                GUILayout.Label(
                    $"Winner: {LudoGameController.DisplayName(controller.Winner.PlayerId)}",
                    GUI.skin.label);
                if (GUILayout.Button("Play Again"))
                {
                    controller.RestartGame();
                }
            }
            else if (controller.Phase == LudoTurnPhase.AwaitingRoll)
            {
                if (GUILayout.Button("Roll Dice  (Space)"))
                {
                    controller.RequestRoll();
                }

                GUILayout.Label("You can also click the die on the board.");
            }
            else if (controller.Phase == LudoTurnPhase.AwaitingAction)
            {
                GUILayout.Label("Legal actions:");
                for (int index = 0; index < controller.LegalActions.Count; index++)
                {
                    LudoLegalAction action = controller.LegalActions[index];
                    string description = action.Type == LudoActionType.LeaveHome
                        ? $"Take {action.Token.name} out of Home"
                        : $"Move {action.Token.name} {controller.RolledValue} spaces";
                    if (GUILayout.Button(description))
                    {
                        controller.TrySelectToken(action.Token);
                    }
                }

                GUILayout.Label("Selectable tokens are highlighted on the board.");
            }
            else
            {
                GUILayout.Label("Resolving turn...");
            }

            GUILayout.EndArea();
        }
    }
}
