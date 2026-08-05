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
        private const float UpgradePanelHeight = 330f;
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
        private LudoElement setupRunElement = LudoElement.Fire;
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
        private GUIStyle standButtonStyle;
        private GUIStyle standWinningButtonStyle;
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

            RefreshBackgroundCamera();
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
            RefreshBackgroundCamera();
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

            DrawUpgradesPanel();
            DrawHistoryPanel();
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

            // The map itself is drawn in 3D and clicked there; this is the
            // legend beside it. Putting the node list back here as buttons
            // would give the player two places to click for the same thing.
            const float width = 340f;
            const float height = 250f;
            GUILayout.BeginArea(
                new Rect(
                    PanelMargin,
                    PanelMargin,
                    width,
                    height),
                panelStyle);

            GUILayout.Label("LA AVENTURA", titleStyle);
            GUILayout.Label(
                $"{LudoElementInfo.DisplayName(run.Element)}  ·  " +
                LudoRunInfo.StatusLine(run),
                subtitleStyle);
            GUILayout.Space(6f);
            DrawRunLives(run);
            GUILayout.Space(8f);

            if (run.IsOver)
            {
                DrawRunEnding(run);
                GUILayout.EndArea();
                return;
            }

            // Named on hover, since a coloured disc alone cannot say what it is.
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

            GUILayout.Space(8f);
            DrawRunLegend();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Abandonar", endMatchButtonStyle))
            {
                controller.ReturnToMenu();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Lives as pips in the run's own colour, spent ones hollowed out.
        /// Shown as a row rather than a number because they are also the tokens
        /// the final game is played with, and a row of four reads as a board.
        /// </summary>
        private void DrawRunLives(LudoRunState run)
        {
            Color full = controller.RunElementColor;
            Color spent = new Color(1f, 1f, 1f, 0.16f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("VIDAS", sectionLabelStyle, GUILayout.Width(56f));
            for (int index = 0; index < LudoRunState.MaxLives; index++)
            {
                GUILayout.Box(
                    string.Empty,
                    MakeAccentStyle(index < run.Lives ? full : spent),
                    GUILayout.Width(18f),
                    GUILayout.Height(18f));
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

            const float width = 660f;
            const float height = 360f;
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                panelStyle);

            GUILayout.Label("ELIGE UNA MEJORA", titleStyle);
            GUILayout.Label(
                "Se activan cuando tú quieras y se gastan al usarse.",
                subtitleStyle);
            GUILayout.Space(10f);

            for (int index = 0; index < offer.Count; index++)
            {
                LudoUpgrade upgrade = offer[index];

                GUILayout.BeginVertical(panelStyle);
                if (GUILayout.Button(
                        LudoUpgradeInfo.DisplayName(upgrade.Kind),
                        primaryButtonStyle))
                {
                    controller.ClaimReward(index);
                }

                GUILayout.Label(LudoUpgradeInfo.Describe(upgrade), hintStyle);
                GUILayout.Label(
                    $"{upgrade.Charges} usos · " +
                    $"{LudoUpgradeInfo.ScopeName(upgrade.Scope)}",
                    hintStyle);
                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }

            GUILayout.EndArea();
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

            GUILayout.BeginArea(
                new Rect(
                    PanelMargin,
                    PanelMargin + 570f,
                    PanelWidth,
                    UpgradePanelHeight),
                panelStyle);

            GUILayout.Label("MEJORAS", sectionLabelStyle);
            GUILayout.Label(
                "Se activan y se gastan al usarse. No están siempre activas.",
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

            // Disabled rather than hidden once spent, so the player can still
            // see what they had and what it did.
            GUI.enabled = !spent;
            string label = slot.Armed
                ? $"◆ {LudoUpgradeInfo.DisplayName(upgrade.Kind)}"
                : LudoUpgradeInfo.DisplayName(upgrade.Kind);

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
            const float height = 232f;

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
                    $"Necesita superar {session.ScoreToBeat} para resistir.",
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

            if (!start)
            {
                return;
            }

            // Adventure is the run now: picking it opens a map rather than
            // dropping straight onto a board. Every other mode is still a
            // single match started the way it always was.
            if (setupMode == LudoGameMode.Adventure)
            {
                controller.StartRun(SelectedRunElement());
                return;
            }

            controller.StartMatch(BuildMatchSettings());
        }

        /// <summary>
        /// The element the run starts with, clamped to what has been unlocked
        /// so a stale selection can never smuggle in a locked one.
        /// </summary>
        private LudoElement SelectedRunElement()
        {
            LudoElementProgress progress = controller.ElementProgress;
            foreach (LudoElement element in progress.Unlocked())
            {
                if (element == setupRunElement)
                {
                    return element;
                }
            }

            foreach (LudoElement element in progress.Unlocked())
            {
                return element;
            }

            return LudoElement.Fire;
        }

        /// <summary>
        /// Which element to take into the run. Locked ones are shown greyed
        /// rather than hidden, so the player can see what finishing a run is
        /// worth.
        /// </summary>
        private void DrawRunElementPicker()
        {
            LudoElementProgress progress = controller.ElementProgress;

            GUILayout.Label("TU ELEMENTO", sectionLabelStyle);
            GUILayout.Label(
                progress.AllUnlocked
                    ? "Los tienes todos."
                    : "Completa una run para desbloquear el siguiente.",
                hintStyle);
            GUILayout.Space(4f);

            foreach (LudoElement element in progress.UnlockOrder)
            {
                bool unlocked = progress.IsUnlocked(element);
                bool selected = unlocked && element == SelectedRunElement();

                GUI.enabled = unlocked;
                if (GUILayout.Button(
                        unlocked
                            ? LudoElementInfo.DisplayName(element)
                            : $"{LudoElementInfo.DisplayName(element)}  (bloqueado)",
                        selected ? toggleOnStyle : toggleOffStyle))
                {
                    setupRunElement = element;
                }

                GUI.enabled = true;
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
            setupElementalRules = DefaultElementalRulesFor(setupMode);
            setupSeatIndex = 0;
            setupDefaultsApplied = true;
        }

        /// <summary>
        /// Adventure is the elemental mode — the duels, the +5 advantage and
        /// the upgrades planned on top of them all assume the layer is on, so
        /// starting it switched off hides the mode's whole point behind a
        /// toggle. The others stay opt-in.
        /// </summary>
        private static bool DefaultElementalRulesFor(LudoGameMode mode)
        {
            return mode == LudoGameMode.Adventure;
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

                // Reset to the mode's own default rather than carrying the
                // previous mode's answer across: Classic can't have the layer
                // at all and Adventure is built around it, so a value that made
                // sense for one is usually wrong for the next.
                setupElementalRules =
                    LudoMatchSettings.SupportsElementalRules(mode) &&
                    DefaultElementalRulesFor(mode);
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

            // Adventure picks its seat by element, and that choice is also the
            // meta-progression, so it replaces the colour picker entirely.
            if (setupMode == LudoGameMode.Adventure)
            {
                DrawRunElementPicker();
            }
            else
            {
                GUILayout.Label(hasAI ? "TU COLOR" : "COLORES EN JUEGO", sectionLabelStyle);
                GUILayout.Space(4f);
                DrawSeatPicker(hasAI);
            }

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
                    background = GetSolidTexture(new Color(0.34f, 0.36f, 0.40f, 0.95f)),
                    textColor = new Color(0.88f, 0.90f, 0.93f)
                },
                hover =
                {
                    background = GetSolidTexture(new Color(0.42f, 0.44f, 0.49f, 0.95f)),
                    textColor = Color.white
                }
            };

            standWinningButtonStyle = new GUIStyle(standButtonStyle)
            {
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
            backgroundButtonStyle.normal.background = GetSolidTexture(
                new Color(0.03f, 0.04f, 0.05f, 0.82f));
            backgroundButtonStyle.normal.textColor = Color.white;
            backgroundButtonStyle.hover.background = GetSolidTexture(
                new Color(0.08f, 0.10f, 0.12f, 0.94f));
            backgroundButtonStyle.hover.textColor = Color.white;

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
