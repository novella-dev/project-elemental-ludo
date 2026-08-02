using System.Collections.Generic;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// The former "Testing UI" OnGUI panel, pulled out of
    /// LudoGameController. Only reads the controller's public state and
    /// forwards button presses back into its public API — no rules or
    /// turn logic live here. Meant to be replaced by real UGUI/UI Toolkit
    /// later (Fase 5) without touching the controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoDebugView : MonoBehaviour
    {
        private const float PanelWidth = 380f;
        private const float PanelMargin = 16f;
        private const float RulesPanelWidth = 300f;
        private const float HistoryPanelWidth = 340f;
        private const float HistoryPanelHeight = 300f;
        private const int MaxHistoryEntries = 10;

        private static readonly Color LightningColor = new Color(0.9490196f, 0.827451f, 0.24313726f);
        private static readonly Color WaterColor = new Color(0.24313726f, 0.61960787f, 0.8117647f);
        private static readonly Color FireColor = new Color(0.827451f, 0.06666667f, 0.21176471f);
        private static readonly Color PlantColor = new Color(0.039215688f, 0.4117647f, 0.28235295f);

        [SerializeField] private LudoGameController controller;
        [SerializeField] private bool showPanel = true;

        private readonly Dictionary<Color, Texture2D> textureCache =
            new Dictionary<Color, Texture2D>();
        private readonly List<string> moveHistory = new List<string>(MaxHistoryEntries);
        private string lastLoggedStatusMessage = string.Empty;

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle playerNameStyle;
        private GUIStyle statusStyle;
        private GUIStyle badgeStyle;
        private GUIStyle sectionLabelStyle;
        private GUIStyle hintStyle;
        private GUIStyle accentBarStyle;
        private GUIStyle actionCardStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle toggleOnStyle;
        private GUIStyle toggleOffStyle;
        private GUIStyle winnerStyle;
        private GUIStyle ruleTitleStyle;
        private GUIStyle historyLatestStyle;
        private bool stylesReady;

        private void Awake()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<LudoGameController>();
            }
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            string current = controller.StatusMessage;
            if (current == lastLoggedStatusMessage)
            {
                return;
            }

            lastLoggedStatusMessage = current;
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            moveHistory.Insert(0, current);
            if (moveHistory.Count > MaxHistoryEntries)
            {
                moveHistory.RemoveAt(moveHistory.Count - 1);
            }
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in textureCache.Values)
            {
                Destroy(texture);
            }

            textureCache.Clear();
        }

        private void OnGUI()
        {
            if (!showPanel || controller == null || !controller.IsInitialized)
            {
                return;
            }

            EnsureStyles();

            Color playerColor = controller.ActivePlayer.TokenColor;

            GUILayout.BeginArea(
                new Rect(PanelMargin, PanelMargin, PanelWidth, 560f),
                panelStyle);

            DrawHeader(playerColor);
            DrawStatusRow();
            DrawAutoRollToggle();
            DrawElementalToggle();
            GUILayout.Space(10f);
            DrawPhaseContent(playerColor);

            GUILayout.EndArea();

            if (controller.ElementalModeEnabled)
            {
                DrawElementalRulesPanel();
            }

            DrawHistoryPanel();
        }

        private void DrawHeader(Color playerColor)
        {
            GUILayout.Label("ELEMENTAL LUDO", titleStyle);
            GUILayout.Label("Fase 0 · Testing UI", subtitleStyle);
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            GUILayout.Box(string.Empty, MakeAccentStyle(playerColor), GUILayout.Width(6f), GUILayout.Height(28f));
            GUILayout.Space(8f);
            GUILayout.Label(
                LudoGameController.DisplayName(controller.ActivePlayer.PlayerId),
                playerNameStyle);

            if (controller.RolledValue > 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(controller.RolledValue.ToString(), badgeStyle, GUILayout.Width(34f), GUILayout.Height(34f));
            }

            GUILayout.EndHorizontal();
        }

        private void DrawStatusRow()
        {
            GUILayout.Space(6f);
            GUILayout.Label(controller.StatusMessage, statusStyle);
        }

        private void DrawAutoRollToggle()
        {
            GUILayout.Space(6f);
            string autoLabel = controller.AutoRoll ? "AUTO-ROLL: ON" : "AUTO-ROLL: OFF";
            GUIStyle style = controller.AutoRoll ? toggleOnStyle : toggleOffStyle;
            if (GUILayout.Button(autoLabel, style))
            {
                controller.AutoRoll = !controller.AutoRoll;
            }
        }

        private void DrawElementalToggle()
        {
            GUILayout.Space(4f);
            string label = controller.ElementalModeEnabled
                ? "ELEMENTAL MODE: ON"
                : "ELEMENTAL MODE: OFF";
            GUIStyle style = controller.ElementalModeEnabled ? toggleOnStyle : toggleOffStyle;
            if (GUILayout.Button(label, style))
            {
                controller.ElementalModeEnabled = !controller.ElementalModeEnabled;
            }
        }

        private void DrawElementalRulesPanel()
        {
            GUILayout.BeginArea(
                new Rect(
                    Screen.width - RulesPanelWidth - PanelMargin,
                    PanelMargin,
                    RulesPanelWidth,
                    320f),
                panelStyle);

            GUILayout.Label("ELEMENTAL RULES", sectionLabelStyle);
            GUILayout.Space(6f);

            DrawElementalRuleLine(
                LightningColor,
                "Lightning (Yellow)",
                "Move distance is roll + 1. Leaving home still needs a 5, and bonus turns/three-sixes still use the real roll.");
            DrawElementalRuleLine(
                WaterColor,
                "Water (Blue)",
                "Ignores barriers of any color on its own moves. A water barrier still blocks everyone else normally.");
            DrawElementalRuleLine(
                FireColor,
                "Fire (Red)",
                "Captures on safe cells too — for Fire, no cell is safe for rivals.");
            DrawElementalRuleLine(
                PlantColor,
                "Plant (Green)",
                "Immune to captures from Water specifically. Fire and Lightning still capture it normally.");

            GUILayout.EndArea();
        }

        private void DrawElementalRuleLine(Color accentColor, string title, string description)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Box(
                string.Empty,
                MakeAccentStyle(accentColor),
                GUILayout.Width(4f),
                GUILayout.ExpandHeight(true));
            GUILayout.Space(6f);
            GUILayout.BeginVertical();
            GUILayout.Label(title, ruleTitleStyle);
            GUILayout.Label(description, hintStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private void DrawHistoryPanel()
        {
            GUILayout.BeginArea(
                new Rect(
                    PanelMargin,
                    Screen.height - HistoryPanelHeight - PanelMargin,
                    HistoryPanelWidth,
                    HistoryPanelHeight),
                panelStyle);

            GUILayout.Label("MOVE HISTORY", sectionLabelStyle);
            GUILayout.Space(6f);

            if (moveHistory.Count == 0)
            {
                GUILayout.Label("Nothing has happened yet.", hintStyle);
            }
            else
            {
                for (int index = 0; index < moveHistory.Count; index++)
                {
                    GUIStyle style = index == 0 ? historyLatestStyle : hintStyle;
                    GUILayout.Label(moveHistory[index], style);
                }
            }

            GUILayout.EndArea();
        }

        private void DrawPhaseContent(Color playerColor)
        {
            switch (controller.Phase)
            {
                case LudoTurnPhase.GameOver:
                    DrawGameOver();
                    break;
                case LudoTurnPhase.AwaitingRoll:
                    DrawAwaitingRoll();
                    break;
                case LudoTurnPhase.AwaitingAction:
                    DrawAwaitingAction(playerColor);
                    break;
                default:
                    DrawResolving();
                    break;
            }
        }

        private void DrawGameOver()
        {
            GUILayout.Label(
                $"{LudoGameController.DisplayName(controller.Winner.PlayerId)} WINS!",
                winnerStyle);
            GUILayout.Space(10f);
            if (GUILayout.Button("Play Again", primaryButtonStyle))
            {
                controller.RestartGame();
            }
        }

        private void DrawAwaitingRoll()
        {
            if (GUILayout.Button("ROLL DICE  (Space)", primaryButtonStyle))
            {
                controller.RequestRoll();
            }

            GUILayout.Space(4f);
            GUILayout.Label("You can also click the die on the board.", hintStyle);
        }

        private void DrawAwaitingAction(Color playerColor)
        {
            GUILayout.Label("LEGAL ACTIONS", sectionLabelStyle);
            GUILayout.Space(4f);

            for (int index = 0; index < controller.LegalActions.Count; index++)
            {
                LudoLegalAction action = controller.LegalActions[index];
                string description = action.Type == LudoActionType.LeaveHome
                    ? $"Take {action.Token.name} out of Home"
                    : $"Move {action.Token.name}  ·  {controller.RolledValue} spaces";

                GUILayout.BeginHorizontal();
                GUILayout.Box(string.Empty, MakeAccentStyle(playerColor), GUILayout.Width(4f), GUILayout.Height(30f));
                GUILayout.Space(6f);
                if (GUILayout.Button(description, actionCardStyle))
                {
                    controller.TrySelectToken(action.Token);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Selectable tokens are highlighted on the board.", hintStyle);
        }

        private void DrawResolving()
        {
            int dotCount = Mathf.FloorToInt(Time.realtimeSinceStartup * 2f) % 4;
            GUILayout.Label("Resolving turn" + new string('.', dotCount), hintStyle);
        }

        private void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = GetSolidTexture(new Color(0.05f, 0.05f, 0.08f, 0.9f)) },
                padding = new RectOffset(18, 18, 16, 16),
                border = new RectOffset(0, 0, 0, 0)
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(1f, 1f, 1f, 0.45f) }
            };

            playerNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            badgeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    background = GetSolidTexture(new Color(1f, 1f, 1f, 0.12f)),
                    textColor = Color.white
                }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
                normal = { textColor = new Color(1f, 1f, 1f, 0.75f) }
            };

            sectionLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.5f) }
            };

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(1f, 1f, 1f, 0.4f) }
            };

            accentBarStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };

            actionCardStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(12, 12, 10, 10),
                normal =
                {
                    background = GetSolidTexture(new Color(1f, 1f, 1f, 0.06f)),
                    textColor = Color.white
                },
                hover =
                {
                    background = GetSolidTexture(new Color(1f, 1f, 1f, 0.14f)),
                    textColor = Color.white
                }
            };

            primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 14, 14),
                normal =
                {
                    background = GetSolidTexture(new Color(0.22f, 0.72f, 0.42f, 0.95f)),
                    textColor = Color.white
                },
                hover =
                {
                    background = GetSolidTexture(new Color(0.27f, 0.8f, 0.48f, 0.95f)),
                    textColor = Color.white
                }
            };

            toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 6, 6),
                normal =
                {
                    background = GetSolidTexture(new Color(0.2f, 0.65f, 0.35f, 0.8f)),
                    textColor = Color.white
                }
            };

            toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 6, 6),
                normal =
                {
                    background = GetSolidTexture(new Color(1f, 1f, 1f, 0.08f)),
                    textColor = new Color(1f, 1f, 1f, 0.6f)
                }
            };

            winnerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            ruleTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            historyLatestStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            stylesReady = true;
        }

        private GUIStyle MakeAccentStyle(Color color)
        {
            accentBarStyle.normal.background = GetSolidTexture(color);
            return accentBarStyle;
        }

        private Texture2D GetSolidTexture(Color color)
        {
            if (textureCache.TryGetValue(color, out Texture2D cached))
            {
                return cached;
            }

            Texture2D texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            textureCache[color] = texture;
            return texture;
        }
    }
}
