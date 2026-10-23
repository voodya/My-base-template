#if HAS_VOODYA
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Voodya.Core;
using Object = UnityEngine.Object;
using ScopeInfo = ArchConfigModel.ScopeInfo;
using ServiceInfo = ArchConfigModel.ServiceInfo;

/// <summary>
/// Window for distributing services between application scopes, see ArchConfigModel.
/// All changes are saved immediately and support Undo.
/// </summary>
public class ArchConfigWindow : EditorWindow
{
    private const string DRAG_FROM_KEY = "VooArch.FromScope";
    private const string LEFT_WIDTH_PREF = "VooArch.ConfigWindow.LeftWidth";
    private const string SHOW_ISSUES_PREF = "VooArch.ConfigWindow.ShowIssues";

    private readonly ArchConfigModel _model = new();
    private readonly Dictionary<Object, SerializedObject> _serializedCache = new();
    private readonly Dictionary<Object, bool> _hasFieldsCache = new();
    private readonly List<Action> _deferred = new();

    private string _search = "";
    private float _leftWidth;
    private bool _resizing;
    private bool _showIssues;
    private Vector2 _servicesScroll;
    private Vector2 _scopesScroll;
    private Vector2 _issuesScroll;
    private readonly HashSet<string> _collapsedScopes = new();
    private readonly HashSet<string> _expandedRows = new();
    private string _dropTarget;
    private bool _pressed;
    private ScriptableInstaller _pressedInstaller;
    private ScopeInfo _pressedFrom;

    private List<ScopeInfo> Scopes => _model.Scopes;

