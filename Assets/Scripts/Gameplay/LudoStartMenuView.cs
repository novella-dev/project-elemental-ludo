using System.Collections.Generic;
using ElementalLudo.Board;
using ElementalLudo.Tokens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// Full-screen front end inspired by the supplied adventure UI references.
    /// The board remains a live procedural backdrop, while the interface is split
    /// into a board hub, an elemental-stage route and a settings drawer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoStartMenuView : MonoBehaviour
    {
        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;
        private const int MaxSeats = 4;

        private static readonly LudoGameMode[] Modes =
        {
            LudoGameMode.Adventure,
            LudoGameMode.Classic,
            LudoGameMode.Hardcore,
            LudoGameMode.Multiplayer
        };

        private enum FrontScreen
        {
            Hub,
            Stages,
            Settings
        }

        [SerializeField] private LudoGameController controller;

        private readonly List<ButtonTile> modeTiles = new List<ButtonTile>();
        private readonly List<ButtonTile> stageTiles = new List<ButtonTile>();
        private readonly List<ButtonTile> seatTiles = new List<ButtonTile>();
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly List<Sprite> generatedSprites = new List<Sprite>();

        private LudoStartMenuBackdrop backdrop;
        private GameObject canvasRoot;
        private GameObject hubScreen;
        private GameObject stagesScreen;
        private GameObject settingsScreen;
        private ButtonTile continueButton;
        private GameObject difficultyGroup;
        private GameObject seatGroup;
        private Text hubModeLabel;
        private Text hubQuestLabel;
        private Text stageProgressLabel;
        private Text stageTitleLabel;
        private Text stageDescriptionLabel;
        private Text startLabel;
        private Text difficultyLabel;
        private Text elementalLabel;
        private Font font;
        private FrontScreen screen;
        private bool built;
        private bool visible;
        private bool defaultsApplied;
        private LudoGameMode setupMode = LudoGameMode.Adventure;
        private LudoAIDifficulty setupDifficulty = LudoAIDifficulty.Normal;
        private bool setupElementalRules = true;
        private int setupSeatIndex;
        private LudoElement setupRunElement = LudoElement.Fire;

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
                controller = FindFirstObjectByType<LudoGameController>();
                if (controller == null)
                {
                    return;
                }
            }

            bool shouldShow = controller.IsInitialized && controller.AwaitingSetup;
            // Enter Play Mode can be configured without a domain reload. In that
            // case the managed flags survive while Unity has already discarded
            // the transient canvas, so rebuild instead of trusting stale state.
            if (shouldShow && (!built || canvasRoot == null || !canvasRoot.activeSelf))
            {
                visible = false;
                if (canvasRoot == null)
                {
                    built = false;
                }
            }

            if (shouldShow != visible)
            {
                visible = shouldShow;
                if (visible)
                {
                    EnsureBuilt();
                    ApplyDefaultsOnce();
                    // The reference keeps the actual board visible behind the
                    // hub. The old menu diorama used a separate close camera
                    // and enlarged abstract pieces, so it is deliberately kept
                    // hidden here.
                    HideAllMenuBackdrops();
                    canvasRoot.SetActive(true);
                    ShowScreen(FrontScreen.Hub);
                }
                else
                {
                    if (canvasRoot != null)
                    {
                        canvasRoot.SetActive(false);
                    }

                    if (backdrop != null)
                    {
                        backdrop.Hide();
                    }
                }
            }

            if (visible && built)
            {
                AnimateInterface();
            }
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in generatedSprites)
            {
                if (sprite != null)
                {
                    Destroy(sprite);
                }
            }

            foreach (Texture2D texture in generatedTextures)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            generatedSprites.Clear();
            generatedTextures.Clear();
        }

        private void ApplyDefaultsOnce()
        {
            if (defaultsApplied)
            {
                return;
            }

            setupMode = controller.DefaultMode;
            setupElementalRules = LudoMatchSettings.SupportsElementalRules(setupMode) &&
                                  setupMode == LudoGameMode.Adventure;
            setupSeatIndex = 0;
            setupRunElement = FirstUnlockedElement();
            defaultsApplied = true;
        }

        private void HideAllMenuBackdrops()
        {
            LudoStartMenuBackdrop[] candidates = FindObjectsByType<LudoStartMenuBackdrop>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (LudoStartMenuBackdrop candidate in candidates)
            {
                candidate.Hide();
                if (backdrop == null)
                {
                    backdrop = candidate;
                }
            }

            // Also protects Enter Play Mode sessions without scene reload,
            // where an orphaned transient camera may outlive its owner.
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera != null && camera.name == "StartMenuCamera")
                {
                    camera.enabled = false;
                }
            }
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            EnsureEventSystem();
            font = ResolveFont();

            canvasRoot = new GameObject("AdventureFrontEnd", typeof(RectTransform))
            {
                hideFlags = HideFlags.DontSave
            };
            canvasRoot.transform.SetParent(transform, false);

            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRoot.AddComponent<GraphicRaycaster>();

            hubScreen = CreateScreen("BoardHub");
            stagesScreen = CreateScreen("ElementalStages");
            settingsScreen = CreateScreen("SettingsDrawer");
            BuildHub();
            BuildStages();
            BuildSettings();
            built = true;
        }

        private GameObject CreateScreen(string name)
        {
            RectTransform rect = CreateRect(name, canvasRoot.transform);
            Stretch(rect);
            return rect.gameObject;
        }

        private void BuildHub()
        {
            AddTopBar(hubScreen.transform, "STAGE 1-4", OpenSettings, true);

            RectTransform quest = CreatePanel("Quest", hubScreen.transform, new Color(0.10f, 0.13f, 0.17f, 0.94f));
            SetRect(quest, new Vector2(20f, -92f), new Vector2(310f, 190f), new Vector2(0f, 1f));
            Text questTitle = CreateLabel("QuestTitle", quest, "▣  QUEST", 34, Color.white, TextAnchor.MiddleLeft);
            SetRect(questTitle.rectTransform, new Vector2(20f, -12f), new Vector2(270f, 50f), new Vector2(0f, 1f));
            hubQuestLabel = CreateLabel(
                "QuestCopy",
                quest,
                "●  First elemental tracker\n│\n○  Complete combat node",
                21,
                Color.white,
                TextAnchor.UpperLeft);
            SetRect(hubQuestLabel.rectTransform, new Vector2(25f, -65f), new Vector2(260f, 105f), new Vector2(0f, 1f));

            RectTransform minimap = CreatePanel("ProgressMap", hubScreen.transform, new Color(0.10f, 0.13f, 0.17f, 0.94f));
            SetRect(minimap, new Vector2(-25f, -92f), new Vector2(190f, 210f), new Vector2(1f, 1f));
            BuildMiniMap(minimap);

            RectTransform modeRibbon = CreatePanel(
                "ModeRibbon",
                hubScreen.transform,
                new Color(0.08f, 0.10f, 0.14f, 0.90f));
            SetRect(modeRibbon, Vector2.zero, new Vector2(330f, 52f), new Vector2(0.5f, 0.18f));
            hubModeLabel = CreateLabel(
                "ModeRibbon",
                modeRibbon,
                string.Empty,
                24,
                Color.white,
                TextAnchor.MiddleCenter);
            Stretch(hubModeLabel.rectTransform, 6f);

            BuildBottomBar(hubScreen.transform, StartFromHub);

            continueButton = CreateCelButton(
                "ContinueRun",
                hubScreen.transform,
                "CONTINUAR",
                LudoUITheme.Plant,
                new Vector2(-25f, -320f),
                new Vector2(185f, 52f),
                new Vector2(1f, 1f),
                ContinueRun);
        }

        private void BuildStages()
        {
            Image background = stagesScreen.AddComponent<Image>();
            background.color = new Color(0.095f, 0.12f, 0.15f, 0.98f);

            AddTopBar(stagesScreen.transform, "SELECCIÓN DE ETAPA ELEMENTAL", OpenSettings, false);

            RectTransform progressPlate = CreatePanel(
                "ProgressPlate",
                stagesScreen.transform,
                new Color(0.25f, 0.28f, 0.38f, 0.98f));
            SetRect(progressPlate, new Vector2(0f, -77f), new Vector2(300f, 56f), new Vector2(0f, 1f));
            stageProgressLabel = CreateLabel(
                "Progress",
                progressPlate,
                "PROGRESO: 1/4",
                25,
                Color.white,
                TextAnchor.MiddleCenter);
            Stretch(stageProgressLabel.rectTransform, 8f);

            RectTransform coinPlate = CreatePanel(
                "CoinPlate",
                stagesScreen.transform,
                new Color(0.25f, 0.28f, 0.38f, 0.98f));
            SetRect(coinPlate, new Vector2(0f, -77f), new Vector2(190f, 56f), new Vector2(1f, 1f));
            Text coins = CreateLabel(
                "Coins",
                coinPlate,
                "●  0",
                28,
                LudoUITheme.Reward,
                TextAnchor.MiddleCenter);
            Stretch(coins.rectTransform, 8f);

            RectTransform route = CreateRect("StageRoute", stagesScreen.transform);
            SetRect(route, new Vector2(0f, -42f), new Vector2(1380f, 620f), new Vector2(0.5f, 0.5f));

            Vector2[] positions =
            {
                new Vector2(-500f, 105f),
                new Vector2(-170f, -55f),
                new Vector2(170f, 105f),
                new Vector2(500f, -55f)
            };
            LudoElement[] elements =
            {
                LudoElement.Fire,
                LudoElement.Plant,
                LudoElement.Water,
                LudoElement.Lightning
            };

            for (int index = 0; index < elements.Length; index++)
            {
                if (index < elements.Length - 1)
                {
                    AddDottedRoute(route, positions[index], positions[index + 1]);
                }

                int captured = index;
                stageTiles.Add(CreateStageIsland(route, elements[index], index, positions[index], () => SelectStage(captured)));
            }

            RectTransform detail = CreatePanel("StageDetail", stagesScreen.transform, new Color(0.08f, 0.10f, 0.13f, 0.94f));
            SetRect(detail, new Vector2(0f, 130f), new Vector2(380f, 125f), new Vector2(0.5f, 0f));
            stageTitleLabel = CreateLabel("StageName", detail, string.Empty, 26, Color.white, TextAnchor.MiddleCenter);
            SetRect(stageTitleLabel.rectTransform, new Vector2(0f, -8f), new Vector2(340f, 52f), new Vector2(0.5f, 1f));
            stageDescriptionLabel = CreateLabel("StageDescription", detail, string.Empty, 16, LudoUITheme.TextSecondary, TextAnchor.UpperCenter);
            SetRect(stageDescriptionLabel.rectTransform, new Vector2(0f, -58f), new Vector2(340f, 54f), new Vector2(0.5f, 1f));

            BuildBottomBar(stagesScreen.transform, StartSelectedStage);
        }

        private void BuildSettings()
        {
            Image veil = settingsScreen.AddComponent<Image>();
            veil.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);

            RectTransform panel = CreatePanel("SettingsPanel", settingsScreen.transform, LudoUITheme.Panel);
            SetRect(panel, Vector2.zero, new Vector2(1050f, 700f), new Vector2(0.5f, 0.5f));

            Text title = CreateLabel("SettingsTitle", panel, "CONFIGURACIÓN", 40, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, -20f), new Vector2(800f, 70f), new Vector2(0.5f, 1f));

            CreateCelButton("Close", panel, "×", LudoUITheme.Danger, new Vector2(-22f, -20f), new Vector2(58f, 52f), new Vector2(1f, 1f), CloseSettings);

            Text modeHeader = CreateLabel("ModeHeader", panel, "MODO DE JUEGO", 21, LudoUITheme.TextSecondary, TextAnchor.MiddleLeft);
            SetRect(modeHeader.rectTransform, new Vector2(40f, -105f), new Vector2(320f, 38f), new Vector2(0f, 1f));

            RectTransform modeRow = CreateRect("ModeRow", panel);
            SetRect(modeRow, new Vector2(40f, -150f), new Vector2(970f, 115f), new Vector2(0f, 1f));
            for (int index = 0; index < Modes.Length; index++)
            {
                LudoGameMode mode = Modes[index];
                ButtonTile tile = CreateCelButton(
                    "Mode" + mode,
                    modeRow,
                    LudoGameModeInfo.DisplayName(mode).ToUpperInvariant(),
                    LudoUITheme.ModeColor(mode),
                    new Vector2(index * 245f, 0f),
                    new Vector2(225f, 92f),
                    new Vector2(0f, 1f),
                    () => SelectMode(mode));
                modeTiles.Add(tile);
            }

            difficultyGroup = CreateRect("Difficulty", panel).gameObject;
            SetRect((RectTransform)difficultyGroup.transform, new Vector2(40f, -290f), new Vector2(465f, 115f), new Vector2(0f, 1f));
            Text difficultyTitle = CreateLabel("Title", difficultyGroup.transform, "DIFICULTAD DE IA", 19, LudoUITheme.TextSecondary, TextAnchor.MiddleLeft);
            SetRect(difficultyTitle.rectTransform, Vector2.zero, new Vector2(350f, 32f), new Vector2(0f, 1f));
            ButtonTile difficultyButton = CreateCelButton("DifficultyButton", difficultyGroup.transform, string.Empty, LudoUITheme.Cyan, new Vector2(0f, -42f), new Vector2(465f, 60f), new Vector2(0f, 1f), ToggleDifficulty);
            difficultyLabel = difficultyButton.Label;

            RectTransform elementalGroup = CreateRect("Elemental", panel);
            SetRect(elementalGroup, new Vector2(-40f, -290f), new Vector2(465f, 115f), new Vector2(1f, 1f));
            Text elementalTitle = CreateLabel("Title", elementalGroup, "REGLAS ELEMENTALES", 19, LudoUITheme.TextSecondary, TextAnchor.MiddleLeft);
            SetRect(elementalTitle.rectTransform, Vector2.zero, new Vector2(350f, 32f), new Vector2(0f, 1f));
            ButtonTile elementalButton = CreateCelButton("ElementalButton", elementalGroup, string.Empty, LudoUITheme.Plant, new Vector2(0f, -42f), new Vector2(465f, 60f), new Vector2(0f, 1f), ToggleElementalRules);
            elementalLabel = elementalButton.Label;

            seatGroup = CreateRect("SeatGroup", panel).gameObject;
            SetRect((RectTransform)seatGroup.transform, new Vector2(40f, -430f), new Vector2(970f, 145f), new Vector2(0f, 1f));
            Text seatTitle = CreateLabel("SeatTitle", seatGroup.transform, "TU COLOR", 19, LudoUITheme.TextSecondary, TextAnchor.MiddleLeft);
            SetRect(seatTitle.rectTransform, Vector2.zero, new Vector2(350f, 32f), new Vector2(0f, 1f));
            for (int index = 0; index < MaxSeats; index++)
            {
                int seat = index;
                ButtonTile tile = CreateCelButton(
                    "Seat" + index,
                    seatGroup.transform,
                    string.Empty,
                    Color.white,
                    new Vector2(index * 245f, -42f),
                    new Vector2(225f, 64f),
                    new Vector2(0f, 1f),
                    () => SelectSeat(seat));
                seatTiles.Add(tile);
            }

            CreateCelButton("Apply", panel, "LISTO", LudoUITheme.Cyan, new Vector2(0f, 28f), new Vector2(310f, 76f), new Vector2(0.5f, 0f), CloseSettings);
        }

        private void AddTopBar(Transform parent, string titleText, UnityEngine.Events.UnityAction settingsAction, bool showHearts)
        {
            RectTransform bar = CreateRect("TopBar", parent);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -78f);
            bar.offsetMax = Vector2.zero;
            Image image = bar.gameObject.AddComponent<Image>();
            image.color = new Color(0.24f, 0.27f, 0.36f, 0.98f);
            AddOutline(image, 3f);

            if (showHearts)
            {
                for (int index = 0; index < 4; index++)
                {
                    Image heart = CreateRect("Heart" + index, bar).gameObject.AddComponent<Image>();
                    heart.sprite = CreateHeartSprite(index == 3 ? new Color(0.95f, 0.28f, 0.52f) : new Color(1f, 0.22f, 0.30f));
                    heart.preserveAspect = true;
                    SetRect(heart.rectTransform, new Vector2(18f + index * 62f, -10f), new Vector2(54f, 54f), new Vector2(0f, 1f));
                }
            }

            Text title = CreateLabel("Title", bar, titleText, 41, Color.white, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform);
            AddTextOutline(title, 3f);
            CreateCelButton("Settings", bar, "⚙", new Color(0.72f, 0.80f, 0.86f), new Vector2(-18f, -10f), new Vector2(58f, 55f), new Vector2(1f, 1f), settingsAction);
        }

        private void BuildBottomBar(Transform parent, UnityEngine.Events.UnityAction startAction)
        {
            RectTransform footer = CreateRect("BottomNavigation", parent);
            footer.anchorMin = Vector2.zero;
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.offsetMin = Vector2.zero;
            footer.offsetMax = new Vector2(0f, 110f);
            Image footerImage = footer.gameObject.AddComponent<Image>();
            footerImage.color = new Color(0.24f, 0.27f, 0.36f, 0.98f);
            AddOutline(footerImage, 3f);

            CreateNavItem(footer, "▱", "AVENTURA", new Vector2(18f, 8f), new Vector2(0f, 0f), () => ShowScreen(FrontScreen.Hub));
            CreateNavItem(footer, "⇧", "MEJORAS", new Vector2(208f, 8f), new Vector2(0f, 0f), OpenSettings);
            CreateNavItem(footer, "▣", "INVENTARIO", new Vector2(-398f, 8f), new Vector2(1f, 0f), OpenSettings);
            CreateNavItem(footer, "▤", "HISTORIA", new Vector2(-208f, 8f), new Vector2(1f, 0f), OpenSettings);

            ButtonTile start = CreateCelButton(
                "Start",
                footer,
                "START",
                LudoUITheme.Cyan,
                Vector2.zero,
                new Vector2(390f, 92f),
                new Vector2(0.5f, 0.5f),
                startAction,
                true);
            startLabel = start.Label;
            start.Root.transform.SetAsLastSibling();
        }

        private void CreateNavItem(
            Transform parent,
            string icon,
            string label,
            Vector2 position,
            Vector2 anchor,
            UnityEngine.Events.UnityAction action)
        {
            ButtonTile tile = CreateCelButton(
                "Nav" + label,
                parent,
                icon + "\n" + label,
                new Color(0.36f, 0.40f, 0.52f),
                position,
                new Vector2(175f, 92f),
                anchor,
                action);
            tile.Label.fontSize = 18;
        }

        private void BuildMiniMap(Transform parent)
        {
            Vector2[] points =
            {
                new Vector2(55f, -155f), new Vector2(95f, -125f), new Vector2(75f, -92f),
                new Vector2(125f, -66f), new Vector2(95f, -36f)
            };
            Color[] colors =
            {
                LudoUITheme.Water, LudoUITheme.Reward, LudoUITheme.Danger,
                LudoUITheme.Water, LudoUITheme.Danger
            };
            for (int index = 0; index < points.Length; index++)
            {
                if (index > 0)
                {
                    CreateLine(parent, points[index - 1], points[index], 5f, new Color(0.05f, 0.07f, 0.10f));
                }

                Image dot = CreateRect("Node" + index, parent).gameObject.AddComponent<Image>();
                dot.color = colors[index];
                SetRect(dot.rectTransform, points[index] - new Vector2(8f, 8f), new Vector2(16f, 16f), new Vector2(0f, 1f));
                AddOutline(dot, 2f);
            }

            Text caption = CreateLabel("Caption", parent, "PROGRESO HASTA\nEL JEFE FINAL", 15, Color.white, TextAnchor.MiddleCenter);
            SetRect(caption.rectTransform, new Vector2(0f, 35f), new Vector2(190f, 42f), new Vector2(0.5f, 0f));
        }

        private ButtonTile CreateStageIsland(Transform parent, LudoElement element, int index, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            Color colour = LudoUITheme.ElementColor(element);
            ButtonTile tile = CreateCelButton(
                "Stage" + element,
                parent,
                StageIcon(element),
                colour,
                position,
                new Vector2(235f, 135f),
                new Vector2(0.5f, 0.5f),
                action,
                true);
            tile.Label.fontSize = 48;

            Text name = CreateLabel(
                "Name",
                tile.Root.transform,
                $"STAGE {index + 1}\n{StageName(element)}",
                18,
                Color.white,
                TextAnchor.MiddleCenter);
            Vector2 captionPosition = index % 2 == 0
                ? new Vector2(0f, -142f)
                : new Vector2(0f, 118f);
            SetRect(name.rectTransform, captionPosition, new Vector2(270f, 64f), new Vector2(0.5f, 0.5f));
            AddTextOutline(name, 2f);
            tile.Caption = name;
            return tile;
        }

        private void AddDottedRoute(Transform parent, Vector2 from, Vector2 to)
        {
            const int dots = 9;
            for (int index = 1; index < dots; index++)
            {
                Vector2 point = Vector2.Lerp(from, to, index / (float)dots);
                Image dot = CreateRect("RouteDot", parent).gameObject.AddComponent<Image>();
                dot.color = new Color(1f, 0.82f, 0.35f, 0.92f);
                SetRect(dot.rectTransform, point - new Vector2(6f, 6f), new Vector2(12f, 12f), new Vector2(0.5f, 0.5f));
            }
        }

        private void ShowScreen(FrontScreen next)
        {
            screen = next;
            hubScreen.SetActive(next == FrontScreen.Hub);
            stagesScreen.SetActive(next == FrontScreen.Stages);
            settingsScreen.SetActive(next == FrontScreen.Settings);
            RefreshAll();
        }

        private void StartFromHub()
        {
            if (setupMode == LudoGameMode.Adventure)
            {
                ShowScreen(FrontScreen.Stages);
                return;
            }

            controller.StartMatch(BuildMatchSettings());
        }

        private void StartSelectedStage()
        {
            controller.StartRun(SelectedRunElement());
        }

        private void ContinueRun()
        {
            controller.ContinueRun();
        }

        private void OpenSettings()
        {
            ShowScreen(FrontScreen.Settings);
        }

        private void CloseSettings()
        {
            ShowScreen(FrontScreen.Hub);
        }

        private void SelectMode(LudoGameMode mode)
        {
            setupMode = mode;
            setupElementalRules = LudoMatchSettings.SupportsElementalRules(mode) && mode == LudoGameMode.Adventure;
            RefreshAll();
        }

        private void SelectStage(int index)
        {
            IReadOnlyList<LudoElement> order = controller.ElementProgress.UnlockOrder;
            if (index < 0 || index >= order.Count || !controller.ElementProgress.IsUnlocked(order[index]))
            {
                return;
            }

            setupRunElement = order[index];
            RefreshAll();
        }

        private void ToggleDifficulty()
        {
            setupDifficulty = setupDifficulty == LudoAIDifficulty.Easy
                ? LudoAIDifficulty.Normal
                : LudoAIDifficulty.Easy;
            RefreshAll();
        }

        private void ToggleElementalRules()
        {
            if (LudoMatchSettings.SupportsElementalRules(setupMode))
            {
                setupElementalRules = !setupElementalRules;
                RefreshAll();
            }
        }

        private void SelectSeat(int index)
        {
            setupSeatIndex = index;
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (!built || controller == null)
            {
                return;
            }

            hubModeLabel.text = LudoGameModeInfo.DisplayName(setupMode).ToUpperInvariant();
            bool canContinue = setupMode == LudoGameMode.Adventure && controller.HasSavedRun;
            continueButton.Root.SetActive(canContinue);
            continueButton.Shadow.SetActive(canContinue);
            hubQuestLabel.text = setupMode == LudoGameMode.Adventure
                ? "●  Sigue el rastro elemental\n│\n○  Completa un nodo de combate"
                : "●  Prepara tus cuatro fichas\n│\n○  Lleva todas hasta la meta";

            for (int index = 0; index < modeTiles.Count; index++)
            {
                bool selected = Modes[index] == setupMode;
                SetTileState(modeTiles[index], selected, LudoUITheme.ModeColor(Modes[index]));
            }

            bool hasAI = setupMode != LudoGameMode.Multiplayer;
            difficultyGroup.SetActive(hasAI);
            seatGroup.SetActive(true);
            difficultyLabel.text = setupDifficulty == LudoAIDifficulty.Easy ? "FÁCIL" : "NORMAL";
            bool supportsElemental = LudoMatchSettings.SupportsElementalRules(setupMode);
            elementalLabel.text = supportsElemental
                ? setupElementalRules ? "ACTIVADAS" : "DESACTIVADAS"
                : "NO DISPONIBLES";

            IReadOnlyList<LudoPlayerState> players = controller.Players;
            for (int index = 0; index < seatTiles.Count; index++)
            {
                ButtonTile tile = seatTiles[index];
                bool exists = index < players.Count;
                tile.Root.SetActive(exists);
                tile.Shadow.SetActive(exists);
                if (!exists)
                {
                    continue;
                }

                LudoPlayerState player = players[index];
                tile.Label.text = LudoGameController.SpanishColorName(player.Style.PlayerId).ToUpperInvariant();
                SetTileState(tile, index == setupSeatIndex, player.Style.TokenColor);
                tile.Button.interactable = hasAI;
            }

            RefreshStages();
        }

        private void RefreshStages()
        {
            LudoElementProgress progress = controller.ElementProgress;
            IReadOnlyList<LudoElement> order = progress.UnlockOrder;
            int unlockedCount = 0;
            for (int index = 0; index < stageTiles.Count; index++)
            {
                ButtonTile tile = stageTiles[index];
                if (index >= order.Count)
                {
                    tile.Root.SetActive(false);
                    tile.Shadow.SetActive(false);
                    continue;
                }

                LudoElement element = order[index];
                bool unlocked = progress.IsUnlocked(element);
                if (unlocked)
                {
                    unlockedCount++;
                }

                tile.Button.interactable = unlocked;
                tile.Label.text = unlocked ? StageIcon(element) : "LOCKED";
                tile.Label.fontSize = unlocked ? 48 : 22;
                tile.Caption.text = $"STAGE {index + 1}\n{StageName(element)}";
                SetTileState(tile, unlocked && element == SelectedRunElement(), LudoUITheme.ElementColor(element));
                if (!unlocked)
                {
                    tile.Image.color = new Color(0.34f, 0.36f, 0.39f, 1f);
                    tile.Caption.color = new Color(0.72f, 0.74f, 0.78f);
                }
                else
                {
                    tile.Caption.color = Color.white;
                }
            }

            stageProgressLabel.text = $"PROGRESO: {unlockedCount}/4";
            LudoElement selected = SelectedRunElement();
            stageTitleLabel.text = StageName(selected).ToUpperInvariant();
            stageDescriptionLabel.text = LudoElementInfo.RuleSummary(selected);
        }

        private void SetTileState(ButtonTile tile, bool selected, Color colour)
        {
            tile.Selected = selected;
            tile.BaseColor = colour;
            tile.Image.color = selected ? colour : Color.Lerp(colour, LudoUITheme.Card, 0.58f);
            tile.Root.transform.localScale = selected ? Vector3.one * 1.04f : Vector3.one;
        }

        private void AnimateInterface()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            IReadOnlyList<ButtonTile> list = screen == FrontScreen.Stages ? stageTiles : modeTiles;
            foreach (ButtonTile tile in list)
            {
                if (tile.Selected && tile.Root.activeInHierarchy)
                {
                    float scale = 1.035f + pulse * 0.018f;
                    tile.Root.transform.localScale = Vector3.one * scale;
                    tile.Image.color = Color.Lerp(tile.BaseColor, Color.white, pulse * 0.09f);
                }
            }
        }

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

            return FirstUnlockedElement();
        }

        private LudoElement FirstUnlockedElement()
        {
            foreach (LudoElement element in controller.ElementProgress.Unlocked())
            {
                return element;
            }

            return LudoElement.Fire;
        }

        private LudoMatchSettings BuildMatchSettings()
        {
            IReadOnlyList<LudoPlayerState> seats = controller.Players;
            int seatIndex = Mathf.Clamp(setupSeatIndex, 0, Mathf.Max(0, seats.Count - 1));
            LudoElement element = seats.Count > 0 ? seats[seatIndex].Element : default;
            return new LudoMatchSettings(setupMode, element, setupDifficulty, setupElementalRules);
        }

        private static string StageName(LudoElement element)
        {
            return element switch
            {
                LudoElement.Fire => "LLANURAS VOLCÁNICAS",
                LudoElement.Plant => "BOSQUE SUSURRANTE",
                LudoElement.Water => "AGUAS ABISALES",
                LudoElement.Lightning => "CUMBRES DEL TRUENO",
                _ => LudoElementInfo.DisplayName(element)
            };
        }

        private static string StageIcon(LudoElement element)
        {
            return element switch
            {
                LudoElement.Fire => "♨",
                LudoElement.Plant => "♣",
                LudoElement.Water => "●",
                LudoElement.Lightning => "ϟ",
                _ => "◆"
            };
        }

        private ButtonTile CreateCelButton(
            string name,
            Transform parent,
            string text,
            Color colour,
            Vector2 position,
            Vector2 size,
            Vector2 anchor,
            UnityEngine.Events.UnityAction action,
            bool hex = false)
        {
            RectTransform shadow = CreateRect(name + "Shadow", parent);
            SetRect(shadow, position + new Vector2(7f, -9f), size, anchor);
            Image shadowImage = shadow.gameObject.AddComponent<Image>();
            shadowImage.color = new Color(0.02f, 0.025f, 0.035f, 0.72f);
            if (hex)
            {
                shadowImage.sprite = CreateHexSprite(Color.black);
                shadowImage.type = Image.Type.Sliced;
            }

            RectTransform root = CreateRect(name, parent);
            SetRect(root, position, size, anchor);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = colour;
            if (hex)
            {
                image.sprite = CreateHexSprite(Color.white);
                image.type = Image.Type.Sliced;
            }
            AddOutline(image, 3f);
            AddBevel(root);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            Text label = CreateLabel(name + "Label", root, text, 24, Color.white, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 12f);
            AddTextOutline(label, 2f);
            return new ButtonTile(root.gameObject, shadow.gameObject, button, image, label, colour);
        }

        private RectTransform CreatePanel(string name, Transform parent, Color colour)
        {
            RectTransform panel = CreateRect(name, parent);
            Image image = panel.gameObject.AddComponent<Image>();
            image.color = colour;
            AddOutline(image, 4f);
            Shadow shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
            shadow.effectDistance = new Vector2(8f, -9f);
            AddBevel(panel);
            return panel;
        }

        private void AddBevel(RectTransform root)
        {
            Image top = CreateRect("Highlight", root).gameObject.AddComponent<Image>();
            top.color = new Color(1f, 1f, 1f, 0.24f);
            top.raycastTarget = false;
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.offsetMin = new Vector2(6f, -6f);
            top.rectTransform.offsetMax = new Vector2(-6f, -2f);

            Image bottom = CreateRect("Shade", root).gameObject.AddComponent<Image>();
            bottom.color = new Color(0f, 0f, 0f, 0.28f);
            bottom.raycastTarget = false;
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            bottom.rectTransform.offsetMin = new Vector2(6f, 3f);
            bottom.rectTransform.offsetMax = new Vector2(-6f, 9f);
        }

        private void CreateLine(Transform parent, Vector2 from, Vector2 to, float width, Color colour)
        {
            Vector2 delta = to - from;
            Image line = CreateRect("Line", parent).gameObject.AddComponent<Image>();
            line.color = colour;
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, width);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private Sprite CreateHexSprite(Color colour)
        {
            const int width = 128;
            const int height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ProceduralCelHex",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color clear = Color.clear;
            for (int y = 0; y < height; y++)
            {
                float ny = y / (height - 1f);
                float inset = Mathf.Abs(ny - 0.5f) * 0.34f;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (width - 1f);
                    bool inside = nx >= inset && nx <= 1f - inset;
                    texture.SetPixel(x, y, inside ? colour : clear);
                }
            }
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(22f, 14f, 22f, 14f));
            generatedTextures.Add(texture);
            generatedSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateHeartSprite(Color fill)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralHeart",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x / (size - 1f) - 0.5f) * 2.2f;
                    float py = (y / (size - 1f) - 0.48f) * 2.2f;
                    float q = px * px + py * py - 1f;
                    bool inside = q * q * q - px * px * py * py * py <= 0f;
                    texture.SetPixel(x, y, inside ? fill : Color.clear);
                }
            }
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            generatedTextures.Add(texture);
            generatedSprites.Add(sprite);
            return sprite;
        }

        private Text CreateLabel(string name, Transform parent, string value, int size, Color colour, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.color = colour;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform))
            {
                hideFlags = HideFlags.DontSave
            };
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void AddOutline(Graphic graphic, float width)
        {
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.025f, 0.035f, 0.045f, 1f);
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        private static void AddTextOutline(Text text, float width)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        private static Font ResolveFont()
        {
            Font resolved = Font.CreateDynamicFontFromOSFont("Arial Black", 20);
            return resolved != null
                ? resolved
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem")
            {
                hideFlags = HideFlags.DontSave
            };
            go.AddComponent<EventSystem>();
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private sealed class ButtonTile
        {
            public ButtonTile(
                GameObject root,
                GameObject shadow,
                Button button,
                Image image,
                Text label,
                Color baseColor)
            {
                Root = root;
                Shadow = shadow;
                Button = button;
                Image = image;
                Label = label;
                BaseColor = baseColor;
            }

            public GameObject Root { get; }
            public GameObject Shadow { get; }
            public Button Button { get; }
            public Image Image { get; }
            public Text Label { get; }
            public Text Caption { get; set; }
            public Color BaseColor { get; set; }
            public bool Selected { get; set; }
        }
    }
}
