using System.Collections.Generic;
using ElementalLudo.Board;
using ElementalLudo.Tokens;
using UnityEngine;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// The former "Testing UI" OnGUI panel, pulled out of
    /// LudoGameController. Only reads the controller's public state and
    /// forwards button presses back into its public API — no rules or
    /// turn logic live here.
    ///
    /// The start menu (Fase 5) is the first piece moved out to real uGUI —
    /// see <see cref="LudoStartMenuView"/> — and is no longer drawn from
    /// here. The rest (duel panel, run map legend, rewards, upgrades,
    /// history) is still IMGUI, meant to move the same way later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoDebugView : MonoBehaviour
    {
        private const float PanelWidth = 400f;
        private const float PanelMargin = 16f;
        private const float RulesPanelWidth = 330f;
        private const float HistoryPanelWidth = 360f;
        private const float HistoryPanelHeight = 300f;
        private const float UpgradePanelHeight = 330f;
        private const int MaxHistoryEntries = 10;

        [SerializeField] private LudoGameController controller;
        [SerializeField] private bool showPanel = true;
        [SerializeField] private Camera backgroundCamera;
        [SerializeField] private Color darkBackground = new Color(0.07f, 0.09f, 0.12f, 1f);
        [SerializeField] private Color lightBackground = new Color(0.80f, 0.84f, 0.89f, 1f);

        private readonly Dictionary<Color, Texture2D> textureCache =
            new Dictionary<Color, Texture2D>();
        private readonly Dictionary<string, Texture2D> celTextureCache =
            new Dictionary<string, Texture2D>();

        private LudoStartMenuView startMenuView;

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle headerTitleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle playerNameStyle;
        private GUIStyle statusStyle;
        private GUIStyle badgeStyle;
        private GUIStyle sectionLabelStyle;
        private GUIStyle hintStyle;
        private GUIStyle accentBarStyle;
        private GUIStyle heartStyle;
        private GUIStyle actionCardStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle endMatchButtonStyle;
        private GUIStyle standButtonStyle;
        private GUIStyle standWinningButtonStyle;
        private GUIStyle toggleOnStyle;
        private GUIStyle toggleOffStyle;
        private GUIStyle winnerStyle;
        private GUIStyle ruleTitleStyle;
        private GUIStyle historyLatestStyle;
        private GUIStyle backgroundButtonStyle;
        private Font uiFont;
        private bool stylesReady;
        private bool lightBackgroundActive;
        private bool showHudSettings;
        private bool showHudUpgrades;
        private bool showHudHistory;

        private void Awake()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<LudoGameController>();
            }

            RefreshBackgroundCamera();
            if (backgroundCamera != null)
            {
                lightBackgroundActive =
                    backgroundCamera.backgroundColor.grayscale >= 0.5f;
                ApplyBackgroundColor();
            }

            EnsureStartMenuView();
        }

        /// <summary>
        /// The start menu used to be drawn here in IMGUI, directly on top of
        /// the live board. It is now its own uGUI screen with a procedural
        /// backdrop, built the same way the run map and combat arena build
        /// themselves — a lazily-created child rather than anything placed
        /// by hand in the scene.
        /// </summary>
        private void EnsureStartMenuView()
        {
            if (startMenuView != null)
            {
                return;
            }

            startMenuView = FindFirstObjectByType<LudoStartMenuView>();
            if (startMenuView == null)
            {
                GameObject menuObject = new GameObject("StartMenu")
                {
                    hideFlags = HideFlags.DontSave
                };
                menuObject.transform.SetParent(transform, false);
                startMenuView = menuObject.AddComponent<LudoStartMenuView>();
            }
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in textureCache.Values)
            {
                Destroy(texture);
            }

            textureCache.Clear();

            foreach (Texture2D texture in celTextureCache.Values)
            {
                Destroy(texture);
            }

            celTextureCache.Clear();

            uiFont = null;
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            EnsureStyles();

            if (controller != null && controller.IsInitialized && controller.AwaitingSetup)
            {
                // LudoStartMenuView owns the screen now: its own uGUI canvas
                // and procedural backdrop, not this IMGUI panel drawing over
                // a live board nobody has started playing on yet.
                return;
            }

            RefreshBackgroundCamera();

            if (controller == null || !controller.IsInitialized)
            {
                return;
            }

            // A duel owns the screen: the board panels are about a board the
            // camera isn't even looking at, so they'd just be clutter on top
            // of the arena.
            if (controller.IsCombatVisible)
            {
                DrawCombatPanel();

                // Armed upgrades are spent when a set ends, not per round, so
                // between rounds of a best-of-three there is still a real
                // choice to make about what to carry into the next one.
                DrawUpgradesPanel();
                return;
            }

            // The reward screen and the map are both between-fights states, so
            // they take the whole screen rather than sitting over a board that
            // is not being played.
            if (controller.IsRewardPending)
            {
                DrawRewardScreen();
                return;
            }

            if (controller.IsRunMapVisible)
            {
                DrawRunMap();
                return;
            }

            DrawBoardAdventureHud(controller.ActivePlayer.TokenColor);
        }

        /// <summary>
        /// The board HUD follows the supplied adventure-menu composition: a
        /// persistent steel header, quest and progress cards, and a navigation
        /// rail with one dominant action. It deliberately does not reuse the
        /// former diagnostic panel layout.
        /// </summary>
        private void DrawBoardAdventureHud(Color playerColor)
        {
            DrawReferenceTopBar(
                $"{LudoGameModeInfo.DisplayName(controller.Settings.Mode).ToUpperInvariant()}  ·  " +
                $"TURNO {LudoGameController.DisplayName(controller.ActivePlayer.PlayerId).ToUpperInvariant()}");
            DrawReferenceQuestCard(playerColor);
            DrawReferenceProgressCard();
            DrawReferenceBottomBar();

            if (showHudSettings)
            {
                DrawHudSettingsDrawer();
            }

            if (showHudUpgrades)
            {
                DrawUpgradesPanel();
            }

            if (showHudHistory)
            {
                DrawHistoryPanel();
            }
        }

        private void DrawReferenceTopBar(string heading)
        {
            Rect header = new Rect(0f, 0f, Screen.width, 72f);
            GUI.Box(header, string.Empty, panelStyle);

            int lives = controller.CurrentRun != null
                ? controller.CurrentRun.Lives
                : LudoRunState.MaxLives;
            for (int index = 0; index < LudoRunState.MaxLives; index++)
            {
                Color heartColor = index < lives
                    ? new Color(1f, 0.22f, 0.31f)
                    : LudoUITheme.Disabled;
                GUI.Box(
                    new Rect(18f + index * 50f, 13f, 42f, 38f),
                    string.Empty,
                    MakeHeartStyle(heartColor));
            }

            GUI.Label(new Rect(250f, 8f, Screen.width - 500f, 52f), heading, headerTitleStyle);
            if (GUI.Button(new Rect(Screen.width - 70f, 10f, 54f, 50f), "⚙", toggleOffStyle))
            {
                showHudSettings = !showHudSettings;
                showHudHistory = false;
                showHudUpgrades = false;
            }
        }

        private void DrawReferenceQuestCard(Color playerColor)
        {
            float height = controller.Phase == LudoTurnPhase.AwaitingAction
                ? Mathf.Min(520f, 220f + controller.LegalActions.Count * 48f)
                : 220f;
            GUILayout.BeginArea(new Rect(18f, 92f, 330f, height), panelStyle);
            GUILayout.Label("▣  QUEST", titleStyle);
            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            GUILayout.Box(string.Empty, MakeAccentStyle(playerColor), GUILayout.Width(14f), GUILayout.Height(22f));
            GUILayout.Label(controller.StatusMessage, statusStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            if (controller.Phase == LudoTurnPhase.AwaitingAction && controller.IsActiveSeatHuman)
            {
                GUILayout.Label("○  ELIGE UNA FICHA", sectionLabelStyle);
                DrawMovementRethrowButton();
                for (int index = 0; index < controller.LegalActions.Count; index++)
                {
                    LudoLegalAction action = controller.LegalActions[index];
                    string description = action.Type == LudoActionType.LeaveHome
                        ? $"Sacar {action.Token.name}"
                        : $"Mover {action.Token.name} · {controller.GetActionMoveDistance(action)}";
                    if (GUILayout.Button(description, actionCardStyle, GUILayout.Height(38f)))
                    {
                        controller.TrySelectToken(action.Token);
                    }
                }
            }
            else
            {
                GUILayout.Label("●  Completa tu turno", sectionLabelStyle);
                GUILayout.Label("│", hintStyle);
                GUILayout.Label("○  Lleva las cuatro fichas a la meta", hintStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawReferenceProgressCard()
        {
            Rect card = new Rect(Screen.width - 205f, 92f, 185f, 205f);
            GUI.Box(card, string.Empty, panelStyle);
            GUI.Label(new Rect(card.x + 12f, card.y + 12f, 161f, 32f), "PROGRESO", sectionLabelStyle);

            Vector2[] points =
            {
                new Vector2(card.x + 55f, card.y + 155f),
                new Vector2(card.x + 100f, card.y + 130f),
                new Vector2(card.x + 74f, card.y + 98f),
                new Vector2(card.x + 125f, card.y + 68f)
            };
            Color[] colours =
            {
                LudoUITheme.Water, LudoUITheme.Reward,
                LudoUITheme.Danger, LudoUITheme.Plant
            };
            for (int index = 0; index < points.Length; index++)
            {
                if (index > 0)
                {
                    DrawGuiLine(points[index - 1], points[index], 5f, LudoUITheme.Ink);
                }

                GUI.DrawTexture(
                    new Rect(points[index].x - 7f, points[index].y - 7f, 14f, 14f),
                    GetSolidTexture(colours[index]));
            }
            GUI.Label(new Rect(card.x + 10f, card.y + 164f, 165f, 28f), "HASTA EL JEFE FINAL", hintStyle);
        }

        private void DrawReferenceBottomBar()
        {
            const float height = 104f;
            float y = Screen.height - height;
            GUI.Box(new Rect(0f, y, Screen.width, height), string.Empty, panelStyle);

            if (GUI.Button(new Rect(16f, y + 10f, 150f, 78f), "▱\nAVENTURA", toggleOnStyle))
            {
                showHudHistory = false;
                showHudUpgrades = false;
            }
            if (GUI.Button(new Rect(176f, y + 10f, 150f, 78f), "⇧\nMEJORAS", toggleOffStyle))
            {
                showHudUpgrades = !showHudUpgrades;
                showHudHistory = false;
            }
            if (GUI.Button(new Rect(Screen.width - 326f, y + 10f, 150f, 78f), "▣\nINVENTARIO", toggleOffStyle))
            {
                showHudUpgrades = !showHudUpgrades;
                showHudHistory = false;
            }
            if (GUI.Button(new Rect(Screen.width - 166f, y + 10f, 150f, 78f), "▤\nHISTORIA", toggleOffStyle))
            {
                showHudHistory = !showHudHistory;
                showHudUpgrades = false;
            }

            Rect main = new Rect((Screen.width - 340f) * 0.5f, y - 8f, 340f, 88f);
            if (controller.Phase == LudoTurnPhase.GameOver)
            {
                if (GUI.Button(main, "JUGAR OTRA VEZ", primaryButtonStyle))
                {
                    controller.RestartGame();
                }
            }
            else if (controller.Phase == LudoTurnPhase.AwaitingRoll &&
                !controller.IsDiceRolling && controller.IsActiveSeatHuman)
            {
                if (GUI.Button(main, "TIRAR DADO", primaryButtonStyle))
                {
                    controller.RequestRoll();
                }
            }
            else
            {
                GUI.Box(main, ShortPhaseLabel(), actionCardStyle);
            }
        }

        private string ShortPhaseLabel()
        {
            if (controller.IsDiceRolling)
            {
                return "TIRANDO...";
            }

            return controller.Phase switch
            {
                LudoTurnPhase.AwaitingAction => "ELIGE UNA FICHA",
                LudoTurnPhase.GameOver => "PARTIDA TERMINADA",
                _ => "ESPERA"
            };
        }

        private void DrawHudSettingsDrawer()
        {
            const float width = 330f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 20f, 92f, width, 260f), panelStyle);
            GUILayout.Label("CONFIGURACIÓN", titleStyle);
            DrawSettingsRow();
            GUILayout.Space(8f);
            if (GUILayout.Button(
                    lightBackgroundActive ? "FONDO: BLANCO" : "FONDO: NEGRO",
                    backgroundButtonStyle))
            {
                lightBackgroundActive = !lightBackgroundActive;
                ApplyBackgroundColor();
            }
            if (GUILayout.Button("SALIR DE PARTIDA", endMatchButtonStyle))
            {
                controller.ReturnToMenu();
            }
            GUILayout.EndArea();
        }

        private void DrawGuiLine(Vector2 from, Vector2 to, float width, Color colour)
        {
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            Vector2 delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.matrix = previous;
        }

        private float GetMainPanelHeight()
        {
            return controller.Phase switch
            {
                LudoTurnPhase.AwaitingAction => 560f,
                LudoTurnPhase.GameOver => 390f,
                _ => 350f
            };
        }

        // ------------------------------------------------------------------
        // Run
        // ------------------------------------------------------------------

        /// <summary>
        /// The map between fights: every stage laid out front to back, with the
        /// reachable nodes live and the rest shown but dead, so the player can
        /// see the shape of what is ahead and not just their next two options.
        /// </summary>
        private void DrawRunMap()
        {
            LudoRunState run = controller.CurrentRun;
            if (run == null)
            {
                return;
            }

            DrawReferenceTopBar($"STAGE {Mathf.Max(1, run.Stage + 1)}-{run.Map.StageCount}");

            const float width = 370f;
            const float height = 250f;
            GUILayout.BeginArea(new Rect(18f, 92f, width, height), panelStyle);
            GUILayout.Label("▣  QUEST", titleStyle);
            GUILayout.Label("●  Sigue el rastro elemental", sectionLabelStyle);
            GUILayout.Label("│", hintStyle);
            GUILayout.Label("○  Completa un nodo de combate", hintStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                $"{LudoElementInfo.DisplayName(run.Element).ToUpperInvariant()} · {LudoRunInfo.StatusLine(run)}",
                subtitleStyle);
            GUILayout.Space(8f);

            if (run.IsOver)
            {
                DrawRunEnding(run);
                GUILayout.EndArea();
                DrawRunBottomBar(null);
                return;
            }

            LudoRunNode hovered = controller.HoveredRunNode;
            if (hovered != null)
            {
                GUILayout.Label(LudoRunInfo.NodeName(hovered.Kind), sectionLabelStyle);
                GUILayout.Label(LudoRunInfo.NodeSummary(hovered.Kind), hintStyle);
            }
            else
            {
                GUILayout.Label(
                    run.Status == LudoRunStatus.Choosing
                        ? "Pasa el ratón por un nodo iluminado y haz clic."
                        : LudoRunInfo.NodeName(run.CurrentNode.Kind),
                    hintStyle);
            }
            GUILayout.EndArea();
            DrawReferenceProgressCard();
            DrawRunBottomBar(hovered);

            if (showHudSettings)
            {
                DrawHudSettingsDrawer();
            }
            if (showHudUpgrades)
            {
                DrawUpgradesPanel();
            }
            if (showHudHistory)
            {
                DrawHistoryPanel();
            }
        }

        private void DrawRunBottomBar(LudoRunNode hovered)
        {
            const float height = 104f;
            float y = Screen.height - height;
            GUI.Box(new Rect(0f, y, Screen.width, height), string.Empty, panelStyle);
            if (GUI.Button(new Rect(16f, y + 10f, 150f, 78f), "▱\nAVENTURA", toggleOnStyle))
            {
                showHudUpgrades = false;
                showHudHistory = false;
            }
            if (GUI.Button(new Rect(176f, y + 10f, 150f, 78f), "⇧\nMEJORAS", toggleOffStyle))
            {
                showHudUpgrades = !showHudUpgrades;
                showHudHistory = false;
            }
            if (GUI.Button(new Rect(Screen.width - 326f, y + 10f, 150f, 78f), "▣\nINVENTARIO", toggleOffStyle))
            {
                showHudUpgrades = !showHudUpgrades;
                showHudHistory = false;
            }
            if (GUI.Button(new Rect(Screen.width - 166f, y + 10f, 150f, 78f), "▤\nHISTORIA", toggleOffStyle))
            {
                showHudHistory = !showHudHistory;
                showHudUpgrades = false;
            }

            Rect start = new Rect((Screen.width - 340f) * 0.5f, y - 8f, 340f, 88f);
            if (hovered != null)
            {
                if (GUI.Button(start, "START", primaryButtonStyle))
                {
                    controller.TryEnterNode(hovered);
                }
            }
            else
            {
                GUI.Box(start, "ELIGE UN NODO", actionCardStyle);
            }
        }

        /// <summary>
        /// Lives as pips in the run's own colour, spent ones hollowed out.
        /// Shown as a row rather than a number because they are also the tokens
        /// the final game is played with, and a row of four reads as a board.
        /// </summary>
        private void DrawRunLives(LudoRunState run)
        {
            Color full = controller.RunElementColor;
            Color spent = LudoUITheme.Disabled;

            GUILayout.BeginHorizontal();
            GUILayout.Label("VIDAS", sectionLabelStyle, GUILayout.Width(56f));
            for (int index = 0; index < LudoRunState.MaxLives; index++)
            {
                GUILayout.Box(
                    string.Empty,
                    MakeHeartStyle(index < run.Lives ? full : spent),
                    GUILayout.Width(26f),
                    GUILayout.Height(24f));
                GUILayout.Space(4f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label(
                $"Llegarás a la partida final con {run.Lives} ficha(s).",
                hintStyle);
        }

        /// <summary>Which colour on the map means what.</summary>
        private void DrawRunLegend()
        {
            foreach (LudoRunNodeKind kind in RunNodeKinds)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Box(
                    string.Empty,
                    MakeAccentStyle(RunNodeLegendColor(kind)),
                    GUILayout.Width(10f),
                    GUILayout.Height(14f));
                GUILayout.Space(6f);
                GUILayout.Label(LudoRunInfo.NodeName(kind), hintStyle);
                GUILayout.EndHorizontal();
            }
        }

        private static readonly LudoRunNodeKind[] RunNodeKinds =
        {
            LudoRunNodeKind.Reward,
            LudoRunNodeKind.Duel,
            LudoRunNodeKind.Elite,
            LudoRunNodeKind.Heal,
            LudoRunNodeKind.Boss
        };

        /// <summary>
        /// Mirrors the map's own colours. Kept here rather than reached for
        /// across namespaces, since the legend only needs the swatch and the
        /// view has no business depending on Board.
        /// </summary>
        private static Color RunNodeLegendColor(LudoRunNodeKind kind)
        {
            return kind switch
            {
                LudoRunNodeKind.Reward => new Color(1f, 0.80f, 0.08f),
                LudoRunNodeKind.Duel => new Color(0.17f, 0.59f, 0.97f),
                LudoRunNodeKind.Heal => new Color(0.26f, 0.78f, 0.30f),
                LudoRunNodeKind.Match => new Color(0.91f, 0.72f, 0.39f),
                LudoRunNodeKind.Elite => new Color(0.97f, 0.43f, 0.50f),
                LudoRunNodeKind.Boss => new Color(0.43f, 0.04f, 0.10f),
                _ => Color.white
            };
        }

        private void DrawRunEnding(LudoRunState run)
        {
            GUILayout.Label(
                run.Status == LudoRunStatus.Won ? "VICTORIA" : "DERROTA",
                winnerStyle);
            GUILayout.Label(controller.StatusMessage, hintStyle);
            GUILayout.Space(10f);

            if (GUILayout.Button("Volver al menú", primaryButtonStyle))
            {
                controller.ReturnToMenu();
            }
        }

        /// <summary>
        /// The pick-one-of-three between fights. This is where a run's build
        /// actually gets decided, so it takes the screen on its own.
        /// </summary>
        private void DrawRewardScreen()
        {
            IReadOnlyList<LudoUpgrade> offer = controller.RewardOffer;
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                GetSolidTexture(new Color(0.07f, 0.09f, 0.12f, 0.96f)));
            DrawReferenceTopBar("RECOMPENSA DE AVENTURA");

            GUI.Label(
                new Rect(0f, 88f, Screen.width, 55f),
                "ELIGE UNA MEJORA",
                headerTitleStyle);
            GUI.Label(
                new Rect(0f, 135f, Screen.width, 34f),
                "Escoge una carta para incorporarla a tu inventario.",
                subtitleStyle);

            int count = Mathf.Max(1, offer.Count);
            float cardWidth = Mathf.Min(320f, (Screen.width - 120f) / count - 22f);
            float totalWidth = count * cardWidth + (count - 1) * 24f;
            float firstX = (Screen.width - totalWidth) * 0.5f;
            for (int index = 0; index < offer.Count; index++)
            {
                LudoUpgrade upgrade = offer[index];
                float x = firstX + index * (cardWidth + 24f);
                GUILayout.BeginArea(new Rect(x, 195f, cardWidth, 345f), panelStyle);
                GUILayout.Label("◆", winnerStyle);
                GUILayout.Space(8f);
                GUILayout.Label(LudoUpgradeInfo.DisplayName(upgrade).ToUpperInvariant(), sectionLabelStyle);
                GUILayout.Space(10f);
                GUILayout.Label(LudoUpgradeInfo.Describe(upgrade), statusStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    $"{upgrade.Charges} USOS · {LudoUpgradeInfo.ScopeName(upgrade.Scope).ToUpperInvariant()}" +
                    (upgrade.Level > 1 ? " · SUBE DE NIVEL" : string.Empty),
                    hintStyle);
                if (GUILayout.Button("ELEGIR", primaryButtonStyle, GUILayout.Height(58f)))
                {
                    controller.ClaimReward(index);
                }
                GUILayout.EndArea();
            }

            LudoRunState run = controller.CurrentRun;
            if (run != null && run.RefreshesLeft > 0 &&
                GUI.Button(
                    new Rect((Screen.width - 310f) * 0.5f, 565f, 310f, 50f),
                    $"CAMBIAR LAS TRES · {run.RefreshesLeft}",
                    endMatchButtonStyle))
            {
                controller.TryRefreshRewardOffer();
            }

            GUI.Box(new Rect(0f, Screen.height - 90f, Screen.width, 90f), string.Empty, panelStyle);
            if (showHudSettings)
            {
                DrawHudSettingsDrawer();
            }
        }

        /// <summary>
        /// What the player is carrying and what they have switched on.
        ///
        /// Sits on the left under the main panel, and only appears when there
        /// is anything to show — every mode but Adventure grants nothing, so
        /// this stays invisible there rather than showing an empty box.
        /// </summary>
        private void DrawUpgradesPanel()
        {
            IReadOnlyList<LudoUpgradeSlot> slots = controller.Upgrades.Slots;
            if (slots.Count == 0)
            {
                return;
            }

            // Under whichever panel is above it, which differs per screen: a
            // duel has none on the left at all, the run's legend is short, and
            // a match's controls are tall.
            float top = PanelMargin;
            if (!controller.IsCombatVisible)
            {
                top += controller.CurrentRun != null
                    ? 330f
                    : GetMainPanelHeight() + 10f;
            }

            GUILayout.BeginArea(
                new Rect(PanelMargin, top, PanelWidth, UpgradePanelHeight),
                panelStyle);

            GUILayout.Label("MEJORAS", sectionLabelStyle);
            GUILayout.Label(
                controller.CurrentRun != null
                    ? "Actívalas ahora: se gastan en el siguiente combate."
                    : "Se activan y se gastan al usarse. No están siempre activas.",
                hintStyle);
            GUILayout.Space(6f);

            for (int index = 0; index < slots.Count; index++)
            {
                DrawUpgradeRow(slots[index], index);
            }

            GUILayout.EndArea();
        }

        private void DrawUpgradeRow(LudoUpgradeSlot slot, int index)
        {
            LudoUpgrade upgrade = slot.Upgrade;
            bool spent = slot.ChargesLeft <= 0;

            GUILayout.BeginHorizontal();

            // Spent ones are dropped from the inventory between fights, so this
            // only greys out one that ran dry during the duel on screen.
            GUI.enabled = !spent;
            string label = slot.Armed
                ? $"◆ {LudoUpgradeInfo.DisplayName(upgrade)}"
                : LudoUpgradeInfo.DisplayName(upgrade);

            if (GUILayout.Button(
                    label,
                    slot.Armed ? toggleOnStyle : toggleOffStyle,
                    GUILayout.Width(168f)))
            {
                if (slot.Armed)
                {
                    controller.TryDisarmUpgrade(index);
                }
                else
                {
                    controller.TryArmUpgrade(index);
                }
            }

            GUI.enabled = true;
            GUILayout.Space(6f);
            GUILayout.BeginVertical();
            GUILayout.Label(LudoUpgradeInfo.Describe(upgrade), hintStyle);
            GUILayout.Label(
                spent
                    ? "Agotada"
                    : $"{slot.ChargesLeft}/{upgrade.Charges} usos · " +
                      $"{LudoUpgradeInfo.ScopeName(upgrade.Scope)}",
                hintStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
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
            RefreshBackgroundCamera();
            if (backgroundCamera != null)
            {
                backgroundCamera.backgroundColor = lightBackgroundActive
                    ? lightBackground
                    : darkBackground;
            }
        }

        private void RefreshBackgroundCamera()
        {
            Camera activeCamera = null;
            foreach (Camera candidate in Camera.allCameras)
            {
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (activeCamera == null || candidate.depth > activeCamera.depth)
                {
                    activeCamera = candidate;
                }
            }

            if (activeCamera == null)
            {
                activeCamera = Camera.main;
            }

            if (activeCamera == null)
            {
                activeCamera = FindFirstObjectByType<Camera>();
            }

            if (backgroundCamera == activeCamera)
            {
                return;
            }

            backgroundCamera = activeCamera;
            if (backgroundCamera != null)
            {
                lightBackgroundActive =
                    backgroundCamera.backgroundColor.grayscale >= 0.5f;
            }
        }

        private void DrawHeader(Color playerColor)
        {
            GUILayout.Label("ELEMENTAL LUDO", titleStyle);
            GUILayout.Label(
                $"{LudoGameModeInfo.DisplayName(controller.Settings.Mode).ToUpperInvariant()}  ·  PARTIDA EN CURSO",
                subtitleStyle);
            GUILayout.Space(5f);
            DrawStatusRule(playerColor);
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            Color previous = GUI.color;
            float pulse = 0.82f + 0.18f *
                (0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3.4f));
            GUI.color = new Color(1f, 1f, 1f, pulse);
            GUILayout.Box(string.Empty, MakeAccentStyle(playerColor), GUILayout.Width(8f), GUILayout.Height(30f));
            GUI.color = previous;
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

        private void DrawStatusRule(Color playerColor)
        {
            Color previous = GUI.color;
            float shimmer = 0.82f + 0.18f *
                (0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.8f));
            GUI.color = new Color(1f, 1f, 1f, shimmer);
            GUILayout.Box(
                string.Empty,
                MakeAccentStyle(playerColor),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(6f));
            GUI.color = previous;
        }

        private void DrawStatusRow()
        {
            GUILayout.Space(6f);
            GUILayout.Label(controller.StatusMessage, statusStyle);
        }

        private void DrawSettingsRow()
        {
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();

            string autoLabel = controller.AutoRoll ? "AUTO: SÍ" : "AUTO: NO";
            GUIStyle autoStyle = controller.AutoRoll ? toggleOnStyle : toggleOffStyle;
            if (GUILayout.Button(autoLabel, autoStyle))
            {
                controller.AutoRoll = !controller.AutoRoll;
            }

            string label = controller.ElementalModeEnabled
                ? "ELEMENTAL: SÍ"
                : "ELEMENTAL: NO";
            GUIStyle elementalStyle = controller.ElementalModeEnabled
                ? toggleOnStyle
                : toggleOffStyle;
            if (GUILayout.Button(label, elementalStyle))
            {
                controller.ElementalModeEnabled = !controller.ElementalModeEnabled;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPrimaryTurnAction()
        {
            if (controller.Phase != LudoTurnPhase.AwaitingRoll ||
                controller.IsDiceRolling ||
                !controller.IsActiveSeatHuman)
            {
                return;
            }

            const float width = 300f;
            const float height = 62f;
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    Screen.height - height - PanelMargin,
                    width,
                    height));

            if (GUILayout.Button(
                    "TIRAR DADO  ·  ESPACIO",
                    primaryButtonStyle,
                    GUILayout.Height(height)))
            {
                controller.RequestRoll();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Quits back to the start menu from mid-match, not just from the
        /// game-over screen — mainly so switching modes to test doesn't need
        /// a full close-and-reopen of the Editor each time.
        /// </summary>
        private void DrawEndMatchButton()
        {
            const float width = 170f;
            const float height = 42f;

            GUILayout.BeginArea(
                new Rect(
                    PanelMargin,
                    Screen.height - height - PanelMargin,
                    width,
                    height));

            if (GUILayout.Button(
                    "SALIR DE PARTIDA",
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
            const float width = 760f;
            const float height = 286f;

            LudoCombatSession session = controller.CombatSession;
            LudoCombatReport report = controller.CombatReport;
            Token attackerToken = session?.AttackerToken ?? report.Attacker;
            Token defenderToken = session?.DefenderToken ?? report.Defender;
            if (attackerToken == null || defenderToken == null)
            {
                return;
            }

            // Right aligned so the near combatant and its coloured pedestal
            // stay visible instead of disappearing behind the controls.
            GUILayout.BeginArea(
                new Rect(
                    Screen.width - width - PanelMargin,
                    Screen.height - height - PanelMargin,
                    width,
                    height),
                panelStyle);

            // Only present inside a loose combat, where a single round decides
            // nothing on its own.
            string scoreline = controller.RunDuelScoreline;
            if (!string.IsNullOrEmpty(scoreline))
            {
                GUILayout.Label(scoreline, sectionLabelStyle);
                GUILayout.Space(2f);
            }

            // Resolved but not yet cleared counts as over: the session lingers
            // through the result display, so testing for null alone would keep
            // showing the in-progress view the whole time.
            if (session == null || session.Phase == LudoCombatPhase.Resolved)
            {
                DrawCombatScoreLine(attackerToken, report.Outcome.Attacker, "ATACANTE", false);
                DrawCombatScoreLine(defenderToken, report.Outcome.Defender, "DEFENSOR", false);
                DrawCombatElementLine(
                    session != null && session.ElementalRules,
                    report.Outcome.Attacker,
                    report.Outcome.Defender,
                    attackerToken,
                    defenderToken);
                GUILayout.Space(8f);
                DrawCombatVerdict(report);
                GUILayout.EndArea();
                return;
            }

            bool attackerActive = session.Phase == LudoCombatPhase.AttackerTurn;
            LudoCombatRoll attackerRoll = session.Attacker.Evaluate();
            LudoCombatRoll defenderRoll = session.Defender.Evaluate();
            DrawCombatScoreLine(attackerToken, attackerRoll, "ATACANTE", attackerActive);

            if (attackerActive)
            {
                GUILayout.Label("DEFENSOR  ·  esperando su turno", hintStyle);
            }
            else
            {
                DrawCombatScoreLine(defenderToken, defenderRoll, "DEFENSOR", true);
                GUILayout.Label(
                    // Reaching the target is enough, not beating it: ties go to
                    // the defender. Saying "superar" was quietly telling the
                    // player to reroll a hand that had already won.
                    $"Con llegar a {session.ScoreToBeat} resiste — los empates " +
                    "son del defensor.",
                    hintStyle);
            }

            DrawCombatElementLine(
                session.ElementalRules,
                attackerRoll,
                defenderRoll,
                attackerToken,
                defenderToken);

            GUILayout.Space(6f);
            DrawCombatControls(session);

            // Drawn on top of the flow rather than inside it, pinned to the
            // panel's own corner: it needs to stay in the same spot duel after
            // duel so the player can find it without looking.
            DrawStandButton(session, width, height);
            GUILayout.EndArea();
        }

        /// <summary>
        /// Spells the elemental edge out inside the duel: which side it favours
        /// and the circle it comes from. Drawn whenever the elemental layer is
        /// on, so a neutral matchup still explains why nobody got the five
        /// points rather than leaving a blank where an explanation was.
        /// </summary>
        private void DrawCombatElementLine(
            bool elementalRules,
            LudoCombatRoll attackerRoll,
            LudoCombatRoll defenderRoll,
            Token attackerToken,
            Token defenderToken)
        {
            bool anyBonus =
                attackerRoll.ElementBonus > 0 || defenderRoll.ElementBonus > 0;
            if (!elementalRules && !anyBonus)
            {
                return;
            }

            string advantage = LudoCombatInfo.AdvantageLine(
                attackerRoll,
                defenderRoll,
                attackerToken,
                defenderToken);

            GUILayout.Space(4f);
            GUILayout.Label(
                advantage ?? "Sin ventaja elemental en este duelo.",
                advantage != null ? sectionLabelStyle : hintStyle);
            GUILayout.Label(LudoCombatInfo.AdvantageRule, hintStyle);
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
            GUILayout.Label($"{roll.Score}", winnerStyle, GUILayout.Width(70f));
            GUILayout.Label(LudoCombatInfo.Describe(roll), ruleTitleStyle);
            GUILayout.EndHorizontal();

            // The arithmetic under the total, so nothing about how a score was
            // reached has to be taken on trust.
            GUILayout.BeginHorizontal();
            GUILayout.Space(203f);
            GUILayout.Label(LudoCombatInfo.Breakdown(roll), hintStyle);
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
        }

        /// <summary>
        /// Pinned to the panel's bottom-right corner rather than following the
        /// rest of the layout, so it lands in the same place duel after duel.
        ///
        /// Grey by default; green with the label swapped to VICTORIA the
        /// instant the player's own score would already win the fight — which
        /// only has a real answer on the defender's turn, once
        /// <see cref="LudoCombatSession.ScoreToBeat"/> is fixed. On the
        /// attacker's turn the defender's dice exist but haven't been played
        /// yet, and the panel deliberately keeps that score hidden elsewhere
        /// in this view; colouring the button off it here would leak the same
        /// information through the back door.
        /// </summary>
        private void DrawStandButton(LudoCombatSession session, float panelWidth, float panelHeight)
        {
            if (!session.IsHumanTurn)
            {
                return;
            }

            bool winning =
                session.Phase == LudoCombatPhase.DefenderTurn &&
                session.CurrentHand.Evaluate().Score >= session.ScoreToBeat;

            const float buttonWidth = 150f;
            const float buttonHeight = 40f;
            const float margin = 14f;
            Rect buttonRect = new Rect(
                panelWidth - buttonWidth - margin,
                panelHeight - buttonHeight - margin,
                buttonWidth,
                buttonHeight);

            if (GUI.Button(
                    buttonRect,
                    winning ? "VICTORIA" : "Plantarse",
                    winning ? standWinningButtonStyle : standButtonStyle))
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
            IReadOnlyList<string> history = controller.MoveHistory;
            float height = Mathf.Min(
                HistoryPanelHeight,
                112f + Mathf.Min(history.Count, MaxHistoryEntries) * 22f);

            GUILayout.BeginArea(
                new Rect(
                    Screen.width - HistoryPanelWidth - PanelMargin,
                    Screen.height - height - PanelMargin,
                    HistoryPanelWidth,
                    height),
                panelStyle);

            GUILayout.Label("HISTORIAL DE MOVIMIENTOS", sectionLabelStyle);
            GUILayout.Space(6f);

            if (history.Count == 0)
            {
                GUILayout.Label("Todavía no ha ocurrido ningún movimiento.", hintStyle);
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
                GUILayout.Label("El dado está rodando...", statusStyle);
                return;
            }

            // The controller ignores input from seats the human doesn't hold,
            // so showing the button during an AI turn would just look broken.
            if (!controller.IsActiveSeatHuman)
            {
                GUILayout.Label("La IA está pensando...", statusStyle);
                return;
            }

            GUILayout.Label(
                "Usa el botón central, pulsa Espacio o haz clic en el dado del tablero.",
                hintStyle);
        }

        /// <summary>
        /// Offered only while a Repetir tirada is armed. Drawn above the action
        /// list because it replaces the roll those actions came from — once one
        /// is taken there is nothing left to rethrow.
        /// </summary>
        private void DrawMovementRethrowButton()
        {
            if (!controller.Upgrades.IsArmed(LudoUpgradeKind.MovementRethrow))
            {
                return;
            }

            if (GUILayout.Button(
                    $"Repetir tirada (sacaste {controller.RolledValue})",
                    primaryButtonStyle))
            {
                controller.RequestMovementRethrow();
            }

            GUILayout.Space(6f);
        }

        private void DrawAwaitingAction(Color playerColor)
        {
            if (!controller.IsActiveSeatHuman)
            {
                GUILayout.Label("La IA está eligiendo su jugada...", statusStyle);
                return;
            }

            DrawMovementRethrowButton();

            GUILayout.Label("MOVIMIENTOS DISPONIBLES", sectionLabelStyle);
            GUILayout.Space(4f);

            for (int index = 0; index < controller.LegalActions.Count; index++)
            {
                LudoLegalAction action = controller.LegalActions[index];
                string description = action.Type == LudoActionType.LeaveHome
                    ? $"Sacar {action.Token.name} de casa"
                    : $"Mover {action.Token.name}  ·  " +
                      $"{controller.GetActionMoveDistance(action)} casillas";

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
            GUILayout.Label("Las fichas disponibles están resaltadas en el tablero.", hintStyle);
        }

        private void DrawResolving()
        {
            int dotCount = Mathf.FloorToInt(Time.realtimeSinceStartup * 2f) % 4;
            GUILayout.Label("Resolviendo turno" + new string('.', dotCount), hintStyle);
        }

        private void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }

            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont != null)
            {
                GUI.skin.font = uiFont;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = GetCelTexture(LudoUITheme.Panel) },
                padding = new RectOffset(22, 22, 20, 20),
                border = new RectOffset(9, 9, 9, 9)
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = LudoUITheme.TextPrimary }
            };

            headerTitleStyle = new GUIStyle(titleStyle)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter
            };

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = LudoUITheme.TextSecondary }
            };

            playerNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = LudoUITheme.TextPrimary }
            };

            badgeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    background = GetCelTexture(LudoUITheme.Lightning),
                    textColor = LudoUITheme.Ink
                }
            };
            badgeStyle.border = new RectOffset(7, 7, 7, 7);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
                normal = { textColor = LudoUITheme.TextSecondary }
            };

            sectionLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = LudoUITheme.TextPrimary }
            };

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = LudoUITheme.TextMuted }
            };

            accentBarStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };

            heartStyle = new GUIStyle(GUI.skin.box)
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
                    background = GetCelTexture(LudoUITheme.Card),
                    textColor = LudoUITheme.TextPrimary
                },
                hover =
                {
                    background = GetCelTexture(LudoUITheme.CardHover),
                    textColor = LudoUITheme.TextPrimary
                }
            };
            actionCardStyle.border = new RectOffset(8, 8, 8, 8);

            primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 14, 14),
                normal =
                {
                    background = GetCelTexture(LudoUITheme.Cyan),
                    textColor = LudoUITheme.TextPrimary
                },
                hover =
                {
                    background = GetCelTexture(LudoUITheme.CyanHighlight),
                    textColor = LudoUITheme.Ink
                }
            };
            primaryButtonStyle.border = new RectOffset(8, 8, 8, 8);

            // Grey while standing pat is just an option, green the moment it
            // wins the duel — the colour is the whole signal, so it stays
            // deliberately dull until it means something.
            standButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 10, 10),
                normal =
                {
                    background = GetCelTexture(LudoUITheme.PanelRaised),
                    textColor = LudoUITheme.TextPrimary
                },
                hover =
                {
                    background = GetCelTexture(LudoUITheme.CardHover),
                    textColor = LudoUITheme.TextPrimary
                }
            };
            standButtonStyle.border = new RectOffset(8, 8, 8, 8);

            standWinningButtonStyle = new GUIStyle(standButtonStyle)
            {
                normal =
                {
                    background = GetCelTexture(LudoUITheme.Cyan),
                    textColor = LudoUITheme.TextPrimary
                },
                hover =
                {
                    background = GetCelTexture(LudoUITheme.CyanHighlight),
                    textColor = LudoUITheme.Ink
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
                    background = GetCelTexture(LudoUITheme.Danger),
                    textColor = Color.white
                },
                hover =
                {
                    background = GetCelTexture(LudoBoardVisualStyle.Lighten(LudoUITheme.Danger, 0.12f)),
                    textColor = Color.white
                }
            };
            endMatchButtonStyle.border = new RectOffset(8, 8, 8, 8);

            toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 6, 6),
                normal =
                {
                    background = GetCelTexture(LudoUITheme.Cyan),
                    textColor = LudoUITheme.TextPrimary
                }
            };
            toggleOnStyle.border = new RectOffset(8, 8, 8, 8);

            toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 6, 6),
                normal =
                {
                    background = GetCelTexture(LudoUITheme.Card),
                    textColor = LudoUITheme.TextSecondary
                }
            };
            toggleOffStyle.border = new RectOffset(8, 8, 8, 8);

            winnerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = LudoUITheme.Reward }
            };

            ruleTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = LudoUITheme.TextPrimary }
            };

            historyLatestStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = LudoUITheme.TextPrimary }
            };

            backgroundButtonStyle = new GUIStyle(toggleOffStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            backgroundButtonStyle.normal.background = GetCelTexture(LudoUITheme.Card);
            backgroundButtonStyle.normal.textColor = LudoUITheme.TextPrimary;
            backgroundButtonStyle.hover.background = GetCelTexture(LudoUITheme.Lightning);
            backgroundButtonStyle.hover.textColor = LudoUITheme.Ink;
            backgroundButtonStyle.border = new RectOffset(8, 8, 8, 8);

            stylesReady = true;
        }

        private GUIStyle MakeAccentStyle(Color color)
        {
            accentBarStyle.normal.background = GetSolidTexture(color);
            return accentBarStyle;
        }

        private GUIStyle MakeHeartStyle(Color color)
        {
            heartStyle.normal.background = GetHeartTexture(color);
            return heartStyle;
        }

        /// <summary>
        /// Generates a small nine-sliced card with clipped corners and a dark
        /// ink contour. Keeping it procedural means every overlay stays crisp
        /// at arbitrary Game-view resolutions without adding bitmap UI assets.
        /// </summary>
        private Texture2D GetCelTexture(Color fill)
        {
            string key = ColorUtility.ToHtmlStringRGBA(fill);
            if (celTextureCache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            const int size = 32;
            const int cut = 5;
            const int stroke = 3;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside =
                        x + y >= cut &&
                        (size - 1 - x) + y >= cut &&
                        x + (size - 1 - y) >= cut &&
                        (size - 1 - x) + (size - 1 - y) >= cut;

                    if (!inside)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    bool edge =
                        x < stroke || x >= size - stroke ||
                        y < stroke || y >= size - stroke ||
                        x + y < cut + stroke ||
                        (size - 1 - x) + y < cut + stroke ||
                        x + (size - 1 - y) < cut + stroke ||
                        (size - 1 - x) + (size - 1 - y) < cut + stroke;
                    Color pixel = fill;
                    if (edge)
                    {
                        pixel = LudoUITheme.Ink;
                    }
                    else if (y >= size - stroke - 2)
                    {
                        pixel = LudoBoardVisualStyle.Lighten(fill, 0.16f);
                    }
                    else if (y <= stroke + 2)
                    {
                        pixel = LudoBoardVisualStyle.Shade(fill, 0.28f);
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply(false, true);
            celTextureCache[key] = texture;
            return texture;
        }

        private Texture2D GetHeartTexture(Color fill)
        {
            string key = "heart-" + ColorUtility.ToHtmlStringRGBA(fill);
            if (celTextureCache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            bool[,] mask = new bool[size, size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - 15.5f) / 13.5f;
                    float ny = (y - 15f) / 13.5f;
                    float expression = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) -
                                       nx * nx * ny * ny * ny;
                    mask[x, y] = expression <= 0f;
                }
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!mask[x, y])
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    bool outline = false;
                    for (int oy = -1; oy <= 1 && !outline; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int sampleX = x + ox;
                            int sampleY = y + oy;
                            if (sampleX < 0 || sampleX >= size ||
                                sampleY < 0 || sampleY >= size ||
                                !mask[sampleX, sampleY])
                            {
                                outline = true;
                                break;
                            }
                        }
                    }

                    Color pixel = outline
                        ? LudoUITheme.Ink
                        : Color.Lerp(fill, Color.white, Mathf.InverseLerp(0f, size, y) * 0.18f);
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply(false, true);
            celTextureCache[key] = texture;
            return texture;
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