    [MenuItem("VooArch/Voo arch configure")]
    public static void ShowWindow()
    {
        var window = GetWindow<ArchConfigWindow>();
        window.titleContent = new GUIContent("Arch Config", EditorGUIUtility.IconContent("ScriptableObject Icon").image);
        window.minSize = new Vector2(620, 360);
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<ArchConfigWindow>())
            window.Refresh();
    }

    private void OnEnable()
    {
        _leftWidth = EditorPrefs.GetFloat(LEFT_WIDTH_PREF, 260f);
        _showIssues = EditorPrefs.GetBool(SHOW_ISSUES_PREF, true);
        wantsMouseMove = true;
        _model.Changed += Repaint;
        Undo.undoRedoPerformed += OnUndoRedo;
        Refresh();
    }

    private void OnDisable()
    {
        _model.Changed -= Repaint;
        Undo.undoRedoPerformed -= OnUndoRedo;
        _serializedCache.Clear();
    }

    private void OnProjectChange()
    {
        Refresh();
    }

    private void OnUndoRedo()
    {
        _model.SaveDirtyComposites();
    }

    private void Refresh()
    {
        _serializedCache.Clear();
        _hasFieldsCache.Clear();
        _model.Reload();
        Repaint();
    }

    #region GUI

    private void OnGUI()
    {
        Styles.Init();
        var e = Event.current;
        if (e.type == EventType.MouseMove)
            Repaint();
        if (e.type == EventType.DragExited || e.type == EventType.MouseUp)
        {
            _dropTarget = null;
            _pressed = false;
        }

        if (_model.Config == null)
        {
            EditorGUILayout.HelpBox("Config is not loaded.", MessageType.Warning);
            if (GUILayout.Button("Refresh")) Refresh();
            return;
        }

        EditorGUIUtility.SetIconSize(new Vector2(16, 16));
        DrawToolbar();
        using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
        {
            DrawServicesPanel();
            DrawSplitter();
            DrawScopesPanel();
        }
        DrawIssuesPanel();
        EditorGUIUtility.SetIconSize(Vector2.zero);

        // Data changes are applied after layout to keep IMGUI groups consistent
        if (_deferred.Count > 0)
        {
            var actions = _deferred.ToList();
            _deferred.Clear();
            foreach (var action in actions)
                action();
            Repaint();
        }
    }

    private void Defer(Action action) => _deferred.Add(action);

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button(new GUIContent(" Refresh", Styles.RefreshIcon.image, "Rescan services and entry points"), EditorStyles.toolbarButton, GUILayout.Width(76)))
                Defer(Refresh);
            if (GUILayout.Button(new GUIContent("Sync configs", "Update entry points and scenes configs from code"), EditorStyles.toolbarButton, GUILayout.Width(90)))
                Defer(_model.SyncConfigs);
            GUILayout.Space(6);
            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MaxWidth(240));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Config", "Select ArchConfigData asset"), EditorStyles.toolbarButton))
                ArchConfigModel.SelectAsset(_model.Config);

            int problems = _model.Issues.Count(i => i.Type != MessageType.Info);
            int errors = _model.Issues.Count(i => i.Type == MessageType.Error);
            var icon = errors > 0 ? Styles.ErrorIcon : problems > 0 ? Styles.WarnIcon : Styles.InfoIcon;
            var content = new GUIContent($" {problems}", icon.image, "Show problems");
            bool show = GUILayout.Toggle(_showIssues, content, EditorStyles.toolbarButton, GUILayout.Width(48));
            if (show != _showIssues)
            {
                _showIssues = show;
                EditorPrefs.SetBool(SHOW_ISSUES_PREF, show);
            }
        }
    }

    private void DrawServicesPanel()
    {
        var panel = new EditorGUILayout.VerticalScope(GUILayout.Width(_leftWidth));
        using (panel)
        {
            DrawSectionHeader("Services", "Drag to a scope, right click for actions.\nDrag a row from a scope here to remove it.");
            _servicesScroll = EditorGUILayout.BeginScrollView(_servicesScroll);
            var visible = _model.Services.Where(s => Matches(s.Name)).ToList();
            foreach (var service in visible.Where(s => s.Installer != null))
                DrawServiceRow(service);

            var broken = visible.Where(s => s.Installer == null).ToList();
            if (broken.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("Without installer", EditorStyles.miniBoldLabel);
                foreach (var service in broken)
                    DrawServiceRow(service);
            }
            if (visible.Count == 0)
                GUILayout.Label(_model.Services.Count == 0 ? "No classes with [Service] attribute" : "Nothing found", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();
        }
        HandleRemoveDrop(panel.rect);
    }

    private void DrawServiceRow(ServiceInfo service)
    {
        var scopes = service.Installer != null ? _model.ScopesOf(service.Installer) : new List<ScopeInfo>();
        Texture icon = service.Installer == null ? Styles.WarnIcon.image : Styles.ScriptIcon.image;
        string usage = service.Installer == null ? "no installer" : scopes.Count == 0 ? "unused" : string.Join(", ", scopes.Select(s => s.Title));
        string tooltip = service.InstallerType != null ? $"Installer: {service.InstallerType.Name}" : service.Error ?? "No installer";

        Rect row = EditorGUILayout.BeginHorizontal(Styles.Row);
        DrawHover(row);
        GUILayout.Label(new GUIContent(service.Name, icon, tooltip), Styles.RowLabel, GUILayout.MinWidth(40));
        GUILayout.FlexibleSpace();
        GUILayout.Label(new GUIContent(usage, usage), scopes.Count == 0 ? Styles.MutedWarning : Styles.Muted, GUILayout.MaxWidth(_leftWidth * 0.5f));
        EditorGUILayout.EndHorizontal();

        HandleRowEvents(row, service.Installer, null, () => ShowServiceMenu(service));
    }

    private void DrawSplitter()
    {
        Rect rect = GUILayoutUtility.GetRect(5f, 5f, GUILayout.Width(5f), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y, 1, rect.height), Styles.SplitterColor);
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

        var e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            _resizing = true;
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseDrag)
        {
            _leftWidth = Mathf.Clamp(e.mousePosition.x, 180f, Mathf.Max(180f, position.width - 320f));
            Repaint();
            e.Use();
        }
        else if (_resizing && e.rawType == EventType.MouseUp)
        {
            _resizing = false;
            EditorPrefs.SetFloat(LEFT_WIDTH_PREF, _leftWidth);
        }
    }

    private void DrawScopesPanel()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            DrawSectionHeader("Scopes", "Global scope lives for the whole app lifetime.\nOther scopes live while their state is active.");
            _scopesScroll = EditorGUILayout.BeginScrollView(_scopesScroll);
            foreach (var scope in Scopes)
                DrawScopeCard(scope);
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawScopeCard(ScopeInfo scope)
    {
        bool collapsed = _collapsedScopes.Contains(scope.Name);
        Color accent = Styles.Accent(scope.IsGlobal, scope.IsStart, scope.IsStale || (!scope.IsGlobal && !scope.IsRegistered));

        Rect card = EditorGUILayout.BeginVertical(Styles.Card);
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(new Rect(card.x, card.y, 3, card.height), accent);
            if (_dropTarget == scope.Name)
                EditorGUI.DrawRect(card, Styles.DropColor);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            Rect foldRect = GUILayoutUtility.GetRect(14f, 20f, GUILayout.Width(14f));
            bool expanded = EditorGUI.Foldout(foldRect, !collapsed, GUIContent.none, true);
            if (expanded == collapsed)
            {
                if (expanded) _collapsedScopes.Remove(scope.Name);
                else _collapsedScopes.Add(scope.Name);
            }

            GUILayout.Label(new GUIContent(scope.Title, scope.EntryType?.FullName ?? scope.Name), Styles.CardTitle);
            if (scope.IsGlobal) DrawBadge("GLOBAL", accent);
            if (scope.IsStart) DrawBadge("START", accent);
            if (scope.IsStale) DrawBadge("MISSING", Styles.WarningColor);
            else if (!scope.IsGlobal && !scope.IsRegistered) DrawBadge("NOT REGISTERED", Styles.WarningColor);
            else if (!scope.IsGlobal && scope.IsIgnored) DrawBadge("IGNORED", Styles.SplitterColor);
            if (!scope.IsGlobal && scope.IsRegistered && !scope.IsIgnored)
                GUILayout.Label($"priority {scope.Priority}", Styles.Muted);

            GUILayout.FlexibleSpace();
            int count = scope.Installers.Count;
            GUILayout.Label(count == 1 ? "1 service" : $"{count} services", Styles.Muted);
            if (scope.EntryType != null && IconButton(Styles.ScriptIcon, "Open entry point script"))
                ArchConfigModel.OpenScript(scope.EntryType);
            if (scope.Composite != null && IconButton(Styles.AssetIcon, "Select composite installer asset"))
                ArchConfigModel.SelectAsset(scope.Composite);
            if (scope.Composite != null && !scope.IsStale && IconButton(Styles.PlusIcon, "Add service"))
                ShowAddMenu(scope);
        }

        if (scope.IsGlobal)
            GUILayout.Label($"{scope.Name} · services live for the whole app lifetime", Styles.Muted);
        else if (!scope.IsStale)
            GUILayout.Label($"Child of Global · lives while {scope.Name} is active", Styles.Muted);

        if (!collapsed)
        {
            GUILayout.Space(3);
            if (scope.IsStale)
            {
                EditorGUILayout.HelpBox("Entry point class not found. Restore the class or remove this scope from config.", MessageType.Warning);
                if (GUILayout.Button("Remove from config", GUILayout.Width(140)))
                    Defer(() => _model.RemoveStaleScope(scope));
            }
            else if (scope.Installers.Count == 0)
            {
                GUILayout.Label("Drop services here or press +", Styles.Placeholder);
            }

            var installers = scope.Installers;
            for (int i = 0; i < installers.Count; i++)
                DrawInstallerRow(scope, installers[i]);
        }
        EditorGUILayout.EndVertical();

        if (!scope.IsStale)
            HandleDrop(card, scope);
    }

    private void DrawInstallerRow(ScopeInfo scope, ScriptableInstaller installer)
    {
        if (installer == null)
        {
            using (new EditorGUILayout.HorizontalScope(Styles.Row))
            {
                GUILayout.Label(new GUIContent("Missing installer asset", Styles.ErrorIcon.image), Styles.RowLabel);
                GUILayout.FlexibleSpace();
                if (IconButton(Styles.MinusIcon, "Remove empty slots"))
                    Defer(() => _model.RemoveEmptySlots(scope));
            }
            return;
        }

        string name = _model.DisplayName(installer);
        if (!Matches(name) && !Matches(installer.name)) return;

        if (!_hasFieldsCache.TryGetValue(installer, out bool hasFields))
            _hasFieldsCache[installer] = hasFields = ArchConfigModel.TopLevelProperties(installer).Any();
        string key = $"{scope.Name}/{installer.GetInstanceID()}";
        bool expanded = hasFields && _expandedRows.Contains(key);
        var global = _model.GlobalScope;
        bool alsoGlobal = !scope.IsGlobal && global != null && global.Installers.Contains(installer);
        Texture icon = _model.ProblemInstallers.Contains(installer) ? Styles.ErrorIcon.image : Styles.AssetIcon.image;

        Rect row = EditorGUILayout.BeginHorizontal(Styles.Row);
        DrawHover(row);
        Rect foldRect = GUILayoutUtility.GetRect(14f, 18f, GUILayout.Width(14f));
        if (hasFields)
        {
            bool newExpanded = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
            if (newExpanded != expanded)
            {
                if (newExpanded) _expandedRows.Add(key);
                else _expandedRows.Remove(key);
            }
        }
        GUILayout.Label(new GUIContent(name, icon, AssetDatabase.GetAssetPath(installer)), Styles.RowLabel);
        if (name != installer.name)
            GUILayout.Label(installer.name, Styles.Muted);
        if (alsoGlobal)
            DrawBadge("ALSO GLOBAL", Styles.WarningColor);
        GUILayout.FlexibleSpace();
        if (IconButton(Styles.MenuIcon, "More actions"))
            ShowInstallerMenu(scope, installer);
        if (IconButton(Styles.MinusIcon, "Remove from scope"))
            Defer(() => _model.RemoveInstaller(scope, installer));
        EditorGUILayout.EndHorizontal();

        HandleRowEvents(row, installer, scope, () => ShowInstallerMenu(scope, installer));

        if (expanded)
            DrawInlineInspector(installer);
    }

    private void DrawInlineInspector(ScriptableInstaller installer)
    {
        if (!_serializedCache.TryGetValue(installer, out var serialized) || serialized.targetObject == null)
        {
            serialized = new SerializedObject(installer);
            _serializedCache[installer] = serialized;
        }

        using (new EditorGUILayout.VerticalScope(Styles.Inspector))
        {
            if (_model.ScopesOf(installer).Count > 1)
                GUILayout.Label("Shared asset: changes affect every scope that uses it", Styles.Muted);

            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 140f;
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            var property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(property, true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                Defer(() =>
                {
                    AssetDatabase.SaveAssetIfDirty(installer);
                    _model.BuildIssues();
                });
            }
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }

    private void DrawIssuesPanel()
    {
        if (!_showIssues) return;

        using (new EditorGUILayout.VerticalScope(Styles.IssuesBox))
        {
            var issues = _model.Issues;
            DrawSectionHeader($"Problems ({issues.Count(i => i.Type != MessageType.Info)})", "Errors fail the build, see VooArch/Validate Config On Build");
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No problems found", MessageType.Info);
                return;
            }

            _issuesScroll = EditorGUILayout.BeginScrollView(_issuesScroll, GUILayout.MaxHeight(160));
            foreach (var issue in issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.Text, issue.Type);
                    if (issue.Fix != null && GUILayout.Button(issue.FixLabel, GUILayout.Width(80), GUILayout.ExpandHeight(true)))
                        Defer(issue.Fix);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSectionHeader(string title, string tooltip)
    {
        GUILayout.Label(new GUIContent(title, tooltip), Styles.SectionHeader);
    }

    private static void DrawBadge(string text, Color color)
    {
        var content = new GUIContent(text);
        Rect rect = GUILayoutUtility.GetRect(content, Styles.Badge);
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.35f));
        GUI.Label(rect, content, Styles.Badge);
    }

    private static void DrawHover(Rect row)
    {
        if (Event.current.type == EventType.Repaint && row.Contains(Event.current.mousePosition))
            EditorGUI.DrawRect(row, Styles.HoverColor);
    }

    private static bool IconButton(GUIContent icon, string tooltip)
    {
        return GUILayout.Button(new GUIContent(icon.image, tooltip), Styles.IconButton, GUILayout.Width(20), GUILayout.Height(18));
    }

    private bool Matches(string text)
    {
        return string.IsNullOrEmpty(_search) || text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    #endregion

    #region Menus and drag & drop

    private void ShowServiceMenu(ServiceInfo service)
    {
        var menu = new GenericMenu();
        if (service.Installer != null)
        {
            foreach (var scope in Scopes.Where(s => !s.IsStale && s.Composite != null))
            {
                bool contains = scope.Installers.Contains(service.Installer);
                menu.AddItem(new GUIContent($"Scopes/{scope.Title}"), contains, () =>
                {
                    if (contains) _model.RemoveInstaller(scope, service.Installer);
                    else _model.AddInstaller(scope, service.Installer);
                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Select installer asset"), false, () => ArchConfigModel.SelectAsset(service.Installer));
        }
        menu.AddItem(new GUIContent("Open service script"), false, () => ArchConfigModel.OpenScript(service.ServiceType));
        if (service.InstallerType != null)
            menu.AddItem(new GUIContent("Open installer script"), false, () => ArchConfigModel.OpenScript(service.InstallerType));
        menu.ShowAsContext();
    }

    private void ShowInstallerMenu(ScopeInfo scope, ScriptableInstaller installer)
    {
        var menu = new GenericMenu();
        foreach (var target in Scopes.Where(s => s != scope && !s.IsStale && s.Composite != null))
        {
            if (target.Installers.Contains(installer))
            {
                menu.AddDisabledItem(new GUIContent($"Move to/{target.Title}"));
                menu.AddDisabledItem(new GUIContent($"Copy to/{target.Title}"));
            }
            else
            {
                menu.AddItem(new GUIContent($"Move to/{target.Title}"), false, () => _model.MoveInstaller(scope, target, installer));
                menu.AddItem(new GUIContent($"Copy to/{target.Title}"), false, () => _model.AddInstaller(target, installer));
            }
        }
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Select installer asset"), false, () => ArchConfigModel.SelectAsset(installer));
        var service = _model.ServiceOf(installer);
        if (service != null)
            menu.AddItem(new GUIContent("Open service script"), false, () => ArchConfigModel.OpenScript(service.ServiceType));
        menu.AddItem(new GUIContent("Open installer script"), false, () => ArchConfigModel.OpenScript(installer.GetType()));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Remove from scope"), false, () => _model.RemoveInstaller(scope, installer));
        menu.ShowAsContext();
    }

    private void ShowAddMenu(ScopeInfo scope)
    {
        var menu = new GenericMenu();
        var candidates = _model.Services.Where(s => s.Installer != null && !scope.Installers.Contains(s.Installer)).ToList();
        var unused = candidates.Where(s => _model.ScopesOf(s.Installer).Count == 0).ToList();
        var used = candidates.Except(unused).ToList();

        foreach (var service in unused)
            menu.AddItem(new GUIContent(service.Name), false, () => _model.AddInstaller(scope, service.Installer));
        if (unused.Count > 0 && used.Count > 0)
            menu.AddSeparator("");
        foreach (var service in used)
        {
            string where = string.Join(", ", _model.ScopesOf(service.Installer).Select(s => s.Title));
            menu.AddItem(new GUIContent($"{service.Name}   ({where})"), false, () => _model.AddInstaller(scope, service.Installer));
        }
        if (candidates.Count == 0)
            menu.AddDisabledItem(new GUIContent("All services are already in this scope"));
        menu.ShowAsContext();
    }

    private void HandleRowEvents(Rect row, ScriptableInstaller installer, ScopeInfo from, Action showMenu)
    {
        var e = Event.current;
        if (!row.Contains(e.mousePosition) && e.type != EventType.MouseDrag) return;

        switch (e.type)
        {
            case EventType.ContextClick:
                showMenu();
                e.Use();
                break;
            case EventType.MouseDown when e.button == 0 && installer != null:
                if (e.clickCount == 2)
                {
                    ArchConfigModel.SelectAsset(installer);
                    e.Use();
                    break;
                }
                _pressed = true;
                _pressedInstaller = installer;
                _pressedFrom = from;
                break;
            case EventType.MouseDrag when _pressed && _pressedInstaller == installer && _pressedFrom == from:
                _pressed = false;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { installer };
                DragAndDrop.SetGenericData(DRAG_FROM_KEY, from?.Name ?? "");
                DragAndDrop.StartDrag(_model.DisplayName(installer));
                e.Use();
                break;
        }
    }

    private void HandleDrop(Rect rect, ScopeInfo scope)
    {
        var e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        if (!rect.Contains(e.mousePosition))
        {
            if (_dropTarget == scope.Name)
            {
                _dropTarget = null;
                Repaint();
            }
            return;
        }

        var dragged = DragAndDrop.objectReferences.OfType<ScriptableInstaller>().Where(i => _model.CanAdd(scope, i)).ToList();
        if (dragged.Count == 0)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            return;
        }

        var from = DraggedFrom();
        // Dragging between scopes moves the service, hold Ctrl/Cmd to copy
        bool move = from != null && from != scope && !EditorGUI.actionKey;
        DragAndDrop.visualMode = move ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Copy;
        if (_dropTarget != scope.Name)
        {
            _dropTarget = scope.Name;
            Repaint();
        }

        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            _dropTarget = null;
            Defer(() =>
            {
                foreach (var installer in dragged)
                {
                    if (move) _model.MoveInstaller(from, scope, installer);
                    else _model.AddInstaller(scope, installer);
                }
            });
        }
        e.Use();
    }

    private void HandleRemoveDrop(Rect rect)
    {
        var e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        if (!rect.Contains(e.mousePosition)) return;

        var from = DraggedFrom();
        var installer = DragAndDrop.objectReferences.OfType<ScriptableInstaller>().FirstOrDefault();
        if (from == null || installer == null) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            Defer(() => _model.RemoveInstaller(from, installer));
        }
        e.Use();
    }

    private ScopeInfo DraggedFrom()
    {
        string name = DragAndDrop.GetGenericData(DRAG_FROM_KEY) as string;
        return string.IsNullOrEmpty(name) ? null : Scopes.FirstOrDefault(s => s.Name == name);
    }

    #endregion

    private static class Styles
    {
        public static GUIStyle Card;
        public static GUIStyle CardTitle;
        public static GUIStyle Row;
        public static GUIStyle RowLabel;
        public static GUIStyle Muted;
        public static GUIStyle MutedWarning;
        public static GUIStyle Badge;
        public static GUIStyle Placeholder;
        public static GUIStyle SectionHeader;
        public static GUIStyle Inspector;
        public static GUIStyle IssuesBox;
        public static GUIStyle IconButton;

        public static GUIContent RefreshIcon;
        public static GUIContent PlusIcon;
        public static GUIContent MinusIcon;
        public static GUIContent MenuIcon;
        public static GUIContent ScriptIcon;
        public static GUIContent AssetIcon;
        public static GUIContent WarnIcon;
        public static GUIContent ErrorIcon;
        public static GUIContent InfoIcon;

        public static readonly Color WarningColor = new(0.85f, 0.5f, 0.2f);
        public static readonly Color DropColor = new(0.3f, 0.6f, 1f, 0.18f);
        public static Color SplitterColor => EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.6f, 0.6f, 0.6f);
        public static Color HoverColor => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.04f) : new Color(0f, 0f, 0f, 0.05f);

        private static bool _ready;
        private static bool _proSkin;

        public static Color Accent(bool global, bool start, bool problem)
        {
            if (problem) return WarningColor;
            if (global) return new Color(0.2f, 0.62f, 0.62f);
            if (start) return new Color(0.35f, 0.7f, 0.35f);
            return new Color(0.45f, 0.55f, 0.85f);
        }

        public static void Init()
        {
            if (_ready && _proSkin == EditorGUIUtility.isProSkin) return;
            _ready = true;
            _proSkin = EditorGUIUtility.isProSkin;

            Card = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 8, 6, 8), margin = new RectOffset(4, 4, 4, 6) };
            CardTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            Row = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(4, 4, 2, 2), margin = new RectOffset(2, 2, 1, 1) };
            RowLabel = new GUIStyle(EditorStyles.label) { fixedHeight = 18 };
            Muted = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = _proSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f) } };
            MutedWarning = new GUIStyle(Muted) { normal = { textColor = WarningColor } };
            Badge = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(5, 5, 1, 1), margin = new RectOffset(3, 3, 3, 0) };
            Placeholder = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fixedHeight = 32 };
            SectionHeader = new GUIStyle(EditorStyles.boldLabel) { margin = new RectOffset(6, 6, 4, 2) };
            Inspector = new GUIStyle { padding = new RectOffset(22, 6, 2, 6) };
            IssuesBox = new GUIStyle { padding = new RectOffset(4, 4, 2, 4) };
            IconButton = new GUIStyle(EditorStyles.iconButton) { fixedWidth = 20, fixedHeight = 18, alignment = TextAnchor.MiddleCenter };

            RefreshIcon = EditorGUIUtility.IconContent("Refresh");
            PlusIcon = EditorGUIUtility.IconContent("Toolbar Plus");
            MinusIcon = EditorGUIUtility.IconContent("Toolbar Minus");
            MenuIcon = EditorGUIUtility.IconContent("_Popup");
            ScriptIcon = EditorGUIUtility.IconContent("cs Script Icon");
            AssetIcon = EditorGUIUtility.IconContent("ScriptableObject Icon");
            WarnIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
            ErrorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
            InfoIcon = EditorGUIUtility.IconContent("console.infoicon.sml");
        }
    }
}
#endif
