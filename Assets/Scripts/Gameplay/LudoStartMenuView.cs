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
    /// The real start menu: a uGUI screen over the procedural
    /// <see cref="LudoStartMenuBackdrop"/>, replacing the IMGUI popup that
    /// used to sit directly on top of the live board.
    ///
    /// Built entirely from code, the same way <see cref="LudoRunMapView"/>
    /// and the combat arena build their own screens — nothing is placed by
    /// hand in the scene, so it wires itself in the moment a
    /// <see cref="LudoGameController"/> exists.
    ///
    /// Same choices as the panel it replaces, ported one for one: mode, AI
    /// difficulty, elemental rules, the Adventure element/continue picker or
    /// the seat picker, and the start button. Everything it decides is
    /// still handed to <see cref="LudoGameController"/> exactly as before —
    /// only the surface changed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LudoStartMenuView : MonoBehaviour
    {
        private const int MaxSeats = 4;
        private const float PanelWidth = 1000f;
        private const float PanelHeight = 640f;
        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;

        private static readonly LudoGameMode[] SelectableModes =
        {
            LudoGameMode.Classic,
            LudoGameMode.Adventure,
            LudoGameMode.Hardcore,
            LudoGameMode.Multiplayer
        };

        private static readonly Color PanelBackground = new Color(0.045f, 0.045f, 0.07f, 0.94f);
        private static readonly Color TitleColor = Color.white;
        private static readonly Color SubtitleColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color SectionLabelColor = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color ChipOnColor = new Color(0.2f, 0.65f, 0.35f, 0.9f);
        private static readonly Color ChipOffColor = new Color(1f, 1f, 1f, 0.09f);
        private static readonly Color ChipOffTextColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color LockedTextColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color PrimaryButtonColor = new Color(0.22f, 0.72f, 0.42f, 0.95f);
        private static readonly Color ContinueButtonColor = new Color(0.22f, 0.5f, 0.78f, 0.95f);

        [SerializeField] private LudoGameController controller;

        private LudoStartMenuBackdrop backdrop;
        private Font font;
        private GameObject canvasRoot;
        private bool built;
        private bool visible;

        // Selection state, ported from the IMGUI menu it replaces.
        private LudoGameMode setupMode;
        private LudoAIDifficulty setupDifficulty = LudoAIDifficulty.Normal;
        private bool setupElementalRules;
        private int setupSeatIndex;
        private LudoElement setupRunElement = LudoElement.Fire;
        private bool defaultsApplied;

        private readonly List<Chip> modeChips = new List<Chip>(SelectableModes.Length);
        private GameObject difficultySection;
        private Chip difficultyEasyChip;
        private Chip difficultyNormalChip;
        private Text difficultyHintLabel;
        private Text noAIHintLabel;
        private GameObject elementalSection;
        private Chip elementalToggleChip;
        private Text elementalHintLabel;
        private Text classicHintLabel;
        private GameObject adventureOptions;
        private Text elementHintLabel;
        private readonly List<Chip> elementChips = new List<Chip>(4);
        private GameObject seatOptions;
        private Text seatHeaderLabel;
        private readonly List<Chip> seatChips = new List<Chip>(MaxSeats);
        private Chip continueChip;
        private Chip startChip;

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
            if (shouldShow == visible)
            {
                return;
            }

            visible = shouldShow;
            if (visible)
            {
                EnsureBuilt();
                ApplyDefaultsOnce();
                RefreshAll();
                canvasRoot.SetActive(true);
                EnsureBackdrop();
                backdrop.Show();
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

        private void ApplyDefaultsOnce()
        {
            if (defaultsApplied)
            {
                return;
            }

            setupMode = controller.DefaultMode;
            setupElementalRules = DefaultElementalRulesFor(setupMode);
            setupSeatIndex = 0;
            defaultsApplied = true;
        }

        private static bool DefaultElementalRulesFor(LudoGameMode mode)
        {
            return mode == LudoGameMode.Adventure;
        }

        private void EnsureBackdrop()
        {
            if (backdrop != null)
            {
                return;
            }

            backdrop = FindFirstObjectByType<LudoStartMenuBackdrop>();
            if (backdrop == null)
            {
                GameObject backdropObject = new GameObject("StartMenuBackdrop")
                {
                    hideFlags = HideFlags.DontSave
                };
                backdropObject.transform.SetParent(transform, false);
                backdrop = backdropObject.AddComponent<LudoStartMenuBackdrop>();
            }
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            EnsureEventSystem();
            font = ResolveDefaultFont();

            canvasRoot = new GameObject("StartMenuCanvas", typeof(RectTransform))
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
            scaler.matchWidthOrHeight = 0.5f;

            canvasRoot.AddComponent<GraphicRaycaster>();

            BuildPanel(canvasRoot.transform);

            canvasRoot.SetActive(false);
            built = true;
        }

        /// <summary>
        /// The project runs exclusively on the new Input System (see
        /// ProjectSettings), so the classic StandaloneInputModule would never
        /// receive a click — every button on this menu would be dead without
        /// this module instead.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem")
            {
                hideFlags = HideFlags.DontSave
            };
            eventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule inputModule =
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private static Font ResolveDefaultFont()
        {
            Font resolved = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (resolved == null)
            {
                resolved = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return resolved != null ? resolved : Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        private void BuildPanel(Transform canvasTransform)
        {
            RectTransform panel = CreateRect("Panel", canvasTransform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.gameObject.AddComponent<Image>().color = PanelBackground;

            AddVertical(panel.gameObject, 10f, new RectOffset(28, 28, 24, 24));

            Text title = CreateLabel(
                "Title", panel, "ELEMENTAL LUDO", 26, TitleColor, FontStyle.Bold);
            AddLayoutElement(title.gameObject, preferredHeight: 34f);

            Text subtitle = CreateLabel(
                "Subtitle", panel, "Elige cómo quieres jugar", 14, SubtitleColor);
            AddLayoutElement(subtitle.gameObject, preferredHeight: 20f);

            AddLayoutElement(CreateRect("Spacer", panel).gameObject, preferredHeight: 6f);

            RectTransform columns = CreateRect("Columns", panel);
            AddHorizontal(columns.gameObject, 24f);
            AddLayoutElement(columns.gameObject, flexibleHeight: 1f);

            BuildModeColumn(columns);
            BuildOptionsColumn(columns);
        }

        private void BuildModeColumn(Transform parent)
        {
            RectTransform column = CreateRect("ModeColumn", parent);
            AddLayoutElement(column.gameObject, preferredWidth: 340f);
            AddVertical(column.gameObject, 8f);

            Text header = CreateLabel(
                "ModeHeader", column, "MODO", 12, SectionLabelColor, FontStyle.Bold);
            AddLayoutElement(header.gameObject, preferredHeight: 18f);

            foreach (LudoGameMode mode in SelectableModes)
            {
                modeChips.Add(CreateModeCard(column, mode));
            }
        }

        private Chip CreateModeCard(Transform parent, LudoGameMode mode)
        {
            RectTransform root = CreateRect($"Mode_{mode}", parent);
            AddLayoutElement(root.gameObject, preferredHeight: 76f);

            Image background = root.gameObject.AddComponent<Image>();
            background.color = ChipOffColor;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => SelectMode(mode));

            AddVertical(root.gameObject, 2f, new RectOffset(14, 14, 10, 10));

            Text name = CreateLabel(
                "Name", root, LudoGameModeInfo.DisplayName(mode), 15, Color.white, FontStyle.Bold);
            Text summary = CreateLabel(
                "Summary", root, LudoGameModeInfo.Summary(mode), 11, HintColor);
            summary.verticalOverflow = VerticalWrapMode.Truncate;
            AddLayoutElement(summary.gameObject, flexibleHeight: 1f);

            return new Chip
            {
                Root = root.gameObject,
                Button = button,
                Background = background,
                Label = name,
                Hint = summary
            };
        }

        private void BuildOptionsColumn(Transform parent)
        {
            RectTransform column = CreateRect("OptionsColumn", parent);
            AddLayoutElement(column.gameObject, flexibleWidth: 1f);
            AddVertical(column.gameObject, 10f);

            Text header = CreateLabel(
                "OptionsHeader", column, "OPCIONES", 12, SectionLabelColor, FontStyle.Bold);
            AddLayoutElement(header.gameObject, preferredHeight: 18f);

            BuildDifficultySection(column);
            noAIHintLabel = CreateLabel(
                "NoAIHint", column, "Sin IA: los cuatro colores son humanos.", 11, HintColor);
            AddLayoutElement(noAIHintLabel.gameObject, preferredHeight: 18f);

            BuildElementalSection(column);
            classicHintLabel = CreateLabel(
                "ClassicHint", column, "Clásico no admite reglas elementales.", 11, HintColor);
            AddLayoutElement(classicHintLabel.gameObject, preferredHeight: 18f);

            BuildAdventureOptions(column);
            BuildSeatOptions(column);

            AddLayoutElement(CreateRect("Flex", column).gameObject, flexibleHeight: 1f);

            continueChip = CreateChip(
                "ContinueButton", column, "CONTINUAR AVENTURA", 14);
            AddLayoutElement(continueChip.Root, preferredHeight: 46f);
            continueChip.Background.color = ContinueButtonColor;
            continueChip.Button.onClick.AddListener(OnContinueClicked);

            startChip = CreateChip("StartButton", column, "EMPEZAR", 16);
            AddLayoutElement(startChip.Root, preferredHeight: 52f);
            startChip.Background.color = PrimaryButtonColor;
            startChip.Button.onClick.AddListener(OnStartClicked);
        }

        private void BuildDifficultySection(Transform parent)
        {
            difficultySection = CreateRect("DifficultySection", parent).gameObject;
            AddVertical(difficultySection, 4f);

            Text header = CreateLabel(
                "DifficultyHeader",
                difficultySection.transform,
                "Dificultad de la IA",
                12,
                Color.white,
                FontStyle.Bold);
            AddLayoutElement(header.gameObject, preferredHeight: 16f);

            RectTransform row = CreateRect("DifficultyRow", difficultySection.transform);
            AddHorizontal(row.gameObject, 8f);
            AddLayoutElement(row.gameObject, preferredHeight: 34f);

            difficultyEasyChip = CreateChip("DifficultyEasy", row, "Fácil", 12);
            AddLayoutElement(difficultyEasyChip.Root, flexibleWidth: 1f);
            difficultyEasyChip.Button.onClick.AddListener(
                () => SelectDifficulty(LudoAIDifficulty.Easy));

            difficultyNormalChip = CreateChip("DifficultyNormal", row, "Normal", 12);
            AddLayoutElement(difficultyNormalChip.Root, flexibleWidth: 1f);
            difficultyNormalChip.Button.onClick.AddListener(
                () => SelectDifficulty(LudoAIDifficulty.Normal));

            difficultyHintLabel = CreateLabel(
                "DifficultyHint", difficultySection.transform, string.Empty, 11, HintColor);
            AddLayoutElement(difficultyHintLabel.gameObject, preferredHeight: 32f);
        }

        private void BuildElementalSection(Transform parent)
        {
            elementalSection = CreateRect("ElementalSection", parent).gameObject;
            AddVertical(elementalSection, 4f);

            elementalToggleChip = CreateChip(
                "ElementalToggle", elementalSection.transform, string.Empty, 12);
            AddLayoutElement(elementalToggleChip.Root, preferredHeight: 34f);
            elementalToggleChip.Button.onClick.AddListener(ToggleElementalRules);

            elementalHintLabel = CreateLabel(
                "ElementalHint", elementalSection.transform, string.Empty, 11, HintColor);
            AddLayoutElement(elementalHintLabel.gameObject, preferredHeight: 32f);
        }

        private void BuildAdventureOptions(Transform parent)
        {
            adventureOptions = CreateRect("AdventureOptions", parent).gameObject;
            AddVertical(adventureOptions, 4f);

            Text header = CreateLabel(
                "ElementHeader",
                adventureOptions.transform,
                "TU ELEMENTO",
                12,
                SectionLabelColor,
                FontStyle.Bold);
            AddLayoutElement(header.gameObject, preferredHeight: 16f);

            elementHintLabel = CreateLabel(
                "ElementHint", adventureOptions.transform, string.Empty, 11, HintColor);
            AddLayoutElement(elementHintLabel.gameObject, preferredHeight: 16f);

            // Exactly four elements, always — the roster is fixed, so the
            // rows can be built once here instead of instantiated per frame.
            for (int index = 0; index < 4; index++)
            {
                Chip chip = CreateChip(
                    $"Element_{index}",
                    adventureOptions.transform,
                    string.Empty,
                    12,
                    TextAnchor.MiddleLeft);
                AddLayoutElement(chip.Root, preferredHeight: 32f);
                int captured = index;
                chip.Button.onClick.AddListener(() => SelectRunElementAt(captured));
                elementChips.Add(chip);
            }
        }

        private void BuildSeatOptions(Transform parent)
        {
            seatOptions = CreateRect("SeatOptions", parent).gameObject;
            AddVertical(seatOptions, 4f);

            seatHeaderLabel = CreateLabel(
                "SeatHeader",
                seatOptions.transform,
                "TU COLOR",
                12,
                SectionLabelColor,
                FontStyle.Bold);
            AddLayoutElement(seatHeaderLabel.gameObject, preferredHeight: 16f);

            for (int index = 0; index < MaxSeats; index++)
            {
                Chip chip = CreateAccentedChip($"Seat_{index}", seatOptions.transform);
                AddLayoutElement(chip.Root, preferredHeight: 32f);
                int captured = index;
                chip.Button.onClick.AddListener(() => SelectSeat(captured));
                seatChips.Add(chip);
            }
        }

        // ------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------

        private void SelectMode(LudoGameMode mode)
        {
            setupMode = mode;
            setupElementalRules =
                LudoMatchSettings.SupportsElementalRules(mode) && DefaultElementalRulesFor(mode);
            RefreshAll();
        }

        private void SelectDifficulty(LudoAIDifficulty difficulty)
        {
            setupDifficulty = difficulty;
            RefreshAll();
        }

        private void ToggleElementalRules()
        {
            setupElementalRules = !setupElementalRules;
            RefreshAll();
        }

        private void SelectRunElementAt(int index)
        {
            IReadOnlyList<LudoElement> order = controller.ElementProgress.UnlockOrder;
            if (index < 0 || index >= order.Count || !controller.ElementProgress.IsUnlocked(order[index]))
            {
                return;
            }

            setupRunElement = order[index];
            RefreshAll();
        }

        private void SelectSeat(int index)
        {
            setupSeatIndex = index;
            RefreshAll();
        }

        private void OnStartClicked()
        {
            if (setupMode == LudoGameMode.Adventure)
            {
                controller.StartRun(SelectedRunElement());
                return;
            }

            controller.StartMatch(BuildMatchSettings());
        }

        private void OnContinueClicked()
        {
            controller.ContinueRun();
        }

        /// <summary>
        /// The element the run starts with, clamped to what has been
        /// unlocked so a stale selection can never smuggle in a locked one.
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

        private LudoMatchSettings BuildMatchSettings()
        {
            IReadOnlyList<LudoPlayerState> seats = controller.Players;
            int seatIndex = Mathf.Clamp(setupSeatIndex, 0, Mathf.Max(0, seats.Count - 1));
            LudoElement seatElement = seats.Count > 0 ? seats[seatIndex].Element : default;

            return new LudoMatchSettings(setupMode, seatElement, setupDifficulty, setupElementalRules);
        }

        // ------------------------------------------------------------------
        // Refresh
        // ------------------------------------------------------------------

        private void RefreshAll()
        {
            if (!built || controller == null)
            {
                return;
            }

            bool hasAI = setupMode != LudoGameMode.Multiplayer;
            bool supportsElemental = LudoMatchSettings.SupportsElementalRules(setupMode);
            bool isAdventure = setupMode == LudoGameMode.Adventure;

            RefreshModeCards();

            difficultySection.SetActive(hasAI);
            noAIHintLabel.gameObject.SetActive(!hasAI);
            if (hasAI)
            {
                SetChipState(difficultyEasyChip, setupDifficulty == LudoAIDifficulty.Easy);
                SetChipState(difficultyNormalChip, setupDifficulty == LudoAIDifficulty.Normal);
                difficultyHintLabel.text = setupDifficulty == LudoAIDifficulty.Easy
                    ? "Avanza al azar y saca fichas cuando puede. Si captura, es casualidad."
                    : "Prioriza capturar, formar barreras y no quedarse a tiro.";
            }

            elementalSection.SetActive(supportsElemental);
            classicHintLabel.gameObject.SetActive(!supportsElemental);
            if (supportsElemental)
            {
                elementalToggleChip.Label.text = setupElementalRules
                    ? "REGLAS ELEMENTALES: ON"
                    : "REGLAS ELEMENTALES: OFF";
                SetChipState(elementalToggleChip, setupElementalRules);
                elementalHintLabel.text = setupElementalRules
                    ? "Cada color juega con el poder de su elemento."
                    : "Todos los colores juegan con las mismas reglas.";
            }

            adventureOptions.SetActive(isAdventure);
            seatOptions.SetActive(!isAdventure);

            if (isAdventure)
            {
                RefreshAdventureOptions();
            }
            else
            {
                RefreshSeatOptions(hasAI);
            }

            continueChip.Root.SetActive(isAdventure && controller.HasSavedRun);
        }

        private void RefreshModeCards()
        {
            for (int index = 0; index < modeChips.Count && index < SelectableModes.Length; index++)
            {
                SetChipState(modeChips[index], SelectableModes[index] == setupMode);
            }
        }

        private void RefreshAdventureOptions()
        {
            LudoElementProgress progress = controller.ElementProgress;
            elementHintLabel.text = progress.AllUnlocked
                ? "Los tienes todos."
                : "Completa una run para desbloquear el siguiente.";

            IReadOnlyList<LudoElement> order = progress.UnlockOrder;
            LudoElement selected = SelectedRunElement();

            for (int index = 0; index < elementChips.Count; index++)
            {
                Chip chip = elementChips[index];
                if (index >= order.Count)
                {
                    chip.Root.SetActive(false);
                    continue;
                }

                chip.Root.SetActive(true);
                LudoElement element = order[index];
                bool unlocked = progress.IsUnlocked(element);
                bool isSelected = unlocked && element == selected;

                chip.Label.text = unlocked
                    ? LudoElementInfo.DisplayName(element)
                    : $"{LudoElementInfo.DisplayName(element)}  (bloqueado)";
                chip.Button.interactable = unlocked;

                if (!unlocked)
                {
                    chip.Background.color = ChipOffColor;
                    chip.Label.color = LockedTextColor;
                }
                else
                {
                    SetChipState(chip, isSelected);
                }
            }
        }

        private void RefreshSeatOptions(bool hasAI)
        {
            seatHeaderLabel.text = hasAI ? "TU COLOR" : "COLORES EN JUEGO";

            IReadOnlyList<LudoPlayerState> seats = controller.Players;
            for (int index = 0; index < seatChips.Count; index++)
            {
                Chip chip = seatChips[index];
                if (index >= seats.Count)
                {
                    chip.Root.SetActive(false);
                    continue;
                }

                chip.Root.SetActive(true);
                LudoPlayerState seat = seats[index];
                bool isChosen = hasAI && index == setupSeatIndex;

                chip.Label.text = setupElementalRules
                    ? ElementHeading(seat)
                    : LudoGameController.SpanishColorName(seat.Style.PlayerId);
                chip.Button.interactable = hasAI;
                if (chip.Accent != null)
                {
                    chip.Accent.color = seat.Style.TokenColor;
                }

                SetChipState(chip, isChosen);
            }
        }

        private static string ElementHeading(LudoPlayerState seat)
        {
            return $"{LudoElementInfo.DisplayName(seat.Element)} " +
                   $"({LudoGameController.SpanishColorName(seat.Style.PlayerId)})";
        }

        private static void SetChipState(Chip chip, bool on)
        {
            chip.Background.color = on ? ChipOnColor : ChipOffColor;
            if (chip.Label != null)
            {
                chip.Label.color = on ? Color.white : ChipOffTextColor;
            }
        }

        // ------------------------------------------------------------------
        // uGUI helpers
        // ------------------------------------------------------------------

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

        private Text CreateLabel(
            string name,
            Transform parent,
            string text,
            int fontSize,
            Color color,
            FontStyle style = FontStyle.Normal,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            RectTransform rect = CreateRect(name, parent);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        /// <summary>A plain toggle-style button: coloured background plus one centred label.</summary>
        private Chip CreateChip(
            string name,
            Transform parent,
            string text,
            int fontSize,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform root = CreateRect(name, parent);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = ChipOffColor;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            RectTransform labelRect = CreateRect(name + "Label", root);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 4f);
            labelRect.offsetMax = new Vector2(-10f, -4f);

            Text label = labelRect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            return new Chip
            {
                Root = root.gameObject,
                Button = button,
                Background = background,
                Label = label
            };
        }

        /// <summary>A toggle-style button with a thin colour bar down the left edge, for seats.</summary>
        private Chip CreateAccentedChip(string name, Transform parent)
        {
            RectTransform root = CreateRect(name, parent);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = ChipOffColor;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            AddHorizontal(root.gameObject, 8f, new RectOffset(10, 10, 6, 6));

            Image accent = CreateRect("Accent", root).gameObject.AddComponent<Image>();
            accent.color = Color.white;
            AddLayoutElement(accent.gameObject, preferredWidth: 5f, flexibleHeight: 1f);

            Text label = CreateLabel(
                "Label", root, string.Empty, 12, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            AddLayoutElement(label.gameObject, flexibleWidth: 1f);

            return new Chip
            {
                Root = root.gameObject,
                Button = button,
                Background = background,
                Label = label,
                Accent = accent
            };
        }

        private static VerticalLayoutGroup AddVertical(
            GameObject go, float spacing, RectOffset padding = null)
        {
            VerticalLayoutGroup group = go.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset();
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        private static HorizontalLayoutGroup AddHorizontal(
            GameObject go, float spacing, RectOffset padding = null)
        {
            HorizontalLayoutGroup group = go.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset();
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            return group;
        }

        private static LayoutElement AddLayoutElement(
            GameObject go,
            float? preferredWidth = null,
            float? preferredHeight = null,
            float flexibleHeight = 0f,
            float flexibleWidth = 0f)
        {
            LayoutElement element = go.AddComponent<LayoutElement>();
            if (preferredWidth.HasValue)
            {
                element.preferredWidth = preferredWidth.Value;
            }

            if (preferredHeight.HasValue)
            {
                element.preferredHeight = preferredHeight.Value;
            }

            element.flexibleHeight = flexibleHeight;
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        private sealed class Chip
        {
            public GameObject Root;
            public Button Button;
            public Image Background;
            public Text Label;
            public Text Hint;
            public Image Accent;
        }
    }
}
