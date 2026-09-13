using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 大厅 UI 自动化搭建工具 —— 菜单一键把「玩家档案」缺失的 UI 元素创建出来并自动接好全部引用。
/// 菜单：Tools > Chess > 搭建大厅 UI
///
/// 行为约定：
///   1. 幂等：字段已接引用 → 跳过；同名对象已存在 → 复用并补组件，不重建、不覆盖；
///   2. 只创建缺失元素：UI_MainMenu 现有配置区（棋盘/棋子数/城邦排除）与 StartButton/ExitButton 一律不动；
///   3. 全部通过 Unity API 创建（不手写场景 YAML）；ShopItem 预制体只被引用，不修改；
///   4. 不引入美术素材；配色沿用大厅深色 + 亮蓝强调；文本字体复用场景现有 TMP 字体资产；
///   5. 执行完标记场景 dirty 并弹出保存提示（不静默保存）。
///
/// 接线对象（与各脚本 [SerializeField] 字段一一对应）：
///   UI_MainMenu: accountText / goldText / switchAccountButton / shopButton / loginPanel / shopPanel / registry
///   UI_LoginPanel: panelRoot / accountInput / passwordInput / loginButton / registerButton / closeButton / messageText
///   UI_PieceShopPanel: panelRoot / contentContainer / itemPrefab / goldText / messageText / closeButton
/// </summary>
public static class LobbyUI_SetupTool
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string MenuPath = "Tools/Chess/搭建大厅 UI";

    // ---- 配色（与 UI_MainMenu 现有风格一致：深色面板 + 亮蓝强调）----
    private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.20f, 0.97f);
    private static readonly Color InputColor = new Color(0.18f, 0.21f, 0.28f, 1f);
    private static readonly Color InputHintColor = new Color(0.60f, 0.62f, 0.68f, 1f);
    private static readonly Color ButtonNormalColor = new Color(0.22f, 0.25f, 0.32f, 1f);
    private static readonly Color ButtonHighlightColor = new Color(0.35f, 0.55f, 0.90f, 1f);
    private static readonly Color ButtonPressedColor = new Color(0.28f, 0.42f, 0.72f, 1f);
    private static readonly Color AccentTextColor = new Color(0.72f, 0.85f, 1.00f, 1f);

    private static TMP_FontAsset _font;

    // ================= 入口 =================
    [MenuItem(MenuPath)]
    public static void BuildLobbyUI()
    {
        // 1) 场景：确保打开 MainMenu（未打开时打开；提示当前场景未保存修改可能被丢弃）
        var active = EditorSceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            if (active.isDirty)
                Debug.LogWarning("[LobbyUI_SetupTool] 当前场景有未保存修改，将打开 MainMenu 场景（未保存修改将被丢弃）。");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var scene = EditorSceneManager.GetActiveScene();

        // 2) 前置：Canvas / EventSystem / TMP 字体
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LobbyUI_SetupTool] 场景中找不到 Canvas，中止。请先搭建 Canvas。");
            return;
        }
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "搭建大厅 UI");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("[LobbyUI_SetupTool] 已创建缺失的 EventSystem（按钮/输入框需要）。");
        }
        _font = SampleFont();

        var mainMenu = Object.FindFirstObjectByType<UI_MainMenu>();
        if (mainMenu == null)
        {
            Debug.LogError("[LobbyUI_SetupTool] 场景中找不到 UI_MainMenu 组件，中止。");
            return;
        }

        int created = 0;

        // 3) UI_MainMenu 直接字段（文本 / 按钮 / 注册表）
        created += EnsureMainMenuFields(mainMenu, canvas.transform);

        // 4) 登录面板
        created += EnsureLoginPanel(mainMenu, canvas.transform);

        // 5) 棋子商店面板
        created += EnsureShopPanel(mainMenu, canvas.transform);

        // 6) 收尾：标记 dirty + 提示保存（不静默保存）
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[LobbyUI_SetupTool] 搭建完成：新建/补建 {created} 个对象。请检查后保存场景（Ctrl+S）。");
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    // ================= UI_MainMenu 字段 =================
    private static int EnsureMainMenuFields(UI_MainMenu mainMenu, Transform canvas)
    {
        int created = 0;

        // 账号 / 金币文本（左上角两行）
        EnsureField(mainMenu, "accountText", "AccountText", canvas,
            go => { created++; return CreateTMP(go, "账号：test", 18, Color.white, TextAlignmentOptions.Left,
                new RectSpec(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(280, 32))); });
        EnsureField(mainMenu, "goldText", "GoldText", canvas,
            go => { created++; return CreateTMP(go, "金币：0", 18, Color.white, TextAlignmentOptions.Left,
                new RectSpec(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -52), new Vector2(280, 32))); });

        // 「切换账号」/「商店」按钮（右上角一行）
        var switchBtn = EnsureField(mainMenu, "switchAccountButton", "SwitchAccountButton", canvas,
            go => { created++; return CreateButton(go, "切换账号"); });
        var shopBtn = EnsureField(mainMenu, "shopButton", "ShopButton", canvas,
            go => { created++; return CreateButton(go, "商店"); });
        ApplyRect(switchBtn.GetComponent<RectTransform>(), new RectSpec(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -14), new Vector2(124, 36)));
        ApplyRect(shopBtn.GetComponent<RectTransform>(), new RectSpec(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-272, -14), new Vector2(124, 36)));

        // 棋池解析源：自动查找 PieceRegistry 资产（必须接上）
        // 注：registry 是 private 字段，跨程序集（Editor）只能经 SerializedObject 读写，不能直接 mainMenu.registry
        var regSp = new SerializedObject(mainMenu).FindProperty("registry");
        if (regSp == null || regSp.objectReferenceValue == null)
        {
            var reg = FindRegistry();
            if (reg != null)
            {
                SetProp(mainMenu, "registry", reg);
                Debug.Log($"[LobbyUI_SetupTool] registry 已接：{AssetDatabase.GetAssetPath(reg)}");
            }
            else
            {
                Debug.LogWarning("[LobbyUI_SetupTool] 未找到 PieceRegistry 资产，registry 未接线（大厅棋池/商店会回退局内 PieceManager 或为空，请手动拖入）。");
            }
        }
        return created;
    }

    // ================= 登录面板 =================
    private static int EnsureLoginPanel(UI_MainMenu mainMenu, Transform canvas)
    {
        var panelObj = ResolvePanel(mainMenu, "loginPanel", "LoginPanel", canvas, out bool createdPanel);
        int created = createdPanel ? 1 : 0;
        if (panelObj == null) return created;

        var panel = panelObj.GetComponent<UI_LoginPanel>();
        if (panel == null) panel = panelObj.AddComponent<UI_LoginPanel>();
        if (createdPanel) panelObj.SetActive(false); // 默认关闭，由大厅「切换账号」按钮打开

        // 背板（根 Image 在子元素之下绘制）
        if (panelObj.GetComponent<Image>() == null)
        {
            var bg = panelObj.AddComponent<Image>();
            bg.color = PanelColor;
        }

        SetPropIfEmpty(panel, "panelRoot", panelObj);

        // 标题（纯展示，无字段）
        CreateTitle(panelObj.transform, "LoginTitleText", "账号登录");

        // 账号 / 密码输入框（顶部通栏拉伸）
        EnsureChild(panel, "accountInput", "AccountInput",
            go => { created++; return CreateInputField(go, "请输入账号",
                new RectSpec(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero),
                new OffsetSpec(16, -96, -16, -56)); });
        EnsureChild(panel, "passwordInput", "PasswordInput",
            go => { created++; return CreateInputField(go, "请输入密码",
                new RectSpec(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero),
                new OffsetSpec(16, -144, -16, -104)); });

        // 提示文本（底部按钮上方）
        EnsureChild(panel, "messageText", "LoginMessageText",
            go => { created++; return CreateTMP(go, string.Empty, 15, Color.white, TextAlignmentOptions.Center,
                new RectSpec(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero),
                new OffsetSpec(16, 60, -16, 92)); });

        // 登录 / 注册按钮（底部两角）+ 关闭按钮（右上角）
        EnsureChild(panel, "loginButton", "LoginButton",
            go => { created++; return CreateButton(go, "登录",
                new RectSpec(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(180, 38))); });
        EnsureChild(panel, "registerButton", "RegisterButton",
            go => { created++; return CreateButton(go, "注册",
                new RectSpec(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(180, 38))); });
        EnsureChild(panel, "closeButton", "LoginCloseButton",
            go => { created++; return CreateButton(go, "关闭",
                new RectSpec(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -10), new Vector2(64, 28))); });

        Debug.Log($"[LobbyUI_SetupTool] 登录面板就绪（{panelObj.name}）");
        return created;
    }

    // ================= 棋子商店面板 =================
    private static int EnsureShopPanel(UI_MainMenu mainMenu, Transform canvas)
    {
        var panelObj = ResolvePanel(mainMenu, "shopPanel", "PieceShopPanel", canvas, out bool createdPanel);
        int created = createdPanel ? 1 : 0;
        if (panelObj == null) return created;

        var panel = panelObj.GetComponent<UI_PieceShopPanel>();
        if (panel == null) panel = panelObj.AddComponent<UI_PieceShopPanel>();
        if (createdPanel) panelObj.SetActive(false); // 默认关闭，由大厅「商店」按钮打开

        if (panelObj.GetComponent<Image>() == null)
        {
            var bg = panelObj.AddComponent<Image>();
            bg.color = PanelColor;
        }

        SetPropIfEmpty(panel, "panelRoot", panelObj);

        // 标题（纯展示）
        CreateTitle(panelObj.transform, "ShopTitleText", "棋子商店");

        // 金币文本
        EnsureChild(panel, "goldText", "ShopGoldText",
            go => { created++; return CreateTMP(go, "金币：0", 18, Color.white, TextAlignmentOptions.Left,
                new RectSpec(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -50), new Vector2(300, 28))); });

        // 滚动列表（中部）：contentContainer 指向其 Content
        var content = EnsureShopScrollView(panelObj.transform, out bool scrollCreated);
        if (scrollCreated) created++;
        SetPropIfEmpty(panel, "contentContainer", content);

        // 提示文本（底部）
        EnsureChild(panel, "messageText", "ShopMessageText",
            go => { created++; return CreateTMP(go, string.Empty, 15, Color.white, TextAlignmentOptions.Center,
                new RectSpec(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero),
                new OffsetSpec(16, 20, -16, 52)); });

        // 关闭按钮（右上角）
        EnsureChild(panel, "closeButton", "ShopCloseButton",
            go => { created++; return CreateButton(go, "关闭",
                new RectSpec(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -10), new Vector2(64, 28))); });

        // itemPrefab → 现有 ShopItem 预制体（只引用，不改）
        // 注：itemPrefab 是 private 字段，跨程序集（Editor）只能经 SerializedObject 读写，不能直接 panel.itemPrefab
        var itemSp = new SerializedObject(panel).FindProperty("itemPrefab");
        if (itemSp == null || itemSp.objectReferenceValue == null)
        {
            var prefab = FindShopItemPrefab();
            if (prefab != null)
            {
                SetProp(panel, "itemPrefab", prefab);
                Debug.Log($"[LobbyUI_SetupTool] itemPrefab 已接：{AssetDatabase.GetAssetPath(prefab)}");
            }
            else
            {
                Debug.LogWarning("[LobbyUI_SetupTool] 未找到带 UI_ShopItemRefs 的预制体，itemPrefab 未接线（商店列表无法生成，请手动拖入 ShopItemRefs.prefab）。");
            }
        }

        Debug.Log($"[LobbyUI_SetupTool] 商店面板就绪（{panelObj.name}）");
        return created;
    }

    // ================= 滚动列表 =================
    /// <summary>幂等构建 ShopScrollView → Viewport → Content（VerticalLayoutGroup + ContentSizeFitter），返回 Content。</summary>
    private static RectTransform EnsureShopScrollView(Transform parent, out bool created)
    {
        created = false;
        var root = FindChild(parent, "ShopScrollView");
        if (root == null)
        {
            var go = new GameObject("ShopScrollView", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "搭建大厅 UI");
            go.transform.SetParent(parent, false);
            created = true;
            root = go.transform;
        }

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.offsetMin = new Vector2(16, 56);
        rect.offsetMax = new Vector2(-16, -88);

        var scroll = root.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.16f, 1f);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
        }

        // Viewport（裁剪 + 射线）
        var viewport = FindChild(root, "Viewport");
        if (viewport == null)
        {
            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(vpGo, "搭建大厅 UI");
            vpGo.transform.SetParent(root, false);
            viewport = vpGo.transform;
        }
        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
        if (viewport.GetComponent<Image>() == null)
        {
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(1, 1, 1, 0.02f);
        }

        // Content（列表容器：纵向布局 + 自适应高度）
        var content = FindChild(viewport, "Content");
        if (content == null)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(contentGo, "搭建大厅 UI");
            contentGo.transform.SetParent(viewport, false);
            content = contentGo.transform;
        }
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 200);

        if (content.GetComponent<VerticalLayoutGroup>() == null)
        {
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
        if (content.GetComponent<ContentSizeFitter>() == null)
        {
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        if (scroll.viewport != vpRect) scroll.viewport = vpRect;
        if (scroll.content != contentRect) scroll.content = contentRect;
        return contentRect;
    }

    // ================= 通用创建 helper =================
    /// <summary>面板根：字段已接 → 复用；同名对象存在 → 复用并挂脚本；否则新建（居中，尺寸按面板名区分）。</summary>
    private static GameObject ResolvePanel(Component host, string prop, string objName, Transform parent, out bool created)
    {
        var so = new SerializedObject(host);
        var sp = so.FindProperty(prop);
        var existing = sp?.objectReferenceValue as GameObject;
        created = false;
        if (existing != null) return existing;

        var child = FindChild(parent, objName);
        if (child != null)
        {
            SetProp(host, prop, child.gameObject);
            return child.gameObject;
        }

        var go = new GameObject(objName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "搭建大厅 UI");
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = objName == "LoginPanel" ? new Vector2(420, 340) : new Vector2(460, 560);
        SetProp(host, prop, go);
        created = true;
        return go;
    }

    /// <summary>确保字段（挂载于 host 上的组件字段）：已接引用 → 返回；同名子对象存在 → 复用（缺组件则 factory 补）；否则新建。</summary>
    private static T EnsureField<T>(Component host, string prop, string objName, Transform parent, System.Func<GameObject, T> factory) where T : Component
    {
        var so = new SerializedObject(host);
        var sp = so.FindProperty(prop);
        var existing = sp?.objectReferenceValue as T;
        if (existing != null) return existing;

        var child = FindChild(parent, objName);
        if (child != null)
        {
            var comp = child.GetComponent<T>();
            if (comp == null) comp = factory(child.gameObject);
            SetProp(host, prop, comp);
            return comp;
        }

        var go = new GameObject(objName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "搭建大厅 UI");
        go.transform.SetParent(parent, false);
        var result = factory(go);
        SetProp(host, prop, result);
        return result;
    }

    /// <summary>确保面板内子字段：已接引用 → 返回；同名子对象存在 → 复用（缺组件则 factory 补）；否则新建。</summary>
    private static T EnsureChild<T>(Component host, string prop, string objName, System.Func<GameObject, T> factory) where T : Component
    {
        var parent = (host as MonoBehaviour).transform;
        var so = new SerializedObject(host);
        var sp = so.FindProperty(prop);
        var existing = sp?.objectReferenceValue as T;
        if (existing != null) return existing;

        var child = FindChild(parent, objName);
        if (child != null)
        {
            var comp = child.GetComponent<T>();
            if (comp == null) comp = factory(child.gameObject);
            SetProp(host, prop, comp);
            return comp;
        }

        var go = new GameObject(objName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "搭建大厅 UI");
        go.transform.SetParent(parent, false);
        var result = factory(go);
        SetProp(host, prop, result);
        return result;
    }

    // ---- 具体组件工厂 ----
    private static TextMeshProUGUI CreateTMP(GameObject go, string text, int fontSize, Color color, TextAlignmentOptions align,
        RectSpec rect, OffsetSpec? stretchOffsets = null)
    {
        var rt = GetOrAddRect(go);
        ApplyRect(rt, rect, stretchOffsets);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        if (_font != null) tmp.font = _font;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    private static Button CreateButton(GameObject go, string label, RectSpec? rect = null)
    {
        var rt = GetOrAddRect(go);
        if (rect.HasValue) ApplyRect(rt, rect.Value);
        if (go.GetComponent<Image>() == null)
        {
            var img = go.AddComponent<Image>();
            img.color = ButtonNormalColor;
        }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        btn.colors = CreateColorBlock();

        var textGo = new GameObject("Text", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(textGo, "搭建大厅 UI");
        textGo.transform.SetParent(rt, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.color = Color.white;
        if (_font != null) tmp.font = _font;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return btn;
    }

    private static TMP_InputField CreateInputField(GameObject go, string hint, RectSpec rect, OffsetSpec? stretchOffsets = null)
    {
        var rt = GetOrAddRect(go);
        ApplyRect(rt, rect, stretchOffsets);
        if (go.GetComponent<Image>() == null)
        {
            var img = go.AddComponent<Image>();
            img.color = InputColor;
        }
        var input = go.AddComponent<TMP_InputField>();
        input.targetGraphic = go.GetComponent<Image>();

        var area = CreateChildRect("Text Area", rt,
            new RectSpec(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
            new OffsetSpec(10, 5, -10, -5));
        var areaImg = area.gameObject.AddComponent<Image>();
        areaImg.color = new Color(1, 1, 1, 0.02f);

        var placeholder = CreateTMPInChild(area, "Placeholder", hint, 16, InputHintColor);
        var text = CreateTMPInChild(area, "Text", string.Empty, 16, Color.white);

        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private static TextMeshProUGUI CreateTMPInChild(RectTransform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "搭建大厅 UI");
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        if (_font != null) tmp.font = _font;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static RectTransform CreateChildRect(string name, Transform parent, RectSpec rect, OffsetSpec? offsets)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "搭建大厅 UI");
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        ApplyRect(rt, rect, offsets);
        return rt;
    }

    /// <summary>面板标题（纯展示，无字段接线；已存在同名则跳过）</summary>
    private static void CreateTitle(Transform parent, string name, string content)
    {
        if (FindChild(parent, name) != null) return;
        CreateTMP(new GameObject(name, typeof(RectTransform)), content, 22, AccentTextColor, TextAlignmentOptions.Center,
            new RectSpec(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(0, 30)),
            new OffsetSpec(16, -42, -16, -12));
    }

    // ================= Rect 工具 =================
    private struct RectSpec
    {
        public Vector2 anchorMin, anchorMax, pivot, anchoredPos, size;
        public RectSpec(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            this.anchorMin = anchorMin; this.anchorMax = anchorMax; this.pivot = pivot;
            this.anchoredPos = anchoredPos; this.size = size;
        }
    }

    private struct OffsetSpec
    {
        public float left, bottom, right, top;
        public OffsetSpec(float left, float bottom, float right, float top)
        {
            this.left = left; this.bottom = bottom; this.right = right; this.top = top;
        }
    }

    private static void ApplyRect(RectTransform rt, RectSpec spec, OffsetSpec? offsets = null)
    {
        rt.anchorMin = spec.anchorMin;
        rt.anchorMax = spec.anchorMax;
        rt.pivot = spec.pivot;
        if (offsets.HasValue)
        {
            rt.offsetMin = new Vector2(offsets.Value.left, offsets.Value.bottom);
            rt.offsetMax = new Vector2(offsets.Value.right, offsets.Value.top);
        }
        else
        {
            rt.anchoredPosition = spec.anchoredPos;
            rt.sizeDelta = spec.size;
        }
    }

    private static RectTransform GetOrAddRect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        return rt;
    }

    // ================= 查找 / 引用工具 =================
    private static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
            if (child.name == name) return child;
        return null;
    }

    private static bool SetProp(Object target, string prop, Object value)
    {
        if (target == null || value == null) return false;
        var so = new SerializedObject(target);
        var sp = so.FindProperty(prop);
        if (sp == null) return false;
        sp.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void SetPropIfEmpty(Object target, string prop, Object value)
    {
        if (target == null || value == null) return;
        var so = new SerializedObject(target);
        var sp = so.FindProperty(prop);
        if (sp == null || sp.objectReferenceValue != null) return;
        sp.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_FontAsset SampleFont()
    {
        var any = Object.FindFirstObjectByType<TextMeshProUGUI>();
        if (any != null && any.font != null) return any.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static ColorBlock CreateColorBlock()
    {
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor = ButtonNormalColor;
        cb.highlightedColor = ButtonHighlightColor;
        cb.pressedColor = ButtonPressedColor;
        cb.selectedColor = ButtonHighlightColor;
        cb.disabledColor = new Color(0.16f, 0.18f, 0.24f, 0.60f);
        return cb;
    }

    private static PieceRegistry FindRegistry()
    {
        var guids = AssetDatabase.FindAssets("t:PieceRegistry");
        foreach (var g in guids)
        {
            var reg = AssetDatabase.LoadAssetAtPath<PieceRegistry>(AssetDatabase.GUIDToAssetPath(g));
            if (reg != null && reg.name == "PieceRegistry") return reg;
        }
        foreach (var g in guids)
        {
            var reg = AssetDatabase.LoadAssetAtPath<PieceRegistry>(AssetDatabase.GUIDToAssetPath(g));
            if (reg != null) return reg;
        }
        return null;
    }

    private static UI_ShopItemRefs FindShopItemPrefab()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        var candidates = new List<GameObject>();
        foreach (var g in guids)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
            if (prefab != null && prefab.GetComponent<UI_ShopItemRefs>() != null)
                candidates.Add(prefab);
        }
        foreach (var c in candidates) if (c.name == "ShopItemRefs") return c.GetComponent<UI_ShopItemRefs>();
        foreach (var c in candidates) if (c.name == "ShopItem") return c.GetComponent<UI_ShopItemRefs>();
        return candidates.Count > 0 ? candidates[0].GetComponent<UI_ShopItemRefs>() : null;
    }
}
