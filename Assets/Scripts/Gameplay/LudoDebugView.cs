using System.Collections.Generic;
using ElementalLudo.Tokens;
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

        [SerializeField] private LudoGameController controller;
        [SerializeField] private bool showPanel = true;

        private LudoAIDifficulty setupDifficulty = LudoAIDifficulty.Normal;

        private readonly Dictionary<Color, Texture2D> textureCache =
            new Dictionary<Color, Texture2D>();

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

            if (controller.AwaitingSetup)
            {
                DrawSetupPopup();
                return;
            }

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

            GUILayout.Label("REGLAS ELEMENTALES", sectionLabelStyle);
            GUILayout.Space(6f);

            // Driven off the actual seats, so re-assigning an element in the
            // PlayerStyle assets is reflected here without touching this view.
            foreach (LudoPlayerState player in controller.Players)
            {
                DrawElementalRuleLine(
                    player.Style.TokenColor,
                    ElementHeading(player),
                    LudoElementInfo.RuleSummary(player.Element));
            }

            GUILayout.EndArea();
        }

        private static string ElementHeading(LudoPlayerState player)
        {
            return $"{LudoElementInfo.DisplayName(player.Element)} " +
                   $"({LudoGameController.SpanishColorName(player.Style.PlayerId)})";
        }

        /// <summary>
        /// Modal element picker shown before a one-player game starts. Choices
        /// are collected and applied after the layout block closes, so the
        /// game doesn't restart midway through building this frame's GUI.
        /// </summary>
        private void DrawSetupPopup()
        {
            const float width = 560f;
            const float height = 600f;

            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    Mathf.Max(PanelMargin, (Screen.height - height) * 0.5f),
                    width,
                    height),
                panelStyle);

            GUILayout.Label("ELEMENTAL LUDO", titleStyle);
            GUILayout.Label(
                "Elige tu elemento — la IA jugará los otros tres",
                subtitleStyle);
            GUILayout.Space(12f);

            GUILayout.Label("DIFICULTAD", sectionLabelStyle);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    "Fácil",
                    setupDifficulty == LudoAIDifficulty.Easy ? toggleOnStyle : toggleOffStyle))
            {
                setupDifficulty = LudoAIDifficulty.Easy;
            }

            if (GUILayout.Button(
                    "Normal",
                    setupDifficulty == LudoAIDifficulty.Normal ? toggleOnStyle : toggleOffStyle))
            {
                setupDifficulty = LudoAIDifficulty.Normal;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label(
                setupDifficulty == LudoAIDifficulty.Easy
                    ? "Avanza al azar y saca fichas cuando puede. Si captura, es casualidad."
                    : "Prioriza capturar, formar barreras y no quedarse a tiro.",
                hintStyle);

            GUILayout.Space(14f);
            GUILayout.Label("ELEMENTO", sectionLabelStyle);
            GUILayout.Space(4f);

            bool picked = false;
            LudoElement pickedElement = default;
            foreach (LudoPlayerState player in controller.Players)
            {
                if (DrawSetupElementCard(player))
                {
                    picked = true;
                    pickedElement = player.Element;
                }
            }

            GUILayout.EndArea();

            if (picked)
            {
                controller.StartSinglePlayer(pickedElement, setupDifficulty);
            }
        }

        private bool DrawSetupElementCard(LudoPlayerState player)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Box(
                string.Empty,
                MakeAccentStyle(player.Style.TokenColor),
                GUILayout.Width(5f),
                GUILayout.ExpandHeight(true));
            GUILayout.Space(8f);
            GUILayout.BeginVertical();
            GUILayout.Label(ElementHeading(player), ruleTitleStyle);
            GUILayout.Label(LudoElementInfo.RuleSummary(player.Element), hintStyle);
            bool clicked = GUILayout.Button(
                $"Jugar con {LudoElementInfo.DisplayName(player.Element)}",
                actionCardStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            return clicked;
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
                    Screen.width - HistoryPanelWidth - PanelMargin,
                    Screen.height - HistoryPanelHeight - PanelMargin,
                    HistoryPanelWidth,
                    HistoryPanelHeight),
                panelStyle);

            GUILayout.Label("MOVE HISTORY", sectionLabelStyle);
            GUILayout.Space(6f);

            IReadOnlyList<string> history = controller.MoveHistory;
            if (history.Count == 0)
            {
                GUILayout.Label("Nothing has happened yet.", hintStyle);
            }
            else
            {
                int shownCount = Mathf.Min(history.Count, MaxHistoryEntries);
                for (int index = 0; index < shownCount; index++)
                {
                    GUIStyle style = index == 0 ? historyLatestStyle : hintStyle;
                    GUILayout.Label(history[index], style);
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
            // The die keeps tumbling for a moment before it reports a result;
            // hide the button meanwhile so it can't look unresponsive.
            if (controller.IsDiceRolling)
            {
                GUILayout.Label("Rolling the die...", statusStyle);
                return;
            }

            // The controller ignores input from seats the human doesn't hold,
            // so showing the button during an AI turn would just look broken.
            if (!controller.IsActiveSeatHuman)
            {
                GUILayout.Label("La IA está pensando...", statusStyle);
                return;
            }

            if (GUILayout.Button("ROLL DICE  (Space)", primaryButtonStyle))
            {
                controller.RequestRoll();
            }

            GUILayout.Space(4f);
            GUILayout.Label("You can also click the die on the board.", hintStyle);
        }

        private void DrawAwaitingAction(Color playerColor)
        {
            if (!controller.IsActiveSeatHuman)
            {
                GUILayout.Label("La IA está eligiendo su jugada...", statusStyle);
                return;
            }

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
