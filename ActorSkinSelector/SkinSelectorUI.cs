//Author: Jellobeans
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ActorSkinSelector
{
    public class SkinSelectorUI : MonoBehaviour
    {
        private static SkinSelectorUI _instance;
        private Action _onContinue;
        public static bool IsOpen { get; internal set; }

        const float H_HEADER = 52f;
        const float H_CONFIG = 58f;
        const float H_FOOTER = 60f;
        const float H_TEAM   = 38f;
        const float CELL_W   = 140f;
        const float CELL_H   = 200f;
        const float PORT_H   = 135f;
        const float GAP      = 6f;
        const float PAD      = 8f;
        const int   COLS     = 3;
        const float SIDEBAR_RATIO = 0.52f;
        const float GRID_RATIO    = 0.48f;
        const float SEL_ENTRY_H   = 48f;

        static readonly Color C_BG       = new Color(0.05f, 0.05f, 0.08f, 1f);
        static readonly Color C_HEADER   = new Color(0.07f, 0.07f, 0.12f, 1f);
        static readonly Color C_CONFIG   = new Color(0.06f, 0.06f, 0.10f, 1f);
        static readonly Color C_FOOTER   = new Color(0.06f, 0.06f, 0.10f, 1f);
        static readonly Color C_EAGLE    = new Color(0.06f, 0.10f, 0.20f, 1f);
        static readonly Color C_RAVEN    = new Color(0.20f, 0.05f, 0.05f, 1f);
        static readonly Color C_EAG_ACC  = new Color(0.30f, 0.60f, 1.00f, 1f);
        static readonly Color C_RAV_ACC  = new Color(1.00f, 0.28f, 0.22f, 1f);
        static readonly Color C_CELL     = new Color(0.10f, 0.10f, 0.15f, 1f);
        static readonly Color C_CELL_SEL = new Color(0.12f, 0.16f, 0.28f, 1f);
        static readonly Color C_SEP      = new Color(0.15f, 0.15f, 0.22f, 1f);
        static readonly Color C_SIDEBAR  = new Color(0.05f, 0.05f, 0.09f, 1f);
        static readonly Color C_SEL_ENTRY= new Color(0.08f, 0.08f, 0.14f, 1f);
        static readonly Color C_CONT     = new Color(0.10f, 0.52f, 0.18f, 1f);
        static readonly Color C_CLEAR    = new Color(0.42f, 0.08f, 0.08f, 1f);
        static readonly Color C_SAVE     = new Color(0.12f, 0.30f, 0.50f, 1f);
        static readonly Color C_LOAD     = new Color(0.14f, 0.38f, 0.22f, 1f);
        static readonly Color C_DEL      = new Color(0.45f, 0.10f, 0.10f, 1f);
        static readonly Color C_DDR_BG   = new Color(0.08f, 0.10f, 0.18f, 1f);
        static readonly Color C_DDR_ITEM = new Color(0.10f, 0.14f, 0.24f, 1f);
        static readonly Color C_INPUT_BG = new Color(0.08f, 0.10f, 0.16f, 1f);
        static readonly Color C_MODAL_BG = new Color(0.04f, 0.04f, 0.07f, 0.92f);
        static readonly Color C_WHITE    = Color.white;
        static readonly Color C_DIM      = new Color(0.55f, 0.55f, 0.60f, 1f);
        static readonly Color C_CHECK    = new Color(0.20f, 0.60f, 0.30f, 1f);

        private GameObject _root;
        private InputField _nameField;
        private Text       _statusText;
        private Text       _dropdownLabel;
        private GameObject _dropdownListGO;
        private GameObject _modalGO;
        private GameObject _cancelModalGO;
        private GameObject _optionsModalGO;
        private string     _selectedConfig = "";
        private bool       _dropdownOpen;
        private string     _pendingDeleteName = "";

        private Image _optCheck_DontRandomize;
        private Image _optCheck_Preserve;
        private Image _optCheck_VehPlayer;
        private Image _optCheck_KeepVehicle;
        private Text  _optLbl_DontRandomize;
        private Text  _optLbl_Preserve;
        private Text  _optLbl_VehPlayer;
        private Text  _optLbl_KeepVehicle;

        private readonly List<CellState> _cells = new List<CellState>();
        private Transform[] _sidebarContent = new Transform[2];

        private struct CellState
        {
            public int team; public ActorSkin skin;
            public Image outline, bg;
            public GameObject go;
        }

        private GameObject _rarityDropGO;
        private int        _rarityDropTeam;
        private ActorSkin  _rarityDropSkin;

        private GameObject _voiceDropGO;
        private int        _voiceDropTeam;
        private ActorSkin  _voiceDropSkin;

        private GameObject _voiceRarityDropGO;
        private int        _voiceRarityMutatorId;

        private float      _voiceScrollPos = 1f;

        private readonly string[] _searchQuery = new string[2];


        public static void ShowOverlay(Action onContinue)
        {
            if (_instance == null)
            {
                var go = new GameObject("JellosMultiskinUI");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<SkinSelectorUI>();
            }
            _instance.Open(onContinue);
        }

        private void Open(Action onContinue)
        {
            _onContinue     = onContinue;
            _selectedConfig = "";
            _dropdownOpen   = false;
            _searchQuery[0] = "";
            _searchQuery[1] = "";
            SkinPool.Init();
            _cells.Clear();
            if (_root != null) Destroy(_root);
            _root = BuildUI();
            IsOpen = true;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (IsOpen && _root != null)
            {
                IsOpen = false;
                Destroy(_root); _root = null;
                _onContinue = null;
            }
        }

        private void OnDestroy()
        {
            IsOpen = false;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }


        private GameObject BuildUI()
        {
            EnsureEventSystem();

            var cvGO = new GameObject("JMS_Canvas");
            cvGO.transform.SetParent(transform, false);

            var cv = cvGO.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 9999;
            var csc = cvGO.AddComponent<CanvasScaler>();
            csc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            csc.referenceResolution = new Vector2(1920f, 1080f);
            csc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            csc.matchWidthOrHeight = 0.5f;
            cvGO.AddComponent<GraphicRaycaster>();

            var blocker = Img(cvGO.transform, "Blocker", C_BG);
            Stretch(blocker.rt);
            var bswallow = blocker.go.AddComponent<Button>();
            bswallow.transition = Selectable.Transition.None;
            bswallow.onClick.AddListener(CloseAllPopups);

            var content = Img(cvGO.transform, "Content", Color.clear);
            content.img.raycastTarget = false;
            content.rt.anchorMin = Vector2.zero; content.rt.anchorMax = Vector2.one;
            content.rt.offsetMin = new Vector2(0f, H_FOOTER);
            content.rt.offsetMax = new Vector2(0f, -(H_HEADER + H_CONFIG));

            BuildTeamHalf(content.tf, cvGO.transform, 0, "�-�  EAGLE", C_EAG_ACC, C_EAGLE,
                          0f, 0.5f, isEagle: true);

            var div = Img(content.tf, "Div", C_SEP); div.img.raycastTarget = false;
            SetAnchOff(div.rt, 0.5f, 0f, 0.5f, 1f, -1f, 0f, 1f, 0f);

            BuildTeamHalf(content.tf, cvGO.transform, 1, "RAVEN  ▶", C_RAV_ACC, C_RAVEN,
                          0.5f, 1f, isEagle: false);

            var header = Img(cvGO.transform, "Header", C_HEADER);
            PinTop(header.rt, H_HEADER);
            Lbl(header.tf, "�-�  CHOOSE TEAM SKINS  ▶", 20, FontStyle.Bold, C_WHITE);
            var hSep = Img(header.tf, "Sep", C_SEP); hSep.img.raycastTarget = false;
            PinBottom(hSep.rt, 2f);

            var cfg = Img(cvGO.transform, "CfgBar", C_CONFIG);
            cfg.rt.anchorMin = new Vector2(0f, 1f); cfg.rt.anchorMax = new Vector2(1f, 1f);
            cfg.rt.offsetMin = new Vector2(0f, -(H_HEADER + H_CONFIG));
            cfg.rt.offsetMax = new Vector2(0f, -H_HEADER);
            BuildConfigBar(cfg.tf, cvGO.transform);
            var cSep = Img(cfg.tf, "Sep", C_SEP); cSep.img.raycastTarget = false;
            PinBottom(cSep.rt, 2f);

            var footer = Img(cvGO.transform, "Footer", C_FOOTER);
            PinBottom(footer.rt, H_FOOTER);
            var fSep = Img(footer.tf, "Sep", C_SEP); fSep.img.raycastTarget = false;
            PinTop(fSep.rt, 2f);

            _statusText = Lbl(footer.tf, "", 11, FontStyle.Italic, C_DIM);
            _statusText.raycastTarget = false;
            SetAnchOff(_statusText.GetComponent<RectTransform>(), 0.20f, 0f, 0.65f, 1f);

            Btn(footer.tf, "⚙  OPTIONS", new Color(0.10f, 0.12f, 0.22f, 1f), 12, FontStyle.Bold, C_WHITE,
                new Vector2(0.12f, 0f), new Vector2(0.28f, 1f),
                new Vector2(PAD, 8f), new Vector2(-PAD, -8f))
                .onClick.AddListener(ShowOptionsModal);

            Btn(footer.tf, "CLEAR ALL", C_CLEAR, 13, FontStyle.Bold, C_WHITE,
                new Vector2(0f, 0f), new Vector2(0.12f, 1f),
                new Vector2(PAD, 8f), new Vector2(-PAD, -8f))
                .onClick.AddListener(OnClearAll);

            Btn(footer.tf, "✕ CANCEL", new Color(0.35f, 0.25f, 0.08f, 1f), 13, FontStyle.Bold, C_WHITE,
                new Vector2(0.65f, 0f), new Vector2(0.78f, 1f),
                new Vector2(PAD, 8f), new Vector2(-PAD, -8f))
                .onClick.AddListener(OnCancelRequest);

            Btn(footer.tf, "CONTINUE  ▶", C_CONT, 15, FontStyle.Bold, C_WHITE,
                new Vector2(0.78f, 0f), new Vector2(1f, 1f),
                new Vector2(PAD, 8f), new Vector2(-PAD, -8f))
                .onClick.AddListener(OnContinue);

            BuildCancelModal(cvGO.transform);

            BuildOptionsModal(cvGO.transform);

            return cvGO;
        }


        private void BuildTeamHalf(Transform parent, Transform canvasRoot,
            int team, string label, Color accent, Color bgCol,
            float ancL, float ancR, bool isEagle)
        {
            var half = Img(parent, isEagle ? "Eagle" : "Raven", bgCol);
            half.img.raycastTarget = false;
            SetAnchOff(half.rt, ancL, 0f, ancR, 1f, isEagle ? 0f : 2f, 0f, isEagle ? -2f : 0f, 0f);

            var teamLbl = Img(half.tf, "TLbl",
                new Color(accent.r * 0.3f, accent.g * 0.3f, accent.b * 0.3f, 1f));
            teamLbl.img.raycastTarget = false;
            PinTop(teamLbl.rt, H_TEAM);
            Lbl(teamLbl.tf, label, 14, FontStyle.Bold, accent);

            var body = Img(half.tf, "Body", Color.clear);
            body.img.raycastTarget = false;
            body.rt.anchorMin = Vector2.zero; body.rt.anchorMax = Vector2.one;
            body.rt.offsetMin = Vector2.zero; body.rt.offsetMax = new Vector2(0f, -H_TEAM);

            float sideL, sideR, gridL, gridR;
            if (isEagle)
            {
                sideL = 0f;      sideR = SIDEBAR_RATIO;
                gridL = SIDEBAR_RATIO; gridR = 1f;
            }
            else
            {
                gridL = 0f;      gridR = GRID_RATIO;
                sideL = GRID_RATIO; sideR = 1f;
            }

            var sidebar = Img(body.tf, "Sidebar", C_SIDEBAR);
            sidebar.img.raycastTarget = false;
            SetAnchOff(sidebar.rt, sideL, 0f, sideR, 1f);
            BuildSidebar(sidebar.tf, canvasRoot, team);

            float sepX = isEagle ? SIDEBAR_RATIO : GRID_RATIO;
            var sDiv = Img(body.tf, "SDiv", C_SEP); sDiv.img.raycastTarget = false;
            SetAnchOff(sDiv.rt, sepX, 0f, sepX, 1f, -1f, 0f, 1f, 0f);

            var gridArea = Img(body.tf, "GridArea", Color.clear);
            gridArea.img.raycastTarget = false;
            SetAnchOff(gridArea.rt, gridL, 0f, gridR, 1f);
            BuildSkinGrid(gridArea.tf, team, accent);
        }


        private void BuildSkinGrid(Transform parent, int team, Color accent)
        {
            const float SEARCH_H = 30f;
            var searchGO = new GameObject("Search"); searchGO.transform.SetParent(parent, false);
            var searchRT = searchGO.AddComponent<RectTransform>();
            searchRT.anchorMin = new Vector2(0f, 1f); searchRT.anchorMax = new Vector2(1f, 1f);
            searchRT.pivot = new Vector2(0.5f, 1f);
            searchRT.anchoredPosition = Vector2.zero;
            searchRT.sizeDelta = new Vector2(0f, SEARCH_H);
            searchGO.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.11f, 1f);

            var iconLbl = Lbl(searchRT, "\uD83D\uDD0D", 12, FontStyle.Normal, C_DIM);
            iconLbl.alignment = TextAnchor.MiddleLeft;
            var iconRT = iconLbl.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = new Vector2(6f, 0f); iconRT.offsetMax = new Vector2(-4f, 0f);

            var inGO = new GameObject("Input"); inGO.transform.SetParent(searchGO.transform, false);
            var inRT = inGO.AddComponent<RectTransform>();
            inRT.anchorMin = Vector2.zero; inRT.anchorMax = Vector2.one;
            inRT.offsetMin = new Vector2(26f, 3f); inRT.offsetMax = new Vector2(-6f, -3f);
            inGO.AddComponent<Image>().color = C_INPUT_BG;

            var searchField = inGO.AddComponent<InputField>();
            searchField.characterLimit = 40;
            searchField.placeholder = CfgTxt(new GameObject("PH"), inGO.transform, "Search skins...", true);
            searchField.textComponent = CfgTxt(new GameObject("TXT"), inGO.transform, "", false);
            searchField.text = _searchQuery[team];

            var capturedTeam = team;
            searchField.onValueChanged.AddListener(q =>
            {
                _searchQuery[capturedTeam] = q;
                OnSearchChanged(capturedTeam, q);
            });

            var scrollGO = new GameObject("Scroll"); scrollGO.transform.SetParent(parent, false);
            var sRT = scrollGO.AddComponent<RectTransform>();
            sRT.anchorMin = Vector2.zero; sRT.anchorMax = Vector2.one;
            sRT.offsetMin = Vector2.zero; sRT.offsetMax = new Vector2(0f, -SEARCH_H);
            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.scrollSensitivity = 40f; sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = true; sr.decelerationRate = 0.15f;

            var vpGO = new GameObject("VP"); vpGO.transform.SetParent(scrollGO.transform, false);
            var vpRT = vpGO.AddComponent<RectTransform>(); Stretch(vpRT);
            vpGO.AddComponent<RectMask2D>(); sr.viewport = vpRT;

            var gridGO = new GameObject("Grid"); gridGO.transform.SetParent(vpGO.transform, false);
            var gridRT = gridGO.AddComponent<RectTransform>();
            gridRT.anchorMin = new Vector2(0f, 1f); gridRT.anchorMax = new Vector2(1f, 1f);
            gridRT.pivot = new Vector2(0.5f, 1f); gridRT.sizeDelta = Vector2.zero;
            gridGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var glg = gridGO.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(CELL_W, CELL_H);
            glg.spacing = new Vector2(GAP, GAP);
            glg.padding = new RectOffset((int)PAD, (int)PAD, (int)PAD, (int)PAD);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = COLS;
            glg.childAlignment = TextAnchor.UpperLeft;
            sr.content = gridRT;

            PopulateSkins(gridRT.transform, team, accent);
        }

        private void PopulateSkins(Transform grid, int team, Color accent)
        {
            var list = new List<ActorSkin>();
            var def = ActorManager.instance?.defaultActorSkin;
            if (def != null) list.Add(def);
            if (ModManager.instance?.actorSkins != null)
                foreach (var s in ModManager.instance.actorSkins)
                    if (s != null && !list.Contains(s)) list.Add(s);

            if (list.Count == 0) { Lbl(grid, "No skins loaded", 12, FontStyle.Italic, C_DIM); return; }
            foreach (var skin in list) CreateCell(grid, team, skin, accent);
        }

        private void CreateCell(Transform grid, int team, ActorSkin skin, Color accent)
        {
            var cellGO = new GameObject($"Cell_{skin.name}");
            cellGO.transform.SetParent(grid, false);
            cellGO.AddComponent<RectTransform>();
            bool sel = SkinPool.IsSelected(team, skin);
            var bg = cellGO.AddComponent<Image>(); bg.color = sel ? C_CELL_SEL : C_CELL;

            var outGO = new GameObject("Out"); outGO.transform.SetParent(cellGO.transform, false);
            outGO.transform.SetAsFirstSibling();
            var outRT = outGO.AddComponent<RectTransform>();
            outRT.anchorMin = Vector2.zero; outRT.anchorMax = Vector2.one;
            outRT.offsetMin = new Vector2(-3f, -3f); outRT.offsetMax = new Vector2(3f, 3f);
            var outline = outGO.AddComponent<Image>(); outline.color = accent; outline.enabled = sel;

            var portGO = new GameObject("Port"); portGO.transform.SetParent(cellGO.transform, false);
            var portRT = portGO.AddComponent<RectTransform>();
            portRT.anchorMin = new Vector2(0f, 1f); portRT.anchorMax = new Vector2(1f, 1f);
            portRT.pivot = new Vector2(0.5f, 1f);
            portRT.anchoredPosition = Vector2.zero; portRT.sizeDelta = new Vector2(0f, PORT_H);
            var raw = portGO.AddComponent<RawImage>(); raw.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            TryRenderPortrait(raw, skin, team);

            var nlGO = new GameObject("Name"); nlGO.transform.SetParent(cellGO.transform, false);
            var nlRT = nlGO.AddComponent<RectTransform>();
            nlRT.anchorMin = Vector2.zero; nlRT.anchorMax = Vector2.one;
            nlRT.offsetMin = new Vector2(4f, 4f); nlRT.offsetMax = new Vector2(-4f, -PORT_H);
            var nl = nlGO.AddComponent<Text>();
            nl.text = skin.name ?? "(default)"; nl.font = Fnt();
            nl.fontSize = 12; nl.color = C_WHITE; nl.alignment = TextAnchor.MiddleCenter;
            nl.horizontalOverflow = HorizontalWrapMode.Wrap;
            nl.verticalOverflow = VerticalWrapMode.Truncate;
            nl.resizeTextForBestFit = true; nl.resizeTextMinSize = 8; nl.resizeTextMaxSize = 13;
            nl.raycastTarget = false;

            var btn = cellGO.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); btn.colors = cb;
            btn.targetGraphic = bg;

            var cTeam = team; var cSkin = skin; var cOut = outline; var cBg = bg;
            btn.onClick.AddListener(() =>
            {
                CloseAllPopups();
                SkinPool.Toggle(cTeam, cSkin);
                bool s = SkinPool.IsSelected(cTeam, cSkin);
                cOut.enabled = s; cBg.color = s ? C_CELL_SEL : C_CELL;
                RefreshSidebar(cTeam);
            });

            _cells.Add(new CellState { team = team, skin = skin, outline = outline, bg = bg, go = cellGO });
        }


        private static Camera       _portraitCam;
        private static RenderTexture _portraitRT;
        private static GameObject   _portraitRoot;
        private static SkinnedMeshRenderer _portraitSmr;

        private static bool EnsureCustomPortraitSetup()
        {
            if (_portraitCam != null) return true;
            try
            {
                _portraitRoot = new GameObject("JMS_PortraitStudio");
                _portraitRoot.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_portraitRoot);

                var lightGO = new GameObject("Light");
                lightGO.transform.SetParent(_portraitRoot.transform, false);
                lightGO.transform.localRotation = Quaternion.Euler(30f, -30f, 0f);
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.cullingMask = 1 << 31;

                var camGO = new GameObject("Cam");
                camGO.transform.SetParent(_portraitRoot.transform, false);
                camGO.transform.localPosition = new Vector3(0f, 1.6f, 1.2f);
                camGO.transform.localRotation = Quaternion.Euler(5f, 180f, 0f);
                _portraitCam = camGO.AddComponent<Camera>();
                _portraitCam.cullingMask  = 1 << 31;
                _portraitCam.clearFlags   = CameraClearFlags.SolidColor;
                _portraitCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _portraitCam.nearClipPlane = 0.1f;
                _portraitCam.farClipPlane  = 10f;
                _portraitCam.fieldOfView   = 30f;
                _portraitCam.enabled       = false;
                _portraitCam.forceIntoRenderTexture = true;

                _portraitRT = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
                _portraitRT.Create();

                var smrGO = new GameObject("Model");
                smrGO.transform.SetParent(_portraitRoot.transform, false);
                smrGO.layer = 31;
                _portraitSmr = smrGO.AddComponent<SkinnedMeshRenderer>();

                return true;
            }
            catch
            {
                if (_portraitRoot != null) UnityEngine.Object.Destroy(_portraitRoot);
                _portraitRoot = null; _portraitCam = null; _portraitRT = null; _portraitSmr = null;
                return false;
            }
        }

        private static Texture2D RenderCustomPortrait(ActorSkin.MeshSkin ms, int team)
        {
            if (!EnsureCustomPortraitSetup()) return null;

            _portraitRoot.SetActive(true);

            _portraitSmr.sharedMesh = ms.mesh;
            var mats = new Material[ms.mesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = (ms.materials != null && i < ms.materials.Length && ms.materials[i] != null)
                    ? ms.materials[i]
                    : new Material(Shader.Find("Standard"));
            _portraitSmr.sharedMaterials = mats;

            try { ActorManager.ApplyOverrideMeshSkin(_portraitSmr, ms, team); }
            catch { }

            _portraitCam.targetTexture = _portraitRT;
            _portraitCam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = _portraitRT;
            var tex = new Texture2D(256, 256, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            _portraitRoot.SetActive(false);
            return tex;
        }


        private static void TryRenderPortrait(RawImage raw, ActorSkin skin, int team)
        {
            try
            {
                var ms = skin?.characterSkin;
                if (ms == null || ms.mesh == null) return;

                if (RuntimePortraitGenerator.instance != null)
                {
                    try
                    {
                        var tex = RuntimePortraitGenerator.RenderSkinPortrait(ms, team);
                        if (tex != null) { raw.texture = tex; raw.color = Color.white; return; }
                    }
                    catch { }

                    try
                    {
                        var inst = RuntimePortraitGenerator.instance;
                        inst.rootObject.SetActive(true);
                        ActorManager.ApplyOverrideMeshSkin(inst.actorRenderer, ms, team);
                        inst.camera.targetTexture = inst.renderTexture;
                        inst.camera.Render();

                        var prev = RenderTexture.active;
                        RenderTexture.active = inst.renderTexture;
                        var fb = new Texture2D(inst.renderTexture.width, inst.renderTexture.height,
                                               TextureFormat.ARGB32, false);
                        fb.ReadPixels(new Rect(0, 0, inst.renderTexture.width, inst.renderTexture.height), 0, 0);
                        fb.Apply();
                        RenderTexture.active = prev;
                        inst.rootObject.SetActive(false);
                        raw.texture = fb; raw.color = Color.white;
                        return;
                    }
                    catch { }
                }

                try
                {
                    var custom = RenderCustomPortrait(ms, team);
                    if (custom != null) { raw.texture = custom; raw.color = Color.white; return; }
                }
                catch { }

                raw.color = team == 0
                    ? new Color(0.25f, 0.35f, 0.55f, 1f)
                    : new Color(0.55f, 0.25f, 0.25f, 1f);
            }
            catch { }
        }


        private void BuildSidebar(Transform parent, Transform canvasRoot, int team)
        {
            var title = Img(parent, "STitle", new Color(0.08f, 0.08f, 0.13f, 1f));
            title.img.raycastTarget = false;
            PinTop(title.rt, 28f);
            Lbl(title.tf, "SELECTED", 10, FontStyle.Bold, C_DIM);

            var scrollGO = new GameObject("SScroll"); scrollGO.transform.SetParent(parent, false);
            var sRT = scrollGO.AddComponent<RectTransform>();
            sRT.anchorMin = Vector2.zero; sRT.anchorMax = Vector2.one;
            sRT.offsetMin = Vector2.zero; sRT.offsetMax = new Vector2(0f, -28f);
            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.scrollSensitivity = 30f; sr.movementType = ScrollRect.MovementType.Clamped;

            var vpGO = new GameObject("VP"); vpGO.transform.SetParent(scrollGO.transform, false);
            var vpRT = vpGO.AddComponent<RectTransform>(); Stretch(vpRT);
            vpGO.AddComponent<RectMask2D>(); sr.viewport = vpRT;

            var cGO = new GameObject("SContent"); cGO.transform.SetParent(vpGO.transform, false);
            var cRT = cGO.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f); cRT.sizeDelta = Vector2.zero;
            cGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = cGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3f; vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            sr.content = cRT;

            _sidebarContent[team] = cGO.transform;
            RefreshSidebar(team);
        }

        private void RefreshSidebar(int team)
        {
            var container = _sidebarContent[team];
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            var selected = SkinPool.GetSelected(team);
            if (selected.Count == 0)
            {
                Lbl(container, "None selected", 10, FontStyle.Italic, C_DIM);
                return;
            }

            foreach (var skin in selected)
            {
                var entGO = new GameObject("SE_" + skin.name);
                entGO.transform.SetParent(container, false);
                var le = entGO.AddComponent<LayoutElement>(); le.preferredHeight = SEL_ENTRY_H;
                var entBg = entGO.AddComponent<Image>(); entBg.color = C_SEL_ENTRY;

                var pGO = new GameObject("SP"); pGO.transform.SetParent(entGO.transform, false);
                var pRT = pGO.AddComponent<RectTransform>();
                pRT.anchorMin = new Vector2(0f, 0f); pRT.anchorMax = new Vector2(0f, 1f);
                pRT.offsetMin = new Vector2(4f, 3f); pRT.offsetMax = new Vector2(SEL_ENTRY_H, -3f);
                var pRaw = pGO.AddComponent<RawImage>(); pRaw.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                TryRenderPortrait(pRaw, skin, team);

                var nGO = new GameObject("SN"); nGO.transform.SetParent(entGO.transform, false);
                var nRT = nGO.AddComponent<RectTransform>();
                nRT.anchorMin = new Vector2(0f, 0f); nRT.anchorMax = new Vector2(0.5f, 1f);
                nRT.offsetMin = new Vector2(SEL_ENTRY_H + 4f, 0f);
                nRT.offsetMax = new Vector2(-12f, 0f);
                var nTxt = nGO.AddComponent<Text>();
                nTxt.text = skin.name ?? "?"; nTxt.font = Fnt(); nTxt.fontSize = 10;
                nTxt.color = C_WHITE; nTxt.alignment = TextAnchor.MiddleLeft;
                nTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                nTxt.resizeTextForBestFit = true; nTxt.resizeTextMinSize = 7; nTxt.resizeTextMaxSize = 11;
                nTxt.raycastTarget = false;

                bool isVehOnly = SkinPool.GetVehicleOnly(team, skin);
                SkinRole curRoles = SkinPool.GetRoles(team, skin);
                var vGO = new GameObject("VO"); vGO.transform.SetParent(entGO.transform, false);
                var vRT = vGO.AddComponent<RectTransform>();
                vRT.anchorMin = new Vector2(0.5f, 0.5f); vRT.anchorMax = new Vector2(0.5f, 0.5f);
                vRT.pivot = new Vector2(0.5f, 0.5f);
                vRT.sizeDelta = new Vector2(18f, 18f);
                vRT.anchoredPosition = Vector2.zero;
                var vBg = vGO.AddComponent<Image>();
                vBg.color = new Color(0.12f, 0.12f, 0.20f, 1f);

                var vcGO = new GameObject("VCK"); vcGO.transform.SetParent(vGO.transform, false);
                var vcRT = vcGO.AddComponent<RectTransform>();
                vcRT.anchorMin = new Vector2(0.15f, 0.15f); vcRT.anchorMax = new Vector2(0.85f, 0.85f);
                vcRT.offsetMin = vcRT.offsetMax = Vector2.zero;
                var vCheck = vcGO.AddComponent<Image>();
                vCheck.color = new Color(0.30f, 0.70f, 1.0f, 1f);
                vCheck.enabled = isVehOnly;

                var capTeamV = team; var capSkinV = skin;
                var vBtn = vGO.AddComponent<Button>();
                vBtn.transition = Selectable.Transition.None;
                vBtn.onClick.AddListener(() => {
                    SkinPool.ToggleVehicleOnly(capTeamV, capSkinV);
                    RefreshSidebar(capTeamV);
                });

                var tooltipGO = new GameObject("Tooltip"); tooltipGO.transform.SetParent(vGO.transform, false);
                var ttRT = tooltipGO.AddComponent<RectTransform>();
                ttRT.anchorMin = new Vector2(0.5f, 1f); ttRT.anchorMax = new Vector2(0.5f, 1f);
                ttRT.pivot = new Vector2(0.5f, 0f);
                ttRT.sizeDelta = new Vector2(130f, 18f);
                ttRT.anchoredPosition = new Vector2(0f, 4f);
                var ttBg = tooltipGO.AddComponent<Image>();
                ttBg.color = new Color(0.05f, 0.05f, 0.10f, 0.95f);
                var ttLbl = Lbl(tooltipGO.transform, "Only apply skin on vehicles", 8, FontStyle.Italic, C_DIM);
                ttLbl.alignment = TextAnchor.MiddleCenter;
                tooltipGO.SetActive(false);

                var trigger = vGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
                var capTTGO = tooltipGO;
                enterEntry.callback.AddListener((_) => { if (capTTGO != null) capTTGO.SetActive(true); });
                trigger.triggers.Add(enterEntry);
                var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
                exitEntry.callback.AddListener((_) => { if (capTTGO != null) capTTGO.SetActive(false); });
                trigger.triggers.Add(exitEntry);

                SkinRarityTier curTier = SkinPool.GetRarity(team, skin);
                var rBtn = Btn(entGO.transform, SkinRarityInfo.Names[(int)curTier],
                    new Color(0.08f, 0.08f, 0.14f, 1f), 9, FontStyle.Bold,
                    SkinRarityInfo.Colors[(int)curTier],
                    new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(-66f, 2f), new Vector2(-2f, -2f));

                var capTeam = team; var capSkin = skin;
                rBtn.onClick.AddListener(() => ShowRarityPicker(capTeam, capSkin, rBtn.GetComponent<RectTransform>()));

                if (VoicePackScanner.ENABLED)
                {
                var voiceEntries = SkinPool.GetVoiceEntries(team, skin);
                string vpLabel;
                Color vpLabelColor;
                if (voiceEntries.Count == 0)
                {
                    vpLabel = "-";
                    vpLabelColor = C_DIM;
                }
                else if (voiceEntries.Count == 1 && voiceEntries[0].mutatorId == -2)
                {
                    vpLabel = "Random";
                    vpLabelColor = C_WHITE;
                }
                else
                {
                    vpLabel = $"{voiceEntries.Count} voice{(voiceEntries.Count > 1 ? "s" : "")}";
                    vpLabelColor = C_WHITE;
                }

                var vpBtn = Btn(entGO.transform, "",
                    new Color(0.08f, 0.10f, 0.16f, 1f), 8, FontStyle.Normal, C_WHITE,
                    new Vector2(0.54f, 0f), new Vector2(1f, 1f),
                    new Vector2(0f, 2f), new Vector2(-68f, -2f));

                var vpIconTxt = Lbl(vpBtn.targetGraphic.transform, "\u266B", 11, FontStyle.Bold,
                    new Color(0.50f, 0.75f, 1.0f, 1f));
                vpIconTxt.alignment = TextAnchor.MiddleLeft;
                var vpIconRT = vpIconTxt.GetComponent<RectTransform>();
                vpIconRT.anchorMin = Vector2.zero; vpIconRT.anchorMax = Vector2.one;
                vpIconRT.offsetMin = new Vector2(3f, 0f); vpIconRT.offsetMax = new Vector2(-2f, 0f);

                var vpNameTxt = Lbl(vpBtn.targetGraphic.transform, vpLabel, 8, FontStyle.Normal, vpLabelColor);
                vpNameTxt.alignment = TextAnchor.MiddleLeft;
                var vpNameRT = vpNameTxt.GetComponent<RectTransform>();
                vpNameRT.anchorMin = Vector2.zero; vpNameRT.anchorMax = Vector2.one;
                vpNameRT.offsetMin = new Vector2(14f, 0f); vpNameRT.offsetMax = new Vector2(-2f, 0f);

                var capTeamVp = team; var capSkinVp = skin;
                vpBtn.onClick.AddListener(() => { _voiceScrollPos = 1f; ShowVoicePackPicker(capTeamVp, capSkinVp, vpBtn.GetComponent<RectTransform>()); });
                }

                if (isVehOnly && curRoles == SkinRole.None)
                {
                    var warnGO = new GameObject("Warn"); warnGO.transform.SetParent(container, false);
                    warnGO.AddComponent<LayoutElement>().preferredHeight = 16f;
                    var wLbl = Lbl(warnGO.transform, "⚠ No vehicle roles assigned, skipping skin", 8,
                                   FontStyle.Italic, new Color(1f, 0.85f, 0.20f, 1f));
                    wLbl.alignment = TextAnchor.MiddleCenter;
                }
            }
        }


        private void ShowRarityPicker(int team, ActorSkin skin, RectTransform anchorRT)
        {
            CloseRarityPicker();
            _rarityDropTeam = team;
            _rarityDropSkin = skin;

            _rarityDropGO = new GameObject("RarityRoleDrop");
            _rarityDropGO.transform.SetParent(_root.transform, false);

            var dRT = _rarityDropGO.AddComponent<RectTransform>();
            var canvasRT = _root.GetComponent<RectTransform>();
            float pickerW = 150f;

            Vector3 worldPos = anchorRT.position;
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out localPos);

            dRT.anchorMin = new Vector2(0.5f, 0.5f);
            dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.pivot = new Vector2(0.5f, 1f);
            dRT.anchoredPosition = localPos;
            dRT.sizeDelta = new Vector2(pickerW, 0f);

            var dBg = _rarityDropGO.AddComponent<Image>(); dBg.color = C_DDR_BG;

            var vlg = _rarityDropGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4); vlg.spacing = 2f;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            _rarityDropGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddSectionHeader(_rarityDropGO.transform, "RARITY");

            for (int i = 0; i < SkinRarityInfo.TierCount; i++)
            {
                var itemGO = new GameObject("RT_" + SkinRarityInfo.Names[i]);
                itemGO.transform.SetParent(_rarityDropGO.transform, false);
                var le = itemGO.AddComponent<LayoutElement>(); le.preferredHeight = 24f;
                var iBg = itemGO.AddComponent<Image>(); iBg.color = C_DDR_ITEM;
                var iBtn = itemGO.AddComponent<Button>();
                var col = iBtn.colors;
                col.normalColor = Color.white; col.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
                col.pressedColor = new Color(0.8f, 0.8f, 0.8f); iBtn.colors = col;
                iBtn.targetGraphic = iBg;

                var lbl = Lbl(itemGO.transform, SkinRarityInfo.Names[i], 10, FontStyle.Bold,
                              SkinRarityInfo.Colors[i]);
                lbl.alignment = TextAnchor.MiddleCenter;

                var wTxt = Lbl(itemGO.transform, $"({SkinRarityInfo.Weights[i]}%)", 8, FontStyle.Normal, C_DIM);
                wTxt.alignment = TextAnchor.MiddleRight;
                var wRT = wTxt.GetComponent<RectTransform>();
                wRT.offsetMax = new Vector2(-4f, 0f);

                var capTier = (SkinRarityTier)i;
                iBtn.onClick.AddListener(() => OnRarityChosen(capTier));
            }

            AddSectionHeader(_rarityDropGO.transform, "ROLES");

            SkinRole currentRoles = SkinPool.GetRoles(team, skin);
            int lastGroup = -1;
            for (int i = 0; i < SkinRoleInfo.RoleCount; i++)
            {
                int group = SkinRoleInfo.GroupOf(i);
                if (group != lastGroup)
                {
                    lastGroup = group;
                    AddGroupHeader(_rarityDropGO.transform, SkinRoleInfo.GroupHeaders[group]);
                }

                SkinRole thisRole = SkinRoleInfo.AllRoles[i];
                bool isOn = (currentRoles & thisRole) != 0;

                var rowGO = new GameObject("Role_" + SkinRoleInfo.Names[i]);
                rowGO.transform.SetParent(_rarityDropGO.transform, false);
                rowGO.AddComponent<LayoutElement>().preferredHeight = 22f;
                var rowBg = rowGO.AddComponent<Image>(); rowBg.color = C_DDR_ITEM;

                var cbGO = new GameObject("CB"); cbGO.transform.SetParent(rowGO.transform, false);
                var cbRT = cbGO.AddComponent<RectTransform>();
                cbRT.anchorMin = new Vector2(0f, 0.2f); cbRT.anchorMax = new Vector2(0f, 0.8f);
                cbRT.offsetMin = new Vector2(6f, 0f); cbRT.offsetMax = new Vector2(20f, 0f);
                cbGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.14f, 1f);

                var ckGO = new GameObject("CK"); ckGO.transform.SetParent(cbGO.transform, false);
                var ckRT = ckGO.AddComponent<RectTransform>();
                ckRT.anchorMin = new Vector2(0.15f, 0.15f); ckRT.anchorMax = new Vector2(0.85f, 0.85f);
                ckRT.offsetMin = ckRT.offsetMax = Vector2.zero;
                var ckImg = ckGO.AddComponent<Image>();
                ckImg.color = C_CHECK;
                ckImg.enabled = isOn;

                var rlbl = Lbl(rowGO.transform, SkinRoleInfo.Names[i], 9, FontStyle.Normal,
                               isOn ? C_WHITE : C_DIM);
                rlbl.alignment = TextAnchor.MiddleLeft;
                var rlRT = rlbl.GetComponent<RectTransform>();
                rlRT.anchorMin = Vector2.zero; rlRT.anchorMax = Vector2.one;
                rlRT.offsetMin = new Vector2(24f, 0f); rlRT.offsetMax = Vector2.zero;

                var capRole = thisRole;
                var capCk = ckImg;
                var capLbl = rlbl;
                var btn = rowGO.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    SkinPool.ToggleRole(_rarityDropTeam, _rarityDropSkin, capRole);
                    bool nowOn = (SkinPool.GetRoles(_rarityDropTeam, _rarityDropSkin) & capRole) != 0;
                    capCk.enabled = nowOn;
                    capLbl.color = nowOn ? C_WHITE : C_DIM;
                    RefreshSidebar(_rarityDropTeam);
                });
            }

            Canvas.ForceUpdateCanvases();
            float halfCanvasW = canvasRT.rect.width  * 0.5f;
            float halfCanvasH = canvasRT.rect.height * 0.5f;
            float halfPickerW = pickerW * 0.5f;
            float pickerH = dRT.rect.height;
            localPos.x = Mathf.Clamp(localPos.x, -halfCanvasW + halfPickerW, halfCanvasW - halfPickerW);
            if (localPos.y - pickerH < -halfCanvasH)
                localPos.y = -halfCanvasH + pickerH;
            if (localPos.y > halfCanvasH)
                localPos.y = halfCanvasH;
            dRT.anchoredPosition = localPos;
        }

        private static void AddSectionHeader(Transform parent, string text)
        {
            var hdrGO = new GameObject("Hdr_" + text);
            hdrGO.transform.SetParent(parent, false);
            hdrGO.AddComponent<LayoutElement>().preferredHeight = 18f;
            var hLbl = Lbl(hdrGO.transform, text, 9, FontStyle.Bold, C_DIM);
            hLbl.alignment = TextAnchor.MiddleCenter;
        }

        private static void AddGroupHeader(Transform parent, string text)
        {
            var grpGO = new GameObject("Grp_" + text);
            grpGO.transform.SetParent(parent, false);
            grpGO.AddComponent<LayoutElement>().preferredHeight = 16f;
            var gLbl = Lbl(grpGO.transform, "─ " + text + " ─", 8, FontStyle.Italic, new Color(0.45f, 0.45f, 0.55f, 1f));
            gLbl.alignment = TextAnchor.MiddleCenter;
        }

        private void OnRarityChosen(SkinRarityTier tier)
        {
            SkinPool.SetRarity(_rarityDropTeam, _rarityDropSkin, tier);
            CloseRarityPicker();
            RefreshSidebar(_rarityDropTeam);
        }

        private void CloseRarityPicker()
        {
            if (_rarityDropGO != null) { Destroy(_rarityDropGO); _rarityDropGO = null; }
        }


        private void ShowVoicePackPicker(int team, ActorSkin skin, RectTransform anchorRT)
        {
            CloseVoicePackPicker();
            _voiceDropTeam = team;
            _voiceDropSkin = skin;

            _voiceDropGO = new GameObject("VoicePackDrop");
            _voiceDropGO.transform.SetParent(_root.transform, false);

            var dRT = _voiceDropGO.AddComponent<RectTransform>();
            var canvasRT = _root.GetComponent<RectTransform>();
            float pickerW = 260f;

            Vector3 worldPos = anchorRT.position;
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out localPos);

            dRT.anchorMin = new Vector2(0.5f, 0.5f);
            dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.pivot = new Vector2(0.5f, 1f);
            dRT.anchoredPosition = localPos;
            dRT.sizeDelta = new Vector2(pickerW, 0f);

            var dBg = _voiceDropGO.AddComponent<Image>(); dBg.color = C_DDR_BG;

            var scrollGO = new GameObject("VScroll"); scrollGO.transform.SetParent(_voiceDropGO.transform, false);
            var sRT = scrollGO.AddComponent<RectTransform>(); Stretch(sRT);
            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.scrollSensitivity = 30f; sr.movementType = ScrollRect.MovementType.Clamped;

            var vpGO = new GameObject("VP"); vpGO.transform.SetParent(scrollGO.transform, false);
            var vpRT = vpGO.AddComponent<RectTransform>(); Stretch(vpRT);
            vpGO.AddComponent<RectMask2D>(); sr.viewport = vpRT;

            var cGO = new GameObject("VContent"); cGO.transform.SetParent(vpGO.transform, false);
            var cRT = cGO.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f); cRT.sizeDelta = Vector2.zero;
            cGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = cGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4); vlg.spacing = 2f;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            sr.content = cRT;

            {
                var clearGO = new GameObject("VP_Clear");
                clearGO.transform.SetParent(cGO.transform, false);
                clearGO.AddComponent<LayoutElement>().preferredHeight = 26f;
                var clearBg = clearGO.AddComponent<Image>(); clearBg.color = new Color(0.15f, 0.08f, 0.08f, 1f);
                var clearLbl = Lbl(clearGO.transform, "✕ Clear All", 10, FontStyle.Bold,
                    new Color(1f, 0.45f, 0.40f, 1f));
                clearLbl.alignment = TextAnchor.MiddleCenter;
                var clearBtn = clearGO.AddComponent<Button>();
                clearBtn.transition = Selectable.Transition.None;
                clearBtn.onClick.AddListener(() =>
                {
                    SkinPool.SetVoiceEntries(_voiceDropTeam, _voiceDropSkin, new List<VoiceEntry>());
                    CloseVoicePackPicker();
                    RefreshSidebar(_voiceDropTeam);
                });
            }

            AddVoicePickerCheckboxRow(cGO.transform, "\uD83C\uDFB2 Random", "Pick randomly from all packs", -2);

            {
                var sepGO = new GameObject("Sep"); sepGO.transform.SetParent(cGO.transform, false);
                sepGO.AddComponent<LayoutElement>().preferredHeight = 4f;
            }

            bool hasTeamVoices = false;
            bool hasPlayerVoices = false;
            foreach (var vp in VoicePackScanner.DetectedPacks)
            {
                if (vp.voicePackType == VoicePackScanner.VoicePackType.TeamVoice) hasTeamVoices = true;
                if (vp.voicePackType == VoicePackScanner.VoicePackType.PlayerVoice) hasPlayerVoices = true;
            }

            if (hasTeamVoices)
            {
                AddSectionHeader(cGO.transform, "TEAM VOICES");
                foreach (var vp in VoicePackScanner.DetectedPacks)
                    if (vp.voicePackType == VoicePackScanner.VoicePackType.TeamVoice)
                        AddVoicePickerCheckboxRow(cGO.transform, vp.mutatorName, vp.sourceMod, vp.mutatorId);
            }

            if (hasPlayerVoices)
            {
                AddSectionHeader(cGO.transform, "PLAYER VOICES");
                foreach (var vp in VoicePackScanner.DetectedPacks)
                    if (vp.voicePackType == VoicePackScanner.VoicePackType.PlayerVoice)
                        AddVoicePickerCheckboxRow(cGO.transform, vp.mutatorName, vp.sourceMod, vp.mutatorId);
            }

            if (!hasTeamVoices && !hasPlayerVoices)
            {
                var emptyLbl = Lbl(cGO.transform, "No voice packs detected", 10, FontStyle.Italic, C_DIM);
                emptyLbl.alignment = TextAnchor.MiddleCenter;
            }

            Canvas.ForceUpdateCanvases();
            float contentH = Mathf.Min(cRT.rect.height, 350f);
            dRT.sizeDelta = new Vector2(pickerW, contentH);

            float halfCanvasW = canvasRT.rect.width  * 0.5f;
            float halfCanvasH = canvasRT.rect.height * 0.5f;
            float halfPickerW = pickerW * 0.5f;
            localPos.x = Mathf.Clamp(localPos.x, -halfCanvasW + halfPickerW, halfCanvasW - halfPickerW);
            if (localPos.y - contentH < -halfCanvasH)
                localPos.y = -halfCanvasH + contentH;
            if (localPos.y > halfCanvasH)
                localPos.y = halfCanvasH;
            dRT.anchoredPosition = localPos;

            Canvas.ForceUpdateCanvases();
            sr.verticalNormalizedPosition = _voiceScrollPos;
        }

        private void AddVoicePickerCheckboxRow(Transform parent, string label, string subtitle, int mutatorId)
        {
            bool isChecked = SkinPool.HasVoiceEntry(_voiceDropTeam, _voiceDropSkin, mutatorId);
            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            float rowH = hasSubtitle ? 36f : 28f;

            var rowGO = new GameObject("VP_" + mutatorId);
            rowGO.transform.SetParent(parent, false);
            rowGO.AddComponent<LayoutElement>().preferredHeight = rowH;
            var rowBg = rowGO.AddComponent<Image>(); rowBg.color = isChecked
                ? new Color(0.10f, 0.16f, 0.28f, 1f) : C_DDR_ITEM;

            var cbGO = new GameObject("CB"); cbGO.transform.SetParent(rowGO.transform, false);
            var cbRT = cbGO.AddComponent<RectTransform>();
            cbRT.anchorMin = new Vector2(0f, 0.5f); cbRT.anchorMax = new Vector2(0f, 0.5f);
            cbRT.pivot = new Vector2(0f, 0.5f);
            cbRT.sizeDelta = new Vector2(16f, 16f);
            cbRT.anchoredPosition = new Vector2(6f, 0f);
            cbGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.14f, 1f);

            var ckGO = new GameObject("CK"); ckGO.transform.SetParent(cbGO.transform, false);
            var ckRT = ckGO.AddComponent<RectTransform>();
            ckRT.anchorMin = new Vector2(0.15f, 0.15f); ckRT.anchorMax = new Vector2(0.85f, 0.85f);
            ckRT.offsetMin = ckRT.offsetMax = Vector2.zero;
            var ckImg = ckGO.AddComponent<Image>();
            ckImg.color = C_CHECK;
            ckImg.enabled = isChecked;

            var lbl = Lbl(rowGO.transform, label, 10,
                isChecked ? FontStyle.Bold : FontStyle.Normal,
                isChecked ? new Color(0.50f, 0.80f, 1.0f, 1f) : C_WHITE);
            lbl.alignment = TextAnchor.MiddleLeft;
            var lRT = lbl.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = new Vector2(26f, hasSubtitle ? 10f : 0f);
            lRT.offsetMax = new Vector2(mutatorId >= 0 ? -70f : -4f, 0f);

            if (hasSubtitle)
            {
                var sub = Lbl(rowGO.transform, subtitle, 7, FontStyle.Italic, C_DIM);
                sub.alignment = TextAnchor.MiddleLeft;
                var sRT2 = sub.GetComponent<RectTransform>();
                sRT2.anchorMin = Vector2.zero; sRT2.anchorMax = Vector2.one;
                sRT2.offsetMin = new Vector2(26f, 0f);
                sRT2.offsetMax = new Vector2(-4f, -18f);
            }

            if (mutatorId >= 0 && isChecked)
            {
                SkinRarityTier curRarity = SkinPool.GetVoiceEntryRarity(_voiceDropTeam, _voiceDropSkin, mutatorId);
                var rarBtn = Btn(rowGO.transform, SkinRarityInfo.Names[(int)curRarity],
                    new Color(0.06f, 0.06f, 0.12f, 1f), 8, FontStyle.Bold,
                    SkinRarityInfo.Colors[(int)curRarity],
                    new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(-66f, 2f), new Vector2(-2f, -2f));

                var capMid = mutatorId;
                rarBtn.onClick.AddListener(() =>
                    ShowVoiceEntryRarityPicker(capMid, rarBtn.GetComponent<RectTransform>()));
            }

            var toggleBtn = rowGO.AddComponent<Button>();
            var col = toggleBtn.colors;
            col.normalColor = Color.white; col.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            col.pressedColor = new Color(0.85f, 0.85f, 0.85f); toggleBtn.colors = col;
            toggleBtn.targetGraphic = rowBg;

            var capId = mutatorId;
            toggleBtn.onClick.AddListener(() =>
            {
                var oldScroll = _voiceDropGO?.GetComponentInChildren<ScrollRect>();
                if (oldScroll != null) _voiceScrollPos = oldScroll.verticalNormalizedPosition;

                SkinPool.ToggleVoiceEntry(_voiceDropTeam, _voiceDropSkin, capId);
                var anchorRT = _voiceDropGO?.GetComponent<RectTransform>();
                if (anchorRT != null)
                {
                    ShowVoicePackPicker(_voiceDropTeam, _voiceDropSkin, anchorRT);
                }
                RefreshSidebar(_voiceDropTeam);
            });
        }

        //TODO: Bound Raven rarity dropdown to screen res
        private void ShowVoiceEntryRarityPicker(int mutatorId, RectTransform anchorRT)
        {
            CloseVoiceRarityPicker();
            _voiceRarityMutatorId = mutatorId;

            _voiceRarityDropGO = new GameObject("VoiceRarityDrop");
            _voiceRarityDropGO.transform.SetParent(_root.transform, false);

            var dRT = _voiceRarityDropGO.AddComponent<RectTransform>();
            var canvasRT = _root.GetComponent<RectTransform>();
            float pickerW = 120f;

            Vector3 worldPos = anchorRT.position;
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out localPos);

            dRT.anchorMin = new Vector2(0.5f, 0.5f);
            dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.pivot = new Vector2(0.5f, 1f);
            dRT.anchoredPosition = localPos;
            dRT.sizeDelta = new Vector2(pickerW, 0f);

            _voiceRarityDropGO.AddComponent<Image>().color = C_DDR_BG;
            var vrVlg = _voiceRarityDropGO.AddComponent<VerticalLayoutGroup>();
            vrVlg.padding = new RectOffset(3, 3, 3, 3); vrVlg.spacing = 2f;
            vrVlg.childForceExpandWidth = true; vrVlg.childForceExpandHeight = false;
            _voiceRarityDropGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < SkinRarityInfo.TierCount; i++)
            {
                var itemGO = new GameObject("VR_" + SkinRarityInfo.Names[i]);
                itemGO.transform.SetParent(_voiceRarityDropGO.transform, false);
                itemGO.AddComponent<LayoutElement>().preferredHeight = 22f;
                var iBg = itemGO.AddComponent<Image>(); iBg.color = C_DDR_ITEM;
                var iBtn = itemGO.AddComponent<Button>();
                var col = iBtn.colors;
                col.normalColor = Color.white; col.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
                col.pressedColor = new Color(0.8f, 0.8f, 0.8f); iBtn.colors = col;
                iBtn.targetGraphic = iBg;

                var tierLbl = Lbl(itemGO.transform, SkinRarityInfo.Names[i], 9, FontStyle.Bold,
                    SkinRarityInfo.Colors[i]);
                tierLbl.alignment = TextAnchor.MiddleCenter;

                var capTier = (SkinRarityTier)i;
                iBtn.onClick.AddListener(() =>
                {
                    SkinPool.SetVoiceEntryRarity(_voiceDropTeam, _voiceDropSkin,
                        _voiceRarityMutatorId, capTier);
                    CloseVoiceRarityPicker();
                    var oldScroll = _voiceDropGO?.GetComponentInChildren<ScrollRect>();
                    if (oldScroll != null) _voiceScrollPos = oldScroll.verticalNormalizedPosition;
                    var vrAnchor = _voiceDropGO?.GetComponent<RectTransform>();
                    if (vrAnchor != null)
                        ShowVoicePackPicker(_voiceDropTeam, _voiceDropSkin, vrAnchor);
                    RefreshSidebar(_voiceDropTeam);
                });
            }

            Canvas.ForceUpdateCanvases();
            float halfCanvasW = canvasRT.rect.width * 0.5f;
            float halfCanvasH = canvasRT.rect.height * 0.5f;
            float halfPickerW = pickerW * 0.5f;
            float pickerH = dRT.rect.height;
            localPos.x = Mathf.Clamp(localPos.x, -halfCanvasW + halfPickerW, halfCanvasW - halfPickerW);
            if (localPos.y - pickerH < -halfCanvasH)
                localPos.y = -halfCanvasH + pickerH;
            if (localPos.y > halfCanvasH)
                localPos.y = halfCanvasH;
            dRT.anchoredPosition = localPos;
        }

        private void CloseVoiceRarityPicker()
        {
            if (_voiceRarityDropGO != null) { Destroy(_voiceRarityDropGO); _voiceRarityDropGO = null; }
        }

        private void CloseVoicePackPicker()
        {
            CloseVoiceRarityPicker();
            if (_voiceDropGO != null) { Destroy(_voiceDropGO); _voiceDropGO = null; }
        }

        private static string TruncateName(string name, int max)
        {
            if (string.IsNullOrEmpty(name)) return "-";
            if (name.StartsWith("Voice Pack: ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(12);
            if (name.Length > max)
                return name.Substring(0, max - 1) + "…";
            return name;
        }


        private void OnSearchChanged(int team, string query)
        {
            string q = query?.ToLowerInvariant() ?? "";
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                if (c.team != team || c.go == null) continue;
                bool visible = string.IsNullOrEmpty(q) ||
                               (c.skin?.name ?? "").ToLowerInvariant().Contains(q);
                c.go.SetActive(visible);
            }
        }


        private void BuildOptionsModal(Transform canvasRoot)
        {
            _optionsModalGO = new GameObject("OptionsModal");
            _optionsModalGO.transform.SetParent(canvasRoot, false);

            var backdrop = _optionsModalGO.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.72f);
            Stretch(backdrop.rectTransform);
            var backdropBtn = _optionsModalGO.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(CloseOptionsModal);

            var pGO = new GameObject("Panel"); pGO.transform.SetParent(_optionsModalGO.transform, false);
            var pRT = pGO.AddComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.30f, 0.20f); pRT.anchorMax = new Vector2(0.70f, 0.80f);
            pRT.offsetMin = pRT.offsetMax = Vector2.zero;
            pGO.AddComponent<Image>().color = C_MODAL_BG;
            pGO.AddComponent<Button>().transition = Selectable.Transition.None;

            var titleBar = Img(pGO.transform, "TBar", new Color(0.07f, 0.07f, 0.14f, 1f));
            titleBar.img.raycastTarget = false;
            PinTop(titleBar.rt, 44f);
            var titleLbl = Lbl(titleBar.tf, "⚙  OPTIONS", 17, FontStyle.Bold, C_WHITE);
            titleLbl.alignment = TextAnchor.MiddleCenter;

            var tSep = Img(pGO.transform, "TSep", C_SEP); tSep.img.raycastTarget = false;
            SetAnchOff(tSep.rt, 0f, 1f, 1f, 1f, 0f, -44f, 0f, -43f);

            var listGO = new GameObject("List"); listGO.transform.SetParent(pGO.transform, false);
            var listRT = listGO.AddComponent<RectTransform>();
            listRT.anchorMin = Vector2.zero; listRT.anchorMax = Vector2.one;
            listRT.offsetMin = new Vector2(16f, 56f); listRT.offsetMax = new Vector2(-16f, -50f);
            var vlg = listGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            BuildOptionRow(listGO.transform,
                "Don't randomize player",
                "Player always uses default skin, AI bots are randomized.",
                SkinPool.DontRandomizePlayer,
                out _optCheck_DontRandomize, out _optLbl_DontRandomize,
                () => {
                    SkinPool.DontRandomizePlayer = !SkinPool.DontRandomizePlayer;
                    _optCheck_DontRandomize.enabled = SkinPool.DontRandomizePlayer;
                    _optLbl_DontRandomize.color     = SkinPool.DontRandomizePlayer ? C_WHITE : C_DIM;
                });

            BuildOptionRow(listGO.transform,
                "Preserve skin after exit",
                "Actors keep their vehicle skin when they leave the vehicle.",
                SkinPool.PreserveSkinAfterExit,
                out _optCheck_Preserve, out _optLbl_Preserve,
                () => {
                    SkinPool.PreserveSkinAfterExit = !SkinPool.PreserveSkinAfterExit;
                    _optCheck_Preserve.enabled = SkinPool.PreserveSkinAfterExit;
                    _optLbl_Preserve.color     = SkinPool.PreserveSkinAfterExit ? C_WHITE : C_DIM;
                });

            BuildOptionRow(listGO.transform,
                "Vehicle skins affect player",
                "Role-specific skins are also applied to the human player.",
                SkinPool.VehicleSkinsAffectPlayer,
                out _optCheck_VehPlayer, out _optLbl_VehPlayer,
                () => {
                    SkinPool.VehicleSkinsAffectPlayer = !SkinPool.VehicleSkinsAffectPlayer;
                    _optCheck_VehPlayer.enabled = SkinPool.VehicleSkinsAffectPlayer;
                    _optLbl_VehPlayer.color     = SkinPool.VehicleSkinsAffectPlayer ? C_WHITE : C_DIM;
                });

            BuildOptionRow(listGO.transform,
                "Keep vehicle skin on death",
                "Actors respawn wearing their vehicle skin instead of a random infantry skin.",
                SkinPool.KeepVehicleSkinOnDeath,
                out _optCheck_KeepVehicle, out _optLbl_KeepVehicle,
                () => {
                    SkinPool.KeepVehicleSkinOnDeath = !SkinPool.KeepVehicleSkinOnDeath;
                    _optCheck_KeepVehicle.enabled = SkinPool.KeepVehicleSkinOnDeath;
                    _optLbl_KeepVehicle.color     = SkinPool.KeepVehicleSkinOnDeath ? C_WHITE : C_DIM;
                });

            Btn(pGO.transform, "DONE", C_CONT, 14, FontStyle.Bold, C_WHITE,
                new Vector2(0.25f, 0f), new Vector2(0.75f, 0f),
                new Vector2(0f, 10f), new Vector2(0f, 46f))
                .onClick.AddListener(CloseOptionsModal);

            _optionsModalGO.SetActive(false);
        }

        private void BuildOptionRow(Transform parent, string label, string description,
            bool initialOn, out Image checkImg, out Text labelTxt,
            UnityEngine.Events.UnityAction onClick)
        {
            var rowGO = new GameObject("Opt_" + label);
            rowGO.transform.SetParent(parent, false);
            var le = rowGO.AddComponent<LayoutElement>(); le.preferredHeight = 54f;
            var rowBg = rowGO.AddComponent<Image>();
            rowBg.color = initialOn
                ? new Color(0.10f, 0.14f, 0.24f, 1f)
                : new Color(0.07f, 0.07f, 0.12f, 1f);

            var cbGO = new GameObject("CB"); cbGO.transform.SetParent(rowGO.transform, false);
            var cbRT = cbGO.AddComponent<RectTransform>();
            cbRT.anchorMin = new Vector2(0f, 0.5f); cbRT.anchorMax = new Vector2(0f, 0.5f);
            cbRT.pivot = new Vector2(0f, 0.5f);
            cbRT.anchoredPosition = new Vector2(12f, 0f);
            cbRT.sizeDelta = new Vector2(22f, 22f);
            cbGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.16f, 1f);

            var ckGO  = new GameObject("CK"); ckGO.transform.SetParent(cbGO.transform, false);
            var ckRT  = ckGO.AddComponent<RectTransform>();
            ckRT.anchorMin = new Vector2(0.15f, 0.15f); ckRT.anchorMax = new Vector2(0.85f, 0.85f);
            ckRT.offsetMin = ckRT.offsetMax = Vector2.zero;
            checkImg = ckGO.AddComponent<Image>(); checkImg.color = C_CHECK; checkImg.enabled = initialOn;

            labelTxt = Lbl(rowGO.transform, label, 13, FontStyle.Bold,
                initialOn ? C_WHITE : C_DIM);
            labelTxt.alignment = TextAnchor.UpperLeft;
            var lRT = labelTxt.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = new Vector2(44f, 20f); lRT.offsetMax = new Vector2(-10f, 0f);

            var descTxt = Lbl(rowGO.transform, description, 9, FontStyle.Italic, C_DIM);
            descTxt.alignment = TextAnchor.LowerLeft;
            var dRT = descTxt.GetComponent<RectTransform>();
            dRT.anchorMin = Vector2.zero; dRT.anchorMax = Vector2.one;
            dRT.offsetMin = new Vector2(44f, 4f); dRT.offsetMax = new Vector2(-10f, -26f);

            var btn = rowGO.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = rowBg;
            var capBg = rowBg; var capCheck = checkImg;
            btn.onClick.AddListener(() => {
                onClick.Invoke();
                capBg.color = capCheck.enabled
                    ? new Color(0.10f, 0.14f, 0.24f, 1f)
                    : new Color(0.07f, 0.07f, 0.12f, 1f);
            });
        }

        private void ShowOptionsModal()
        {
            CloseAllPopups();
            if (_optionsModalGO != null) _optionsModalGO.SetActive(true);
        }

        private void CloseOptionsModal()
        {
            if (_optionsModalGO != null) _optionsModalGO.SetActive(false);
        }

        private void SyncOptionsModal()
        {
            SetOptCheck(_optCheck_DontRandomize, _optLbl_DontRandomize, SkinPool.DontRandomizePlayer);
            SetOptCheck(_optCheck_Preserve,      _optLbl_Preserve,      SkinPool.PreserveSkinAfterExit);
            SetOptCheck(_optCheck_VehPlayer,      _optLbl_VehPlayer,     SkinPool.VehicleSkinsAffectPlayer);
            SetOptCheck(_optCheck_KeepVehicle,    _optLbl_KeepVehicle,   SkinPool.KeepVehicleSkinOnDeath);
        }

        private void SetOptCheck(Image check, Text lbl, bool on)
        {
            if (check != null) check.enabled = on;
            if (lbl   != null) lbl.color = on ? C_WHITE : C_DIM;
        }



        private void BuildConfigBar(Transform bar, Transform canvasRoot)
        {
            var saveS = Img(bar, "SaveS", Color.clear); saveS.img.raycastTarget = false;
            SetAnchOff(saveS.rt, 0f, 0f, 0.5f, 1f, marginR: -4f);

            LblFixed(saveS.tf, "SAVE AS:", 11, FontStyle.Bold, C_DIM,
                     new Vector2(PAD, 0f), new Vector2(86f, 0f));

            var inputGO = new GameObject("Input"); inputGO.transform.SetParent(saveS.tf, false);
            var inRT = inputGO.AddComponent<RectTransform>();
            inRT.anchorMin = new Vector2(0f, 0.12f); inRT.anchorMax = new Vector2(1f, 0.88f);
            inRT.offsetMin = new Vector2(88f, 0f); inRT.offsetMax = new Vector2(-72f, 0f);
            inputGO.AddComponent<Image>().color = C_INPUT_BG;
            _nameField = inputGO.AddComponent<InputField>(); _nameField.characterLimit = 40;
            _nameField.placeholder = CfgTxt(new GameObject("PH"), inputGO.transform, "Config name...", true);
            _nameField.textComponent = CfgTxt(new GameObject("TXT"), inputGO.transform, "", false);

            Btn(saveS.tf, "SAVE", C_SAVE, 12, FontStyle.Bold, C_WHITE,
                new Vector2(1f, 0.12f), new Vector2(1f, 0.88f),
                new Vector2(-70f, 0f), new Vector2(-PAD, 0f))
                .onClick.AddListener(OnSave);

            var loadS = Img(bar, "LoadS", Color.clear); loadS.img.raycastTarget = false;
            SetAnchOff(loadS.rt, 0.5f, 0f, 1f, 1f, marginL: 4f);

            LblFixed(loadS.tf, "PRESET:", 11, FontStyle.Bold, C_DIM,
                     new Vector2(PAD, 0f), new Vector2(72f, 0f));

            var ddBtn = Btn(loadS.tf, "", C_DDR_BG, 12, FontStyle.Normal, C_WHITE,
                            new Vector2(0f, 0.10f), new Vector2(1f, 0.90f),
                            new Vector2(74f, 0f), new Vector2(-144f, 0f));
            ddBtn.onClick.AddListener(ToggleDropdown);
            _dropdownLabel = Lbl(ddBtn.targetGraphic.transform, "- no preset -", 12, FontStyle.Italic, C_DIM);
            _dropdownLabel.alignment = TextAnchor.MiddleLeft;
            SetAnchOff(_dropdownLabel.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 8f, 0f, -24f, 0f);
            var arrow = Lbl(ddBtn.targetGraphic.transform, "▼", 11, FontStyle.Normal, C_DIM);
            SetAnchOff(arrow.GetComponent<RectTransform>(), 1f, 0f, 1f, 1f, -22f, 0f, -4f, 0f);

            Btn(loadS.tf, "LOAD", C_LOAD, 12, FontStyle.Bold, C_WHITE,
                new Vector2(1f, 0.10f), new Vector2(1f, 0.90f),
                new Vector2(-140f, 0f), new Vector2(-76f, 0f))
                .onClick.AddListener(OnLoad);

            Btn(loadS.tf, "DEL", C_DEL, 12, FontStyle.Bold, C_WHITE,
                new Vector2(1f, 0.10f), new Vector2(1f, 0.90f),
                new Vector2(-72f, 0f), new Vector2(-PAD, 0f))
                .onClick.AddListener(OnDeleteRequest);

            _dropdownListGO = new GameObject("DDR_List");
            _dropdownListGO.transform.SetParent(canvasRoot, false);
            var ddRT = _dropdownListGO.AddComponent<RectTransform>();
            ddRT.anchorMin = new Vector2(0.54f, 1f);
            ddRT.anchorMax = new Vector2(0.86f, 1f);
            ddRT.pivot     = new Vector2(0.5f, 1f);
            ddRT.anchoredPosition = new Vector2(0f, -(H_HEADER + H_CONFIG));
            ddRT.sizeDelta = new Vector2(0f, 0f);
            _dropdownListGO.AddComponent<Image>().color = C_DDR_BG;
            var dvlg = _dropdownListGO.AddComponent<VerticalLayoutGroup>();
            dvlg.padding = new RectOffset(4, 4, 4, 4); dvlg.childForceExpandWidth = true;
            dvlg.childForceExpandHeight = false; dvlg.spacing = 3f;
            _dropdownListGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _dropdownListGO.SetActive(false);
            RefreshPresetList();

            BuildConfirmModal(canvasRoot);
        }


        private void RefreshPresetList()
        {
            if (_dropdownListGO == null) return;
            foreach (Transform ch in _dropdownListGO.transform) Destroy(ch.gameObject);
            var names = SkinConfigManager.GetSavedConfigNames();
            if (names.Length == 0)
            {
                var e = new GameObject("E"); e.transform.SetParent(_dropdownListGO.transform, false);
                e.AddComponent<LayoutElement>().preferredHeight = 32f;
                var t = e.AddComponent<Text>(); t.text = "No saved presets"; t.font = Fnt();
                t.fontSize = 12; t.fontStyle = FontStyle.Italic; t.color = C_DIM;
                t.alignment = TextAnchor.MiddleCenter; return;
            }
            foreach (var n in names)
            {
                var iGO = new GameObject("DD_" + n); iGO.transform.SetParent(_dropdownListGO.transform, false);
                iGO.AddComponent<LayoutElement>().preferredHeight = 34f;
                var iBg = iGO.AddComponent<Image>(); iBg.color = C_DDR_ITEM;
                var iBtn = iGO.AddComponent<Button>();
                var col = iBtn.colors; col.normalColor = Color.white;
                col.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
                col.pressedColor = new Color(0.82f, 0.82f, 0.82f); iBtn.colors = col;
                iBtn.targetGraphic = iBg;
                var lbl = Lbl(iGO.transform, n, 12, FontStyle.Normal, C_WHITE);
                lbl.alignment = TextAnchor.MiddleLeft;
                SetAnchOff(lbl.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 10f, 0f, 0f, 0f);
                var cap = n; iBtn.onClick.AddListener(() => SelectPreset(cap));
            }
        }

        private void ToggleDropdown()
        {
            _dropdownOpen = !_dropdownOpen;
            if (_dropdownListGO != null) { RefreshPresetList(); _dropdownListGO.SetActive(_dropdownOpen); }
        }

        private void CloseDropdown()
        {
            _dropdownOpen = false;
            if (_dropdownListGO != null) _dropdownListGO.SetActive(false);
        }

        private void CloseAllPopups() { CloseDropdown(); CloseRarityPicker(); CloseVoicePackPicker(); CloseOptionsModal(); }


        private void SelectPreset(string name)
        {
            _selectedConfig = name;
            if (_dropdownLabel != null) { _dropdownLabel.text = name; _dropdownLabel.fontStyle = FontStyle.Normal; _dropdownLabel.color = C_WHITE; }
            if (_nameField != null) _nameField.text = name;
            CloseDropdown();
        }


        private void BuildConfirmModal(Transform canvasRoot)
        {
            _modalGO = new GameObject("ConfirmModal"); _modalGO.transform.SetParent(canvasRoot, false);
            var m = _modalGO.AddComponent<Image>(); m.color = new Color(0f, 0f, 0f, 0.72f); Stretch(m.rectTransform);
            _modalGO.AddComponent<Button>().transition = Selectable.Transition.None;

            var pGO = new GameObject("Panel"); pGO.transform.SetParent(_modalGO.transform, false);
            var pRT = pGO.AddComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.35f, 0.40f); pRT.anchorMax = new Vector2(0.65f, 0.60f);
            pRT.offsetMin = pRT.offsetMax = Vector2.zero;
            pGO.AddComponent<Image>().color = C_MODAL_BG;

            var title = Lbl(pGO.transform, "Delete this preset?", 16, FontStyle.Bold, C_WHITE);
            SetAnchOff(title.GetComponent<RectTransform>(), 0f, 0.55f, 1f, 1f, 16f, 0f, -16f, -8f);

            Btn(pGO.transform, "DELETE", C_DEL, 14, FontStyle.Bold, C_WHITE,
                new Vector2(0.54f, 0.08f), new Vector2(0.96f, 0.46f), Vector2.zero, Vector2.zero)
                .onClick.AddListener(OnDeleteConfirm);
            Btn(pGO.transform, "CANCEL", new Color(0.2f, 0.2f, 0.3f, 1f), 14, FontStyle.Bold, C_WHITE,
                new Vector2(0.04f, 0.08f), new Vector2(0.46f, 0.46f), Vector2.zero, Vector2.zero)
                .onClick.AddListener(OnDeleteCancel);

            _modalGO.SetActive(false);
        }


        private void BuildCancelModal(Transform canvasRoot)
        {
            _cancelModalGO = new GameObject("CancelModal"); _cancelModalGO.transform.SetParent(canvasRoot, false);
            var m = _cancelModalGO.AddComponent<Image>(); m.color = new Color(0f, 0f, 0f, 0.72f); Stretch(m.rectTransform);
            _cancelModalGO.AddComponent<Button>().transition = Selectable.Transition.None;

            var pGO = new GameObject("Panel"); pGO.transform.SetParent(_cancelModalGO.transform, false);
            var pRT = pGO.AddComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.30f, 0.35f); pRT.anchorMax = new Vector2(0.70f, 0.65f);
            pRT.offsetMin = pRT.offsetMax = Vector2.zero;
            pGO.AddComponent<Image>().color = C_MODAL_BG;

            var title = Lbl(pGO.transform, "Cancel skin selection?", 18, FontStyle.Bold, C_WHITE);
            SetAnchOff(title.GetComponent<RectTransform>(), 0f, 0.62f, 1f, 1f, 16f, 0f, -16f, -8f);

            var desc = Lbl(pGO.transform,
                "Return to match screen?\nUnsaved presets will be lost.",
                12, FontStyle.Normal, C_DIM);
            desc.alignment = TextAnchor.MiddleCenter;
            SetAnchOff(desc.GetComponent<RectTransform>(), 0.05f, 0.34f, 0.95f, 0.60f);

            Btn(pGO.transform, "YES, EXIT", C_DEL, 14, FontStyle.Bold, C_WHITE,
                new Vector2(0.54f, 0.06f), new Vector2(0.96f, 0.30f), Vector2.zero, Vector2.zero)
                .onClick.AddListener(OnCancelConfirm);
            Btn(pGO.transform, "GO BACK", new Color(0.2f, 0.2f, 0.3f, 1f), 14, FontStyle.Bold, C_WHITE,
                new Vector2(0.04f, 0.06f), new Vector2(0.46f, 0.30f), Vector2.zero, Vector2.zero)
                .onClick.AddListener(OnCancelCancel);

            _cancelModalGO.SetActive(false);
        }

        private void OnCancelRequest()
        {
            CloseAllPopups();
            if (_cancelModalGO != null) _cancelModalGO.SetActive(true);
        }

        private void OnCancelConfirm()
        {
            if (_cancelModalGO != null) _cancelModalGO.SetActive(false);
            SkinPool.ClearAll();
            IsOpen = false;
            if (_root != null) Destroy(_root); _root = null;
            _onContinue = null;
        }

        private void OnCancelCancel()
        {
            if (_cancelModalGO != null) _cancelModalGO.SetActive(false);
        }


        private void OnClearAll()
        {
            CloseAllPopups();
            SkinPool.ClearAll();
            foreach (var c in _cells) { c.outline.enabled = false; c.bg.color = C_CELL; }
            RefreshSidebar(0); RefreshSidebar(1);
            SetStatus("Cleared all.");
        }

        private void OnSave()
        {
            CloseAllPopups();
            string name = _nameField?.text?.Trim();
            if (string.IsNullOrEmpty(name)) { SetStatus("Enter a name first."); return; }
            if (SkinConfigManager.Save(name, out string err))
            { SelectPreset(name); RefreshPresetList(); SetStatus($"Saved \"{name}\"."); }
            else SetStatus("Save failed: " + err);
        }

        private void OnLoad()
        {
            CloseAllPopups();
            if (string.IsNullOrEmpty(_selectedConfig)) { SetStatus("Select a preset first."); return; }
            SkinConfigManager.Load(_selectedConfig, out string msg);
            RefreshAllCellVisuals(); RefreshSidebar(0); RefreshSidebar(1);
            SyncOptionsModal();
            SetStatus(msg);
        }

        private void OnDeleteRequest()
        {
            CloseAllPopups();
            if (string.IsNullOrEmpty(_selectedConfig)) { SetStatus("Select a preset to delete."); return; }
            _pendingDeleteName = _selectedConfig;
            if (_modalGO != null) _modalGO.SetActive(true);
        }

        private void OnDeleteConfirm()
        {
            if (_modalGO != null) _modalGO.SetActive(false);
            SkinConfigManager.Delete(_pendingDeleteName);
            SetStatus($"Deleted \"{_pendingDeleteName}\".");
            if (_selectedConfig == _pendingDeleteName)
            { _selectedConfig = ""; if (_dropdownLabel != null) { _dropdownLabel.text = "- no preset -"; _dropdownLabel.fontStyle = FontStyle.Italic; _dropdownLabel.color = C_DIM; } }
            RefreshPresetList(); _pendingDeleteName = "";
        }

        private void OnDeleteCancel()
        { if (_modalGO != null) _modalGO.SetActive(false); _pendingDeleteName = ""; }

        private void OnContinue()
        {
            IsOpen = false;
            if (_root != null) Destroy(_root); _root = null;
            var cb = _onContinue; _onContinue = null; cb?.Invoke();
        }

        private void RefreshAllCellVisuals()
        {
            foreach (var c in _cells)
            { bool s = SkinPool.IsSelected(c.team, c.skin); c.outline.enabled = s; c.bg.color = s ? C_CELL_SEL : C_CELL; }
        }

        private void SetStatus(string m) { if (_statusText != null) _statusText.text = m; }


        private struct IS { public GameObject go; public Transform tf; public RectTransform rt; public Image img; }

        private static IS Img(Transform parent, string name, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>(); img.color = color;
            return new IS { go = go, tf = go.transform, rt = rt, img = img };
        }

        private static void Stretch(RectTransform rt)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        private static void SetAnchOff(RectTransform rt,
            float aX0, float aY0, float aX1, float aY1,
            float marginL = 0f, float marginB = 0f, float marginR = 0f, float marginT = 0f)
        { rt.anchorMin = new Vector2(aX0, aY0); rt.anchorMax = new Vector2(aX1, aY1); rt.offsetMin = new Vector2(marginL, marginB); rt.offsetMax = new Vector2(marginR, marginT); }

        private static void PinTop(RectTransform rt, float h)
        { rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.offsetMin = new Vector2(0f, -h); rt.offsetMax = Vector2.zero; }

        private static void PinBottom(RectTransform rt, float h)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, 0f); rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, h); }

        private static Text Lbl(Transform parent, string text, int size, FontStyle style, Color color)
        {
            var go = new GameObject("L"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); Stretch(rt);
            var t = go.AddComponent<Text>(); t.text = text; t.font = Fnt();
            t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            return t;
        }

        private static void LblFixed(Transform parent, string text, int size, FontStyle style,
            Color color, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject("L"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            var t = go.AddComponent<Text>(); t.text = text; t.font = Fnt();
            t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
        }

        private static Text CfgTxt(GameObject go, Transform parent, string text, bool ph)
        {
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); Stretch(rt);
            rt.offsetMin = new Vector2(6f, 2f); rt.offsetMax = new Vector2(-6f, -2f);
            var t = go.AddComponent<Text>(); t.text = text; t.font = Fnt(); t.fontSize = 13;
            t.fontStyle = ph ? FontStyle.Italic : FontStyle.Normal;
            t.color = ph ? C_DIM : C_WHITE; t.supportRichText = false;
            return t;
        }

        private static Button Btn(Transform parent, string label, Color bg, int fs,
            FontStyle fst, Color tc, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject("B_" + label); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var bgImg = go.AddComponent<Image>(); bgImg.color = bg;
            var btn = go.AddComponent<Button>();
            var col = btn.colors; col.normalColor = Color.white;
            col.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            col.pressedColor = new Color(0.8f, 0.8f, 0.8f); btn.colors = col;
            btn.targetGraphic = bgImg;
            if (!string.IsNullOrEmpty(label)) Lbl(go.transform, label, fs, fst, tc);
            return btn;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static Font Fnt()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return f;
        }
    }
}