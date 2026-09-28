using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Presentation;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ClustrGalaxy.UI
{
    /// <summary>Retained, change-driven UI. Gameplay state belongs to the core session.</summary>
    public sealed partial class ProbeHud : MonoBehaviour
    {
        private ObserverProbeController flight;
        private ProbeTargeting targeting;
        private SpatialSceneLoader dataLoader;
        private GalaxyNavigationController navigation;
        private GalaxyModeState modeState;
        private GalaxySurveyVisibilityController visibility;
        private SignalScanner scanner;
        private CommentThreadController threadInspection;
        private readonly ProbeHudPresenter presenter = new();
        private readonly List<GalaxyEntityId> draftMembers = new();
        private string draftName = "", draftReason = "", savedPersonalId = "", filter = "", errorText = "";
        private SurveyTransitionOutcome latestOutcome;
        private GameObject canvasObject, menu;
        private RectTransform content;
        private Text status;
        private readonly Text[] speedDigits = new Text[4];
        private readonly Text[] chargeDigits = new Text[3];
        private static readonly string[] Digits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        private Font font;
        private InputField nameField, reasonField, searchField;
        private readonly List<Button> buttons = new();
        private int usedButtons;
        private int jumpPage;
        private bool dirty = true;
        private ScrollRect actionScroll;
        private GameObject observedFocus;
        private int observedSpeed = -1, observedCharge = -1;
        private string observedTravel;
        private GalaxyEntity observedTarget;
        private object observedSurvey, observedThread;
        private string observedStatus, observedHover;
        private bool modalCaptured, menuBeforeModal;
        private bool bodyWasKinematic, flightWasEnabled, navigationWasEnabled;
        private float simulationRateBeforeModal;
        private GameObject pauseIndicator;
        private Vector3 bodyVelocity, bodyAngularVelocity;
        public bool InputCaptured => modalCaptured || (menu != null && menu.activeSelf);
        private readonly CockpitInputContext fallbackInput = new();
        private CockpitInputContext InputContext => GetComponent<GalaxySessionBehaviour>()?.Session?.CockpitInput ?? fallbackInput;
        public long AcquireStation()
        {
            var id = InputContext.AcquireModal(); ApplyModalCapture(InputContext.Paused); return id;
        }
        public void ReleaseStation(long id)
        {
            InputContext.ReleaseModal(id); ApplyModalCapture(InputContext.Paused);
        }
        public void SetModalInputCaptured(bool captured)
        {
            InputContext.SetCompatibilityModal(captured); ApplyModalCapture(InputContext.Paused);
        }
        private void ChartContextChanged(bool open)
        { InputContext.SetChart(open); ApplyInputContext(); }
        private void ApplyInputContext()
        {
            var context = InputContext;
            if (flight != null) flight.SetPilotInputEnabled(context.PilotEnabled);
            if (navigation != null) navigation.SetCockpitInputEnabled(context.NavigationEnabled, context.ChartOpen);
            if (targeting != null) targeting.enabled = context.PilotEnabled;
            if (scanner != null) scanner.enabled = context.PilotEnabled;
            var pointer = !context.PilotEnabled;
            Cursor.lockState = pointer ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = pointer;
        }
        private void ApplyModalCapture(bool captured)
        {
            if (modalCaptured == captured) { ApplyInputContext(); return; }
            var body = GetComponent<Rigidbody>();
            if (captured)
            {
                menuBeforeModal = menu.activeSelf; SetMenuOpen(false); modalCaptured = true;
                simulationRateBeforeModal = Time.timeScale; Time.timeScale = 0;
                if (pauseIndicator != null) pauseIndicator.SetActive(true);
                if (flight != null) { flightWasEnabled = flight.enabled; flight.enabled = false; }
                if (navigation != null) { navigationWasEnabled = navigation.enabled; navigation.enabled = false; }
                if (targeting != null) targeting.enabled = false; if (scanner != null) scanner.enabled = false;
                if (body != null) { bodyWasKinematic = body.isKinematic; bodyVelocity = body.linearVelocity; bodyAngularVelocity = body.angularVelocity; body.isKinematic = true; }
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else
            {
                modalCaptured = false;
                Time.timeScale = simulationRateBeforeModal;
                if (pauseIndicator != null) pauseIndicator.SetActive(false);
                if (flight != null) flight.enabled = flightWasEnabled;
                if (navigation != null) navigation.enabled = navigationWasEnabled;
                if (body != null) { body.isKinematic = bodyWasKinematic; if (!bodyWasKinematic) { body.linearVelocity = bodyVelocity; body.angularVelocity = bodyAngularVelocity; } }
                SetMenuOpen(menuBeforeModal);
            }
            ApplyInputContext();
        }

        public void Initialize(ObserverProbeController flightController, ProbeTargeting targetController,
            SpatialSceneLoader loader, GalaxyNavigationController navigationController,
            GalaxyModeState modes, GalaxySurveyVisibilityController visibilityController,
            SignalScanner signalScanner = null, CommentThreadController inspection = null)
        {
            flight = flightController; targeting = targetController; dataLoader = loader;
            navigation = navigationController; modeState = modes; scanner = signalScanner; threadInspection = inspection;
            modeState.Changed += Invalidate;
            navigation.ModeChanged += ChartContextChanged;
            BindSurveyOutcomes(visibilityController);
            BuildUi(); InputContext.SetChart(navigation.IsMapOpen); ApplyInputContext();
        }
        public void BindSurveyOutcomes(GalaxySurveyVisibilityController value)
        {
            if (visibility != null) visibility.SurveyTransitionCommitted -= OnSurveyOutcome;
            visibility = value;
            if (visibility != null) visibility.SurveyTransitionCommitted += OnSurveyOutcome;
            dirty = true;
        }
        private void Invalidate() => dirty = true;
        public void InvalidateExploration() => dirty = true;
        private void Update()
        {
            if (menu == null) return;
            UpdateFlightInstruments();
            if (modalCaptured) return;
            var editingText = InputCaptured && (nameField.isFocused || reasonField.isFocused || searchField.isFocused);
            if (!editingText && (Keyboard.current?.tabKey.wasPressedThisFrame == true || Gamepad.current?.startButton.wasPressedThisFrame == true))
                SetMenuOpen(!InputCaptured);
            if (InputCaptured && !editingText && (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true)) SetMenuOpen(false);
            var state = visibility?.SurveySession?.State;
            var thread = threadInspection?.DisplayedModel;
            if (observedTarget != targeting?.Target || !ReferenceEquals(observedSurvey, state) ||
                !ReferenceEquals(observedThread, thread) || observedStatus != dataLoader?.StatusText || observedHover != navigation?.MapHoverStatus)
            {
                observedTarget = targeting?.Target; observedSurvey = state; observedThread = thread;
                observedStatus = dataLoader?.StatusText; observedHover = navigation?.MapHoverStatus; dirty = true;
            }
            var speed = Mathf.RoundToInt((flight?.Speed ?? 0) * 10);
            var charge = Mathf.RoundToInt((scanner?.Charge01 ?? 0) * 100);
            if (speed != observedSpeed) { observedSpeed = speed; UpdateDigits(speedDigits, speed); }
            if (charge != observedCharge) { observedCharge = charge; UpdateDigits(chargeDigits, charge); }
            if (observedTravel != navigation?.TravelStatus) { observedTravel = navigation?.TravelStatus; dirty = true; }
            if (dirty) Refresh();
        }
        public void SetMenuOpen(bool open)
        {
            if (menu == null || (!open && flight?.Simulation.Recovering == true)) return;
            if (menu.activeSelf != open) GetComponent<ShipAudioFeedback>()?.PlayInterface();
            menu.SetActive(open);
            InputContext.SetCommand(open); ApplyInputContext();
            dirty = true;
            if (open) Refresh();
            status.SetAllDirty();
            if (open && buttons.Count > 0) EventSystem.current?.SetSelectedGameObject(buttons[0].gameObject);
        }
        private void PlayInterfaceSound() => GetComponent<ShipAudioFeedback>()?.PlayInterface();
        public void ApplyPrompts(ClustrGalaxy.World.GalaxyVisualCatalog catalog)
        {
            if (menu == null || catalog == null || menu.transform.Find("Controller prompts") != null) return;
            var parent = new GameObject("Controller prompts", typeof(RectTransform));
            parent.transform.SetParent(menu.transform, false);
            var root = parent.GetComponent<RectTransform>(); root.anchorMin = root.anchorMax = new Vector2(0, 0);
            root.pivot = Vector2.zero; root.anchoredPosition = new Vector2(28, 4); root.sizeDelta = new Vector2(750, 25);
            var sprites = new[] { catalog.confirmPrompt, catalog.backPrompt };
            for (var i = 0; i < sprites.Length; i++)
            {
                var image = new GameObject("Xbox reference glyph", typeof(RectTransform), typeof(Image));
                image.transform.SetParent(root, false); var graphic = image.GetComponent<Image>();
                graphic.sprite = sprites[i]; graphic.preserveAspect = true; graphic.raycastTarget = false;
                var rect = image.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0,.5f);
                rect.anchoredPosition = new Vector2(12+i*280,0); rect.sizeDelta = new Vector2(24,24);
                graphic.enabled = sprites[i] != null;
                var label = Label(root, "Control equivalent"); label.fontSize = 12;
                label.text = i == 0 ? "A / South / Enter · Confirm" : "B / East / Escape · Back";
                var lr = label.rectTransform;lr.anchorMin=lr.anchorMax=new Vector2(0,.5f);lr.pivot=new Vector2(0,.5f);
                lr.anchoredPosition=new Vector2(30+i*280,0);lr.sizeDelta=new Vector2(248,24);
            }
        }
        private void BuildUi()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?/%—•[]()-_", 16);
            canvasObject = new GameObject("Galaxy HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = canvasObject.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720); scale.matchWidthOrHeight = 1;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Galaxy UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(canvasObject.transform, false);
            }
            status = Label(canvasObject.transform, "Status");
            var rect = status.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(16, -66); rect.sizeDelta = new Vector2(560, 630);
            BuildFlightDisplay();
            pauseIndicator = new GameObject("Paused local simulation", typeof(RectTransform), typeof(Canvas));
            pauseIndicator.transform.SetParent(canvasObject.transform, false);
            var pauseCanvas = pauseIndicator.GetComponent<Canvas>(); pauseCanvas.overrideSorting = true; pauseCanvas.sortingOrder = 200;
            var pauseLabel = Label(pauseIndicator.transform, "Pause context");
            pauseLabel.text = "LOCAL SIMULATION PAUSED · CLOSE THIS VIEW TO RESUME";
            pauseLabel.fontSize = 12; pauseLabel.alignment = TextAnchor.MiddleCenter; pauseLabel.raycastTarget = false;
            var pauseRect = pauseLabel.rectTransform; pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(.5f, 1);
            pauseRect.pivot = new Vector2(.5f, 1); pauseRect.sizeDelta = new Vector2(560, 26); pauseRect.anchoredPosition = new Vector2(0, -8);
            var pauseRoot = pauseIndicator.GetComponent<RectTransform>(); pauseRoot.anchorMin = Vector2.zero; pauseRoot.anchorMax = Vector2.one; pauseRoot.offsetMin = pauseRoot.offsetMax = Vector2.zero;
            pauseIndicator.SetActive(false);
            menu = new GameObject("Actions", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var projection = new GameObject("Cockpit command projection", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var cockpitCamera = GetComponentInChildren<Camera>();
            projection.transform.SetParent(cockpitCamera.transform, false);
            projection.transform.localPosition = new Vector3(0, -.00375f, .55f);
            projection.transform.localRotation = Quaternion.Euler(0, -4f, 0);
            projection.transform.localScale = Vector3.one * .001f;
            projection.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
            var projectionCanvas = projection.GetComponent<Canvas>();
            projectionCanvas.renderMode = RenderMode.WorldSpace; projectionCanvas.worldCamera = cockpitCamera;
            menu.transform.SetParent(projection.transform, false);
            var panel = menu.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(.025f, .035f); panel.anchorMax = new Vector2(.975f, .965f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            menu.GetComponent<Image>().color = new Color(.012f, .028f, .042f, .78f);
            status.transform.SetParent(menu.transform,false);status.fontSize=13;
            rect.anchorMin=new Vector2(.745f,.09f);rect.anchorMax=new Vector2(.975f,.82f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var title=Label(menu.transform,"Command console title");title.text="S U R V E Y O R   /   COMMAND CONSOLE";title.fontSize=24;
            PlaceMeter(title,28,22,1000);title.rectTransform.sizeDelta=new Vector2(1000,38);
            var subtitle=Label(menu.transform,"Console guidance");subtitle.text="Choose a station. B / Escape returns you to flight.";subtitle.color=new Color(.55f,.68f,.72f);PlaceMeter(subtitle,28,62,950);
            var rail=new GameObject("Console stations",typeof(RectTransform),typeof(VerticalLayoutGroup));rail.transform.SetParent(menu.transform,false);sectionRail=rail.transform;
            var railRect=rail.GetComponent<RectTransform>();railRect.anchorMin=new Vector2(.02f,.06f);railRect.anchorMax=new Vector2(.22f,.82f);railRect.offsetMin=railRect.offsetMax=Vector2.zero;
            var railLayout=rail.GetComponent<VerticalLayoutGroup>();railLayout.spacing=8;railLayout.childControlHeight=true;railLayout.childForceExpandHeight=false;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel, false); var viewportRect=viewport.GetComponent<RectTransform>();viewportRect.anchorMin=new Vector2(.245f,.07f);viewportRect.anchorMax=new Vector2(.72f,.82f);viewportRect.offsetMin=viewportRect.offsetMax=Vector2.zero;
            var rows = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rows.transform.SetParent(viewport.transform, false); content = rows.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = rows.GetComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.padding = new RectOffset(12,12,12,12);
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            rows.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = actionScroll = menu.GetComponent<ScrollRect>(); scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            searchField = Field("Search loaded entities", 100, value => { filter = value; selectedSection=ProbeHudSection.Selection;dirty = true; });
            nameField = Field("Constellation name", 60, value => { draftName = value; dirty = true; });
            reasonField = Field("Reason / note", 500, value => { draftReason = value; dirty = true; });
            menu.SetActive(false);
        }
        private InputField Field(string placeholder, int limit, UnityEngine.Events.UnityAction<string> changed)
        {
            var go = new GameObject(placeholder, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(content, false); go.GetComponent<LayoutElement>().preferredHeight = 46;
            go.GetComponent<Image>().color = new Color(.12f,.16f,.2f);
            var field = go.GetComponent<InputField>(); var label = Label(go.transform, "Text"); Stretch(label.rectTransform);
            field.textComponent = label; field.characterLimit = limit;
            var hint = Label(go.transform, "Placeholder"); Stretch(hint.rectTransform); hint.text = placeholder; hint.color = Color.gray;
            field.placeholder = hint; field.onValueChanged.AddListener(changed); return field;
        }
        private Text Label(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = 16; text.color = new Color(.8f,.94f,.95f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; return text;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(8,2); rect.offsetMax = new Vector2(-8,-2); }
        private bool quitRequested, focusQuitCancel;
        private Button quitCancelButton;
        private ProbeHudSection selectedSection=ProbeHudSection.Selection,currentSection;
        private readonly List<ProbeHudSection> buttonSections=new();
        private void AddButton(string label, UnityEngine.Events.UnityAction action)
        {
            Button button;
            if (usedButtons == buttons.Count)
            {
                var go = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(content, false); go.GetComponent<LayoutElement>().preferredHeight = 46;
                go.GetComponent<Image>().color = new Color(.075f,.12f,.16f);
                button = go.GetComponent<Button>(); var text = Label(go.transform,"Label"); Stretch(text.rectTransform);
                buttons.Add(button);buttonSections.Add(currentSection);
            }
            buttonSections[usedButtons]=currentSection;button = buttons[usedButtons++]; button.gameObject.SetActive(true);
            button.GetComponent<LayoutElement>().preferredHeight = 46;
            var station=label.StartsWith("SECTION:")||label=="RESUME FLIGHT"||label=="QUIT GAME";
            button.transform.SetParent(station?sectionRail:content,false);
            ConsoleTheme.Style(button);
            button.gameObject.name=label;
            var buttonText=button.GetComponentInChildren<Text>();buttonText.text=label.StartsWith("SECTION: ")?label.Substring(9):label;buttonText.fontSize=14;buttonText.alignment=TextAnchor.MiddleLeft; button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action); button.onClick.AddListener(Invalidate); button.onClick.AddListener(PlayInterfaceSound);
        }
        private void Refresh()
        {
            dirty = false;
            var model = ReadModel(); var presentation = presenter.Present(model);
            RefreshFlightDisplay(model);
            status.text = "FLIGHT BRIEF\n\n" + string.Join("\n", presentation.Lines) +
                "\n" + errorText + "\nTab / Start: actions • R / X: hold scan\nM / Select: map • F / A: target\nLS: yaw / vertical thrust • RS: pitch / roll\nTriggers: forward / reverse • Bumpers: strafe";
            if (threadInspection?.DisplayedModel != null) status.text += "\nThread replies: open Actions to read.";
            if (!InputCaptured) return;
            usedButtons = 0;currentSection=ProbeHudSection.Common;
            AddButton("RESUME FLIGHT", () => { if (GetComponent<ShipSurfaceSafety>()?.Resume() != false) SetMenuOpen(false); });
            if(quitRequested)
            {
                AddButton("CONFIRM QUIT — UNSAVED DRAFTS WILL BE DISCARDED",()=>Application.Quit());
                AddButton("CANCEL QUIT",()=>quitRequested=false);
                quitCancelButton=buttons[usedButtons-1];
            }
            else AddButton("QUIT GAME",()=>{quitRequested=true;focusQuitCancel=true;});
            if (dataLoader?.DataMode == GalaxyDataMode.Failed && !dataLoader.OfflineOnly)
                AddButton("OPEN AUTHORED LOCAL DEMO", ClustrGalaxy.Bootstrap.GalaxyPrototypeBootstrap.StartDemo);
            foreach(var section in ProbeHudSections.Selectable)
            {var chosen=section;AddButton("SECTION: "+ProbeHudSections.Title(section),()=>{selectedSection=chosen;content.anchoredPosition=Vector2.zero;});}
            foreach (var action in presentation.Actions)
            {
                currentSection=ProbeHudSections.For(action);
                if (action == ProbeHudAction.ChooseStartingSystem)
                {
                    foreach (var system in visibility.LoadedStartingSystems())
                        AddButton("START AT " + system.DisplayName, () => visibility.StartAt(system.StableId));
                }
                else AddButton(ActionLabel(action), () => Dispatch(action));
            }
            currentSection=ProbeHudSection.Selection;
            if (navigation.OrbitActive) AddButton("RELEASE ORBIT · COAST", navigation.ReleaseOrbit);
            else if (targeting.Target != null && navigation.CanApproach(targeting.Target) && targeting.Target.GetComponent<ClustrGalaxy.Simulation.GravitySource>() != null)
                AddButton("AUTO ORBIT SELECTED BODY", () => navigation.StartOrbit(targeting.Target));
            var cruise = GetComponent<GalaxySystemCruiseController>();
            if (cruise != null)
            {
                if (cruise.Active) AddButton("CANCEL CRUISE · SAFE DROP", cruise.Cancel);
                else if (targeting.Target != null && navigation.CanApproach(targeting.Target))
                {
                    AddButton("SYSTEM CRUISE · AUTO APPROACH", () => cruise.Begin(targeting.Target, true));
                    AddButton("SYSTEM CRUISE · MANUAL", () => cruise.Begin(targeting.Target, false));
                }
            }
            var jump = GetComponent<GalaxyJumpController>();
            if (jump?.Active == true) AddButton("CANCEL JUMP · KEEP CURRENT SYSTEM", jump.Cancel);
            else if (jump != null)
            {
                var jumpSession = GetComponent<GalaxySessionBehaviour>().Session;
                if (jumpSession != null)
                {
                    var destinations = jumpSession.JumpDestinations().Where(destination => string.IsNullOrWhiteSpace(filter) ||
                        jumpSession.JumpDestinationLabel(destination).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    jumpPage = Mathf.Clamp(jumpPage, 0, Math.Max(0, (destinations.Length - 1) / 30));
                    for (var i = jumpPage * 30; i < Math.Min(destinations.Length, (jumpPage + 1) * 30); i++)
                    {
                        var destinationId = destinations[i].Id;
                        AddButton("JUMP · " + (i + 1) + " · " + jumpSession.JumpDestinationLabel(destinations[i]), () => jump.Begin(destinationId));
                    }
                    if (jumpPage > 0) AddButton("PREVIOUS CONNECTIONS", () => { jumpPage--; dirty = true; });
                    if ((jumpPage + 1) * 30 < destinations.Length) AddButton("MORE CONNECTIONS", () => { jumpPage++; dirty = true; });
                }
            }
            currentSection=ProbeHudSection.Creations;
            var state = visibility?.SurveySession?.State;
            nameField.gameObject.SetActive(state != null&&ProbeHudSections.Visible(ProbeHudSection.Creations,selectedSection)); reasonField.gameObject.SetActive(state != null&&ProbeHudSections.Visible(ProbeHudSection.Creations,selectedSection));
            foreach (var member in draftMembers.ToArray()) AddButton("REMOVE " + member.Value, () => draftMembers.Remove(member));
            if (state != null) foreach (var item in state.PersonalConstellations)
                AddButton("SAVED: " + item.Name + (state.QueuedPersonalConstellationIds.Contains(item.Id) ? " — QUEUED LOCALLY" : ""), () =>
                {
                    savedPersonalId = item.Id; draftMembers.Clear(); draftMembers.AddRange(item.Members);
                    draftName = item.Name; draftReason = item.Reason;
                    nameField.SetTextWithoutNotify(draftName); reasonField.SetTextWithoutNotify(draftReason);
                });
            currentSection=ProbeHudSection.Selection;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var count = 0;
                foreach (var entity in visibility.Registry.Entities)
                {
                    if (count >= 30) break;
                    if (!visibility.CanLabel(entity) || entity.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    count++; AddButton(entity.DisplayName, () => { targeting.SelectTarget(entity); SetMenuOpen(false); });
                }
            }
            foreach (var row in presentation.ThreadRows)
            {
                var prefix = new string(' ', Math.Min(row.Depth, 3) * 2);
                AddButton(prefix + row.Author + ": " + row.Excerpt, () => { });
                buttons[usedButtons - 1].GetComponent<LayoutElement>().preferredHeight = 100;
            }
            var activities = GetComponent<GalaxySessionBehaviour>()?.Session?.Activities;
            var feedback = GetComponent<ExplorationFeedback>();
            if (activities != null)
            {
                currentSection=ProbeHudSection.Creations;
                if (modeState.WorldMode == GalaxyWorldMode.Survey && state != null && savedPersonalId.Length != 0)
                {
                    var selectedPersonal = state.PersonalConstellations.FirstOrDefault(item => item.Id == savedPersonalId);
                    if (selectedPersonal != null) AddButton("EDIT SAVED CONSTELLATION DESIGN", () =>
                        this.GetOrAddComponent<ConstellationStudio>().Open(selectedPersonal));
                }
                var activeSession=GetComponent<GalaxySessionBehaviour>().Session;
                if(activeSession.SelectedPracticeGhost!=null)AddButton("CLEAR IMPORTED PRACTICE GHOST — "+activeSession.SelectedPracticeGhost.Replay.Identity.CourseId,activeSession.ClearPracticeGhost);
                AddButton("CAPTURE SCREENSHOT",()=> this.GetOrAddComponent<LocalImageViewer>().Capture());
                AddButton("OPEN JOURNEY DIARY",()=> this.GetOrAddComponent<JourneyDiary>().Open());
                AddButton("OPEN LOCAL CREATIONS",()=> this.GetOrAddComponent<LocalCreationLibrary>().Open());
                AddButton("OPEN PROBE OBSERVATORY",()=> this.GetOrAddComponent<ProbeObservatory>().Open());
                currentSection=ProbeHudSection.Discoveries;
                GetComponent<CampaignController>()?.AddActions(AddButton);
                currentSection=ProbeHudSection.Activities;
                GetComponent<ExpeditionController>()?.AddActions(AddButton);
                currentSection=ProbeHudSection.Preferences;
                var prefs = activities.State.Preferences;
                AddButton(prefs.SaveBestGhosts?"SAVE PERSONAL-BEST GHOSTS: ON":"SAVE PERSONAL-BEST GHOSTS: OFF",()=>
                    ApplyPreferences(activities,prefs.With(saveBestGhosts:!prefs.SaveBestGhosts)));
                AddButton(prefs.AutomaticTuning ? "AUTOMATIC TUNING: ON" : "AUTOMATIC TUNING: OFF", () =>
                    ApplyPreferences(activities, prefs.With(automaticTuning: !prefs.AutomaticTuning)));
                AddButton(prefs.MusicMuted ? "EXPLORATION AUDIO: MUTED" : "EXPLORATION AUDIO: ON", () =>
                    ApplyPreferences(activities, prefs.With(musicMuted: !prefs.MusicMuted)));
                AddButton("MUSIC TEMPO: " + prefs.Tempo + " BPM", () => ApplyPreferences(activities, prefs.With(tempo: prefs.Tempo >= 120 ? 60 : prefs.Tempo + 20)));
                AddButton("AUDIO VOLUME: " + Mathf.RoundToInt(prefs.MusicVolume * 100) + "%", () =>
                    ApplyPreferences(activities, prefs.With(musicVolume: prefs.MusicVolume >= .75f ? 0 : Mathf.Min(1, prefs.MusicVolume + .25f))));
                var instrumentNames = new[] { "SOFT BELL", "WARM PLUCK", "AIRY GLASS" };
                AddButton("INSTRUMENT: " + instrumentNames[prefs.MusicPreset], () => ApplyPreferences(activities, prefs.With(musicPreset: (prefs.MusicPreset + 1) % 3)));
                AddButton(prefs.ReducedAudio ? "REDUCED AUDIO: ON" : "REDUCED AUDIO: OFF", () => ApplyPreferences(activities, prefs.With(reducedAudio: !prefs.ReducedAudio)));
                AddButton("CELEBRATION INTENSITY: " + Mathf.RoundToInt(prefs.CelebrationIntensity*100) + "%", () =>
                    ApplyPreferences(activities,prefs.With(celebrationIntensity: prefs.CelebrationIntensity >= .9f ? 0 : Mathf.Min(1,prefs.CelebrationIntensity+.2f))));
                AddButton(prefs.ReducedMotion ? "REDUCED MOTION: ON" : "REDUCED MOTION: OFF", () => ApplyPreferences(activities, prefs.With(reducedMotion: !prefs.ReducedMotion)));
                if (feedback != null)
                {
                    AddButton(feedback.MusicPlaying ? "PAUSE DISCOVERY MUSIC" : "PLAY DISCOVERY MUSIC — ARTISTIC SONIFICATION", feedback.ToggleMusic);
                    if (draftMembers.Count >= 3) AddButton("PLAY DRAFT CONSTELLATION", () => feedback.PlayConstellation(draftMembers));
                    foreach (var member in draftMembers) { var auditionId = member; AddButton("AUDITION " + member.Value, () => feedback.Audition(auditionId)); }
                }
                currentSection=ProbeHudSection.Activities;
                var flightActivities=GetComponent<FlightActivitiesController>();
                if(flightActivities!=null)
                {
                    AddButton("FLIGHT: "+flightActivities.Status,()=>{});
                    foreach(var kind in new[]{FlightActivityKind.RingRun,FlightActivityKind.FigureEight,FlightActivityKind.StableOrbit})
                    {var selectedKind=kind;AddButton("START "+kind,()=>flightActivities.StartPlayground(selectedKind,false));AddButton("PREVIEW "+kind,()=>flightActivities.StartPlayground(selectedKind,true));}
                    AddButton(flightActivities.GhostVisible?"HIDE PERSONAL-BEST GHOST":"SHOW PERSONAL-BEST GHOST",flightActivities.ToggleGhost);
                    AddButton("END FLIGHT ACTIVITY",flightActivities.Cancel);
                    AddButton("SAVE COMPLETED FLIGHT RECORDING",flightActivities.SaveGhost);
                    AddButton("VIEW LATEST FLIGHT RECORDING",flightActivities.ViewGhost);
                    if(GetComponent<GalaxySessionBehaviour>().Session.FlightError.Length!=0)AddButton("RETRY FLIGHT RECORD SAVE",flightActivities.RetrySave);
                    var coreSession=GetComponent<GalaxySessionBehaviour>().Session;
                    var scenicChoices=0;
                    foreach(var route in coreSession.Resident.LoadedRoutes)
                    {
                        if(targeting.Target==null || (route.SourceId.Value!=targeting.Target.StableId && route.TargetId.Value!=targeting.Target.StableId))continue;
                        if(modeState.WorldMode==GalaxyWorldMode.Survey&&(state==null||!state.KnownRoutes.Contains(route.Id)))continue;
                        var id=route.Id;
                        var destination=route.SourceId.Value==targeting.Target.StableId?route.TargetId:route.SourceId;
                        if(!coreSession.Resident.TryFindActive(destination,out var resident))continue;
                        var label=modeState.WorldMode==GalaxyWorldMode.Survey&&!state.IsIdentified(destination)?"Unidentified destination":resident.DisplayName;
                        scenicChoices++;
                        for(var profile=0;profile<3;profile++){var choice=profile;AddButton("SCENIC "+new[]{"BROAD CURRENT","WINDING PASSAGE","EDGE RIBBON"}[profile]+" · "+scenicChoices+" · "+label,()=>flightActivities.StartScenic(id,choice));}
                        if(scenicChoices>=8)break;
                    }
                    if(scenicChoices==8)AddButton("Showing up to 8 loaded routes for this selection",()=>{});
                    foreach(var record in activities.State.Flights)AddButton("PERSONAL BEST · "+record.CourseId+" · "+record.Mode+" · "+Math.Round(record.Accuracy*100)+"% ACCURACY · "+record.Seconds.ToString("F1")+"s",()=>{});
                }
                currentSection=ProbeHudSection.Creations;
                var recorder = GetComponent<JourneyRecorder>();
                if (recorder != null) AddButton("SAVE ROLLING REPLAY — " + recorder.Status, recorder.Save);
                foreach (var entry in activities.State.Diary.Reverse().Take(10)) AddButton("JOURNAL: " + entry.Text, () => { });
            }
            for(var i=0;i<usedButtons;i++)buttons[i].gameObject.SetActive(ProbeHudSections.Visible(buttonSections[i],selectedSection));
            for (var i = usedButtons; i < buttons.Count; i++) buttons[i].gameObject.SetActive(false);
            ConfigureConsoleNavigation();
            var focused=EventSystem.current?.currentSelectedGameObject;if(focused!=null&&!focused.activeInHierarchy)EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
            // The confirm button reuses the pooled QUIT slot; move focus so a repeated press cannot confirm.
            if(focusQuitCancel&&quitRequested&&quitCancelButton!=null&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(quitCancelButton.gameObject);
            focusQuitCancel=false;
        }

        private readonly List<Selectable> visibleNavigation=new();
        private void ConfigureConsoleNavigation()
        {
            visibleNavigation.Clear();Button firstAction=null,station=null;var firstActionIndex=-1;
            for(var i=0;i<usedButtons;i++)if(buttons[i].gameObject.activeSelf){if(buttons[i].transform.parent==content&&firstAction==null){firstAction=buttons[i];firstActionIndex=visibleNavigation.Count;}visibleNavigation.Add(buttons[i]);if(buttons[i].gameObject.name=="SECTION: "+ProbeHudSections.Title(selectedSection))station=buttons[i];}
            // Text fields sit above the actions in the console; keep them reachable by controller.
            var fieldIndex=firstActionIndex<0?visibleNavigation.Count:firstActionIndex;
            foreach(var field in new Selectable[]{searchField,nameField,reasonField})
                if(field!=null&&field.gameObject.activeSelf)visibleNavigation.Insert(fieldIndex++,field);
            for(var i=0;i<visibleNavigation.Count;i++)
            {
                var b=visibleNavigation[i];var nav=b.navigation;nav.mode=Navigation.Mode.Explicit;
                nav.selectOnUp=visibleNavigation[(i+visibleNavigation.Count-1)%visibleNavigation.Count];nav.selectOnDown=visibleNavigation[(i+1)%visibleNavigation.Count];
                nav.selectOnLeft=station??visibleNavigation[0];nav.selectOnRight=firstAction??nav.selectOnDown;b.navigation=nav;
            }
        }

        private void ApplyPreferences(ActivitySession activities, ActivityPreferences preferences)
        {
            var result = activities.Apply(new ActivityIntent(Guid.NewGuid().ToString("N"), activities.State.Identity,
                ActivityOperation.SetPreferences, DateTime.UtcNow, preferences: preferences));
            errorText = result.Committed ? "" : result.Error;
            dirty = true;
        }
        private void KeepFocusedRowVisible()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == observedFocus || selected == null || !selected.transform.IsChildOf(content)) return;
            observedFocus = selected;
            var row = (RectTransform)selected.transform;
            var top = -row.anchoredPosition.y;
            var height = actionScroll.viewport.rect.height;
            var offset = content.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (top + row.rect.height > offset + height) offset = top + row.rect.height - height;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x,
                Mathf.Clamp(offset, 0, Mathf.Max(0, content.rect.height - height)));
        }

        private void LateUpdate()
        {
            if (InputCaptured) KeepFocusedRowVisible();
        }

        private void BuildMeter(string title, Text[] digits, float x, float y, bool decimalPoint)
        {
            var label = Label(flightDisplay.transform, title);
            PlaceMeter(label, x, y-22, 220);label.fontSize=11;label.color=new Color(.57f,.72f,.76f);
            label.text = title;
            for (var i = 0; i < digits.Length; i++)
            {
                digits[i] = Label(flightDisplay.transform, title + i);
                PlaceMeter(digits[i], x + i * 20 + (decimalPoint && i == digits.Length - 1 ? 10 : 0), y, 23);
                digits[i].fontSize=25;digits[i].rectTransform.sizeDelta=new Vector2(24,35);digits[i].text = "0";
            }
            if (decimalPoint)
            {
                var point = Label(flightDisplay.transform, "Decimal");
                PlaceMeter(point, x + (digits.Length - 1) * 20, y, 10);
                point.text = ".";
            }
        }
        private static void PlaceMeter(Text text, float x, float y, float width)
        {
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,24);
        }
        private static void UpdateDigits(Text[] digits, int value)
        {
            value = Math.Max(0,value);
            for (var index = digits.Length - 1; index >= 0; index--)
            {
                var next = Digits[value % 10]; value /= 10;
                if (digits[index].text != next) digits[index].text = next;
            }
        }

        private ProbeHudReadModel ReadModel()
        {
            var target = targeting?.Target;
            var state = visibility?.SurveySession?.State;
            var threadRows = threadInspection?.DisplayedModel?.Presentation().Rows.Select(row =>
                new ProbeHudThreadRow(row.Entry.Depth, row.Entry.Author, row.Entry.Score, row.Entry.ReplyCount,
                    row.Excerpt, row.Entry.HasMore)).ToArray() ?? Array.Empty<ProbeHudThreadRow>();
            return new ProbeHudReadModel
            {
                Speed = flight?.Speed ?? 0f,
                FlightMode = modeState?.FlightMode.ToString().ToUpperInvariant() ?? "ASSIST",
                Survey = modeState?.WorldMode == GalaxyWorldMode.Survey,
                AuthoredContent = dataLoader?.DataMode == GalaxyDataMode.LocalDemo,
                RequiresSurveyStart = visibility?.RequiresStartingSystemChoice == true,
                DataStatus = dataLoader?.StatusText ?? "LOCAL DEMO",
                NavigationLevel = navigation?.Level.ToString().ToUpperInvariant() ?? "FLIGHT",
                NavigationStatus = navigation?.TravelStatus ?? string.Empty,
                TargetName = target?.DisplayName ?? string.Empty,
                TargetKind = target?.Kind.ToString().ToUpperInvariant() ?? string.Empty,
                TargetId = target?.StableId ?? string.Empty,
                TargetKnown = target != null && visibility?.IsKnown(target) == true,
                TargetIdentified = target != null && visibility?.CanLabel(target) == true,
                TargetNavigable = target != null && navigation?.CanTravelTo(target) == true,
                TargetApproachable = target != null && navigation?.CanApproach(target) == true,
                ScanAvailable = target != null && scanner != null && visibility?.IsKnown(target) == true &&
                    state?.IsScanComplete((GalaxyEntityId)target.StableId) != true,
                ScanCharge = scanner?.Charge01 ?? 0f,
                ScannerLevel = state?.ScannerLevel ?? 1,
                Objective = state == null ? string.Empty : $"{state.CompletedContracts.Count}/4 CONTRACTS",
                Notification = latestOutcome?.Notifications.LastOrDefault() ?? string.Empty,
                QueueStatus = state == null ? string.Empty : !string.IsNullOrWhiteSpace(state.PendingQueueId)
                    ? "AWAITING LOCAL QUEUE CONFIRMATION — NOTHING SENT"
                    : state.QueuedPersonalConstellationIds.Contains(savedPersonalId) ? "QUEUED LOCALLY — NOTHING SENT" : string.Empty,
                DraftName = draftName, DraftReason = draftReason, DraftMembers = draftMembers,
                IdentifiedEntities = state?.IdentifiedEntities ?? Array.Empty<GalaxyEntityId>(),
                SavedSelectionName = state?.PersonalConstellations.FirstOrDefault(item => item.Id == savedPersonalId)?.Name ?? string.Empty,
                HasSavedPersonalConstellation = !string.IsNullOrWhiteSpace(savedPersonalId),
                AwaitingQueueConfirmation = !string.IsNullOrWhiteSpace(state?.PendingQueueId),
                ThreadRows = threadRows
            };
        }

        private void Dispatch(ProbeHudAction action)
        {
            var target = targeting?.Target;
            switch (action)
            {
                case ProbeHudAction.EnterSurvey: visibility?.EnterSurvey(); break;
                case ProbeHudAction.ReturnToExplore: visibility?.ReturnToExplore(); break;
                case ProbeHudAction.Scan: SetMenuOpen(false); break;
                case ProbeHudAction.FastTravel: navigation?.TryFastTravel(target); break;
                case ProbeHudAction.PlotAutopilot: navigation?.PlotAutopilot(target); break;
                case ProbeHudAction.TrackMastery: navigation?.TrackMasteryDestination(target); break;
                case ProbeHudAction.AddConstellationMember:
                    if (target != null && draftMembers.Count < 12 && !draftMembers.Contains((GalaxyEntityId)target.StableId))
                        draftMembers.Add((GalaxyEntityId)target.StableId);
                    break;
                case ProbeHudAction.SavePersonalConstellation: SaveDraft(); break;
                case ProbeHudAction.RequestLocalQueue: Apply(new RequestLocalQueueIntent(IntentId("queue-request"),
                    visibility.SurveySession.State.Snapshot, DateTime.UtcNow, savedPersonalId)); break;
                case ProbeHudAction.ConfirmLocalQueue: Apply(new ConfirmLocalQueueIntent(IntentId("queue-confirm"),
                    visibility.SurveySession.State.Snapshot, DateTime.UtcNow)); break;
                case ProbeHudAction.CancelLocalQueue: Apply(new CancelLocalQueueIntent(IntentId("queue-cancel"),
                    visibility.SurveySession.State.Snapshot, DateTime.UtcNow)); break;
            }
        }

        private void SaveDraft()
        {
            var session = visibility?.SurveySession;
            if (session == null) return;
            var outcome = Apply(new SavePersonalConstellationIntent(IntentId("personal-save"), session.State.Snapshot,
                DateTime.UtcNow, draftName, draftReason, draftMembers));
            if (outcome?.Succeeded == true) savedPersonalId = outcome.PersonalConstellationId;
        }

        private SurveyTransitionOutcome Apply(SurveyIntent intent)
        {
            var result = visibility?.ApplySurveyIntent(intent);
            if (result != null) errorText = result.Succeeded ? string.Empty : result.Rejection;
            dirty = true;
            return result;
        }
        private static string IntentId(string prefix) => prefix + ":" + Guid.NewGuid().ToString("N");
        private static string ActionLabel(ProbeHudAction action) => action switch
        {
            ProbeHudAction.EnterSurvey => "ENTER SURVEY",
            ProbeHudAction.ReturnToExplore => "RETURN TO EXPLORE",
            ProbeHudAction.Scan => "RESUME AND HOLD R / X TO SCAN",
            ProbeHudAction.FastTravel => "FAST TRAVEL",
            ProbeHudAction.PlotAutopilot => "PLOT AUTOPILOT",
            ProbeHudAction.TrackMastery => "TRACK MASTERY APPROACH",
            ProbeHudAction.AddConstellationMember => "ADD CONSTELLATION MEMBER",
            ProbeHudAction.SavePersonalConstellation => "SAVE LOCALLY",
            ProbeHudAction.RequestLocalQueue => "QUEUE FOR REVIEW",
            ProbeHudAction.ConfirmLocalQueue => "CONFIRM LOCAL QUEUE",
            ProbeHudAction.CancelLocalQueue => "CANCEL LOCAL QUEUE",
            _ => action.ToString()
        };

        private void OnSurveyOutcome(SurveyTransitionOutcome outcome)
        {
            latestOutcome = outcome; dirty = true;
            if (outcome.RevealedEntities.Count > 0 || outcome.RevealedRoutes.Count > 0)
                GetComponent<GalaxyPerformanceMonitor>()?.RecordScanEvent();
        }


        private void OnDestroy()
        {
            if (modalCaptured) { Time.timeScale = simulationRateBeforeModal; modalCaptured = false; }
            if (modeState != null) modeState.Changed -= Invalidate;
            if (navigation != null) navigation.ModeChanged -= ChartContextChanged;
            BindSurveyOutcomes(null);
            if (canvasObject != null) Destroy(canvasObject);
        }
    }
}
