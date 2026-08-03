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
        [SerializeField] private Camera backgroundCamera;
        [SerializeField] private Color darkBackground = Color.black;
        [SerializeField] private Color lightBackground = Color.white;

        private static readonly LudoGameMode[] SelectableModes =
        {
            LudoGameMode.Classic,
            LudoGameMode.Adventure,
            LudoGameMode.Hardcore,
            LudoGameMode.Multiplayer
        };

        private LudoAIDifficulty setupDifficulty = LudoAIDifficulty.Normal;
        private LudoGameMode setupMode;
        private bool setupElementalRules;
        private int setupSeatIndex;
        private bool setupDefaultsApplied;

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
        private GUIStyle endMatchButtonStyle;
        private GUIStyle toggleOnStyle;
        private GUIStyle toggleOffStyle;
        private GUIStyle winnerStyle;
        private GUIStyle ruleTitleStyle;
        private GUIStyle historyLatestStyle;
        private GUIStyle backgroundButtonStyle;
        private bool stylesReady;
        private bool lightBackgroundActive;

        private void Awake()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<LudoGameController>();
            }

            ResolveBackgroundCamera();
            if (backgroundCamera != null)
            {
                lightBackgroundActive =
                    backgroundCamera.backgroundColor.grayscale >= 0.5f;
                ApplyBackgroundColor();
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
            if (!showPanel)
            {
                return;
            }

            EnsureStyles();
            DrawBackgroundToggle();

            if (controller == null || !controller.IsInitialized)
            {
                return;
            }

            if (controller.AwaitingSetup)
            {
                DrawStartMenu();
                return;
            }

            // A duel owns the screen: the board panels are about a board the
            // camera isn't even looking at, so they'd just be clutter on top
            // of the arena.
            if (controller.IsCombatVisible)
            {
                DrawCombatPanel();
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

            DrawEndMatchButton();

            if (controller.ElementalModeEnabled)
            {
                DrawElementalRulesPanel();
            }

            DrawHistoryPanel();
        }

        private void DrawBackgroundToggle()
        {
            const float width = 180f;
            const float height = 34f;
            Rect buttonRect = new Rect(
                (Screen.width - width) * 0.5f,
                PanelMargin,
                width,
                height);
            string label = lightBackgroundActive
                ? "FONDO: BLANCO"
                : "FONDO: NEGRO";

            if (!GUI.Button(buttonRect, label, backgroundButtonStyle))
            {
                return;
            }

            lightBackgroundActive = !lightBackgroundActive;
            ApplyBackgroundColor();
        }

        private void ApplyBackgroundColor()
        {
            ResolveBackgroundCamera();
            if (backgroundCamera != null)
            {
                backgroundCamera.backgroundColor = lightBackgroundActive
                    ? lightBackground
                    : darkBackground;
            }
        }

        private void ResolveBackgroundCamera()
        {
            if (backgroundCamera != null)
            {
                return;
            }

            backgroundCamera = Camera.main;
            if (backgroundCamera == null)
            {
                backgroundCamera = FindFirstObjectByType<Camera>();
            }
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

            if (controller.ActionMoveDistance > 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(controller.ActionMoveDistance.ToString(), badgeStyle, GUILayout.Width(34f), GUILayout.Height(34f));
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

        /// <summary>
        /// Quits back to the start menu from mid-match, not just from the
        /// game-over screen — mainly so switching modes to test doesn't need
        /// a full close-and-reopen of the Editor each time.
        /// </summary>
        private void DrawEndMatchButton()
        {
            const float width = 220f;
            const float height = 44f;

            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    Screen.height - height - PanelMargin,
                    width,
                    height));

            if (GUILayout.Button(
                    "Finalizar Partida",
                    endMatchButtonStyle,
                    GUILayout.Height(height)))
            {
                controller.ReturnToMenu();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// The dice duel, centred over the board. Only ever drawn for duels
        /// the player is part of; AI-versus-AI ones resolve without any of
        /// this and never set IsCombatVisible.
        /// </summary>
        private void DrawCombatPanel()
        {
            const float width = 700f;
            const float height = 190f;

            LudoCombatSession session = controller.CombatSession;
            LudoCombatReport report = controller.CombatReport;
            Token attackerToken = session?.AttackerToken ?? report.Attacker;
            Token defenderToken = session?.DefenderToken ?? report.Defender;
            if (attackerToken == null || defenderToken == null)
            {
                return;
            }

            // A strip along the bottom, not a centred box: the dice and the
            // combatants are the point now that they exist in 3D, and a panel
            // in the middle would sit right on top of them.
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    Screen.height - height - PanelMargin,
                    width,
                    height),
                panelStyle);

            // Resolved but not yet cleared counts as over: the session lingers
            // through the result display, so testing for null alone would keep
            // showing the in-progress view the whole time.
            if (session == null || session.Phase == LudoCombatPhase.Resolved)
            {
                DrawCombatScoreLine(attackerToken, report.Outcome.Attacker, "ATACANTE", false);
                DrawCombatScoreLine(defenderToken, report.Outcome.Defender, "DEFENSOR", false);
                GUILayout.Space(8f);
                DrawCombatVerdict(report);
                GUILayout.EndArea();
                return;
            }

            bool attackerActive = session.Phase == LudoCombatPhase.AttackerTurn;
            DrawCombatScoreLine(
                attackerToken,
                session.Attacker.Evaluate(),
                "ATACANTE",
                attackerActive);

            if (attackerActive)
            {
                GUILayout.Label("DEFENSOR  ·  esperando su turno", hintStyle);
            }
            else
            {
                DrawCombatScoreLine(
                    defenderToken,
                    session.Defender.Evaluate(),
                    "DEFENSOR",
                    true);
                GUILayout.Label(
                    $"Necesita superar {session.ScoreToBeat} para resistir.",
                    hintStyle);
            }

            GUILayout.Space(6f);
            DrawCombatControls(session);
            GUILayout.EndArea();
        }

        /// <summary>One side's running total, marked when it's their turn.</summary>
        private void DrawCombatScoreLine(
            Token token,
            LudoCombatRoll roll,
            string role,
            bool isActive)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Box(
                string.Empty,
                MakeAccentStyle(token.OwnerStyle.TokenColor),
                GUILayout.Width(5f),
                GUILayout.Height(22f));
            GUILayout.Space(8f);
            GUILayout.Label(
                $"{role}  {LudoGameController.SpanishColorName(token.OwnerStyle.PlayerId)} " +
                $"{token.TokenId}" + (isActive ? "  ←" : string.Empty),
                sectionLabelStyle,
                GUILayout.Width(190f));
            GUILayout.Label(LudoCombatInfo.Describe(roll), ruleTitleStyle);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Reroll controls for the human. Rerolling is done by clicking the die
        /// itself out in the arena, which is why there are no numbered buttons
        /// here any more — the dice are already on screen in front of the
        /// player, and pointing at the one you mean beats matching it to a
        /// list. All that is left is the running total and a way to stop.
        /// </summary>
        private void DrawCombatControls(LudoCombatSession session)
        {
            if (!session.IsHumanTurn)
            {
                GUILayout.Label("El rival está decidiendo sus relanzamientos...", statusStyle);
                return;
            }

            LudoCombatHand hand = session.CurrentHand;
            GUILayout.Label(
                hand.CanReroll
                    ? $"Tu turno — haz clic en un dado para relanzarlo " +
                      $"({hand.RerollsLeft} restantes)"
                    : "Tu turno — sin relanzamientos",
                statusStyle);

            if (GUILayout.Button("Plantarse", primaryButtonStyle, GUILayout.Width(140f)))
            {
                controller.ConfirmCombatHand();
            }
        }


        /// <summary>
        /// Called from the player's point of view, which isn't the same as the
        /// attacker's: losing a duel you defended is a win for you.
        /// </summary>
        private void DrawCombatVerdict(LudoCombatReport report)
        {
            GUILayout.Label(report.HumanWon ? "VICTORIA" : "DERROTA", winnerStyle);
            GUILayout.Label(
                report.Outcome.AttackerWins
                    ? "La captura se consuma."
                    : "El atacante es rechazado.",
                hintStyle);
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
        /// Modal start menu: mode on the left, that mode's options on the
        /// right. Starting is deferred until after the layout block closes, so
        /// the match doesn't begin midway through building this frame's GUI.
        /// </summary>
        private void DrawStartMenu()
        {
            const float width = 900f;
            const float height = 560f;
            const float modeColumnWidth = 330f;

            ApplyStartMenuDefaults();

            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    Mathf.Max(PanelMargin, (Screen.height - height) * 0.5f),
                    width,
                    height),
                panelStyle);

            GUILayout.Label("ELEMENTAL LUDO", titleStyle);
            GUILayout.Label("Elige cómo quieres jugar", subtitleStyle);
            GUILayout.Space(14f);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(modeColumnWidth));
            GUILayout.Label("MODO", sectionLabelStyle);
            GUILayout.Space(4f);
            foreach (LudoGameMode mode in SelectableModes)
            {
                DrawModeCard(mode);
            }

            GUILayout.EndVertical();

            GUILayout.Space(20f);

            GUILayout.BeginVertical();
            bool start = DrawStartMenuOptions();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (start)
            {
                controller.StartMatch(BuildMatchSettings());
            }
        }

        /// <summary>
        /// Seeds the menu from the controller's configured default the first
        /// time it opens, so the Inspector value still means something.
        /// </summary>
        private void ApplyStartMenuDefaults()
        {
            if (setupDefaultsApplied)
            {
                return;
            }

            setupMode = controller.DefaultMode;
            setupElementalRules = false;
            setupSeatIndex = 0;
            setupDefaultsApplied = true;
        }

        private void DrawModeCard(LudoGameMode mode)
        {
            bool isSelected = setupMode == mode;

            GUILayout.BeginHorizontal();
            GUILayout.Box(
                string.Empty,
                MakeAccentStyle(isSelected ? Color.white : new Color(1f, 1f, 1f, 0.18f)),
                GUILayout.Width(4f),
                GUILayout.ExpandHeight(true));
            GUILayout.Space(8f);
            GUILayout.BeginVertical();

            if (GUILayout.Button(
                    LudoGameModeInfo.DisplayName(mode),
                    isSelected ? toggleOnStyle : toggleOffStyle))
            {
                setupMode = mode;

                // Classic is the mode that opts out of the elemental layer, so
                // the toggle can't survive a switch into it.
                if (!LudoMatchSettings.SupportsElementalRules(mode))
                {
                    setupElementalRules = false;
                }
            }

            GUILayout.Label(LudoGameModeInfo.Summary(mode), hintStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>Right-hand column. Returns true when the player starts.</summary>
        private bool DrawStartMenuOptions()
        {
            bool hasAI = setupMode != LudoGameMode.Multiplayer;

            GUILayout.Label("OPCIONES", sectionLabelStyle);
            GUILayout.Space(4f);

            if (hasAI)
            {
                GUILayout.Label("Dificultad de la IA", ruleTitleStyle);
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
                GUILayout.Label(
                    setupDifficulty == LudoAIDifficulty.Easy
                        ? "Avanza al azar y saca fichas cuando puede. Si captura, es casualidad."
                        : "Prioriza capturar, formar barreras y no quedarse a tiro.",
                    hintStyle);
            }
            else
            {
                GUILayout.Label("Sin IA: los cuatro colores son humanos.", hintStyle);
            }

            GUILayout.Space(12f);

            if (LudoMatchSettings.SupportsElementalRules(setupMode))
            {
                if (GUILayout.Button(
                        setupElementalRules
                            ? "REGLAS ELEMENTALES: ON"
                            : "REGLAS ELEMENTALES: OFF",
                        setupElementalRules ? toggleOnStyle : toggleOffStyle))
                {
                    setupElementalRules = !setupElementalRules;
                }

                GUILayout.Label(
                    setupElementalRules
                        ? "Cada color juega con el poder de su elemento."
                        : "Todos los colores juegan con las mismas reglas.",
                    hintStyle);
            }
            else
            {
                GUILayout.Label(
                    "Clásico no admite reglas elementales.",
                    hintStyle);
            }

            GUILayout.Space(12f);
            GUILayout.Label(hasAI ? "TU COLOR" : "COLORES EN JUEGO", sectionLabelStyle);
            GUILayout.Space(4f);
            DrawSeatPicker(hasAI);

            GUILayout.FlexibleSpace();
            return GUILayout.Button("EMPEZAR", primaryButtonStyle);
        }

        private void DrawSeatPicker(bool selectable)
        {
            IReadOnlyList<LudoPlayerState> seats = controller.Players;
            for (int index = 0; index < seats.Count; index++)
            {
                LudoPlayerState seat = seats[index];
                bool isChosen = selectable && index == setupSeatIndex;

                GUILayout.BeginHorizontal();
                GUILayout.Box(
                    string.Empty,
                    MakeAccentStyle(seat.Style.TokenColor),
                    GUILayout.Width(5f),
                    GUILayout.Height(24f));
                GUILayout.Space(8f);

                string label = setupElementalRules
                    ? ElementHeading(seat)
                    : LudoGameController.SpanishColorName(seat.Style.PlayerId);

                if (!selectable)
                {
                    GUILayout.Label(label, ruleTitleStyle);
                }
                else if (GUILayout.Button(label, isChosen ? toggleOnStyle : toggleOffStyle))
                {
                    setupSeatIndex = index;
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }

            if (selectable && setupElementalRules && setupSeatIndex < seats.Count)
            {
                GUILayout.Space(2f);
                GUILayout.Label(
                    LudoElementInfo.RuleSummary(seats[setupSeatIndex].Element),
                    hintStyle);
            }
        }

        private LudoMatchSettings BuildMatchSettings()
        {
            IReadOnlyList<LudoPlayerState> seats = controller.Players;
            int seatIndex = Mathf.Clamp(setupSeatIndex, 0, Mathf.Max(0, seats.Count - 1));
            LudoElement seatElement = seats.Count > 0
                ? seats[seatIndex].Element
                : default;

            return new LudoMatchSettings(
                setupMode,
                seatElement,
                setupDifficulty,
                setupElementalRules);
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
            // Hardcore can end with no winner at all: the player simply ran
            // out of tokens, so Winner is null here.
            if (controller.HumanDefeated)
            {
                GUILayout.Label("HAS PERDIDO", winnerStyle);
                GUILayout.Label("Te quedaste sin fichas.", hintStyle);
            }
            else if (controller.Winner != null)
            {
                string colour = LudoGameController
                    .SpanishColorName(controller.Winner.PlayerId)
                    .ToUpperInvariant();
                GUILayout.Label($"¡GANA {colour}!", winnerStyle);
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Jugar otra vez", primaryButtonStyle))
            {
                controller.RestartGame();
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Volver al menú", actionCardStyle))
            {
                controller.ReturnToMenu();
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
                    : $"Move {action.Token.name}  ·  " +
                      $"{controller.GetActionMoveDistance(action)} spaces";

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

            endMatchButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 10, 10),
                normal =
                {
                    background = GetSolidTexture(new Color(0.72f, 0.2f, 0.2f, 0.9f)),
                    textColor = Color.white
                },
                hover =
                {
                    background = GetSolidTexture(new Color(0.82f, 0.26f, 0.26f, 0.9f)),
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

            backgroundButtonStyle = new GUIStyle(toggleOffStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
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
