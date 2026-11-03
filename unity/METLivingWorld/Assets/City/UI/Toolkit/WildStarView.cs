using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
// A detached star diagram. This view never reads or commands the simulation.
public sealed class WildStarView : MonoBehaviour {
 UIDocument _document;
 PanelSettings _panel;
 VisualElement _root, _graph;
 ScrollView _graphScroll, _inspector;
 StarRoutes _routes;
 readonly Dictionary<string, Button> _nodes = new Dictionary<string, Button>();
 Action<string> _select;
 Action _confirm, _remove, _close;
 WildStarWindowModel _model;
 float _uiScale = 1, _textScale = 1;
 bool _highContrast, _removalVisible;
 public bool IsOpen { get; private set; }
 public VisualElement Root => _root;
 static readonly string[] NodeIds = { "compound", "housing", "production", "organization", "link", "fusion", "apartment", "agriculture" };
 static readonly Vector2[] Positions = {
  new Vector2(.5f, .10f), new Vector2(.28f, .35f), new Vector2(.72f, .35f),
  new Vector2(.16f, .63f), new Vector2(.5f, .63f), new Vector2(.84f, .63f),
  new Vector2(.28f, .89f), new Vector2(.72f, .89f)
 };

 public void Show(WildStarWindowModel model, Action<string> select, Action confirm, Action remove, Action close) {
  if (model == null) throw new ArgumentNullException(nameof(model));
  _select = select; _confirm = confirm; _remove = remove; _close = close;
  if (IsOpen) { Refresh(model); return; }
  EnsureDocument();
  _document.enabled = true;
  _root = _document.rootVisualElement;
  _root.AddToClassList("wild-root");
  var font = Resources.Load<Font>("CityFonts/NotoSansSC");
  if (font != null) _root.style.unityFontDefinition = FontDefinition.FromFont(font);
  _graph = _root.Q("wild-graph");
  _graphScroll = _root.Q<ScrollView>("wild-graph-scroll");
  _inspector = _root.Q<ScrollView>("wild-inspector");
  _inspector.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
  _nodes.Clear();
  _routes = new StarRoutes();
  _routes.AddToClassList("wild-graph-lines");
  _graph.Add(_routes);
  for (var i = 0; i < NodeIds.Length; i++) {
   var id = NodeIds[i];
   var node = new Button(() => { DismissConfirmation(); _select?.Invoke(id); });
   node.name = "wild-node-" + id;
   node.AddToClassList("wild-node");
   node.Add(new Label { name = "node-title" }); node.Q<Label>("node-title").AddToClassList("node-title");
   node.Add(new Label { name = "node-subtitle" }); node.Q<Label>("node-subtitle").AddToClassList("node-subtitle");
   node.Add(new Label { name = "node-status" }); node.Q<Label>("node-status").AddToClassList("node-status");
   _nodes.Add(id, node); _graph.Add(node);
  }
  _routes.Nodes = _nodes;
  for (var side = 0; side < 2; side++) {
   var bypass = new Label("可跳过槽位"); bypass.name = "wild-bypass-" + side;
   bypass.AddToClassList("wild-bypass-label"); bypass.AddToClassList("text-micro");
   bypass.pickingMode = PickingMode.Ignore; _graph.Add(bypass);
  }
  _graph.RegisterCallback<GeometryChangedEvent>(_ => _routes.MarkDirtyRepaint());
  Bind("wild-close", OnClose); Bind("wild-cancel", OnClose); Bind("wild-confirm", OnConfirm);
  Bind("wild-remove", OnRequestRemove); Bind("wild-keep", OnKeep); Bind("wild-remove-yes", OnRemove);
  _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
  IsOpen = true; _removalVisible = false;
  Refresh(model);
  _graphScroll.scrollOffset = Vector2.zero; _inspector.scrollOffset = Vector2.zero;
  _root.schedule.Execute(() => {
   if (!IsOpen) return;
   if (_nodes.TryGetValue(model.SelectedId ?? "", out var node)) node.Focus();
   else _root.Q<Button>("wild-cancel").Focus();
  });
 }

 public void Refresh(WildStarWindowModel model) {
  if (!IsOpen || model == null) return;
  if (_model != null && _model.SelectedId != model.SelectedId) _removalVisible = false;
  _model = model;
  _routes.Edges = model.Routes;
  SetText("wild-title", model.Title); SetText("wild-subtitle", model.Subtitle);
  SetText("wild-stock", model.StockText);
  SetText("wild-capacity", model.CapacityUsed.ToString("0.##") + " / " + model.CapacityTotal.ToString("0.##"));
  _root.Q("wild-capacity-fill").style.width = Length.Percent(model.CapacityTotal > 0 ? Mathf.Clamp01(model.CapacityUsed / model.CapacityTotal) * 100 : 0);
  SetText("wild-selected-title", model.SelectedTitle); SetText("wild-description", model.Description);
  SetText("wild-status", model.StatusText); SetText("wild-costs", model.Costs);
  SetText("wild-resources", model.ResourceCost); SetText("wild-available", model.ResourceAvailable);
  SetText("wild-dp", string.IsNullOrEmpty(model.DpCost) ? "" : "设计占用 · " + model.DpCost); SetText("wild-dp-free", model.DpFree);
  SetText("wild-cp", string.IsNullOrEmpty(model.CpCost) ? "" : "施工工程量 · " + model.CpCost);
  SetText("wild-prerequisites", model.Prerequisites); SetText("wild-constraint", model.Constraint);
  SetText("wild-removal-description", model.RemovalDescription);
  var confirm = _root.Q<Button>("wild-confirm");
  confirm.text = string.IsNullOrEmpty(model.ConfirmText) ? "确认建造" : model.ConfirmText;
  confirm.SetEnabled(model.CanConfirm);
  _root.Q<Button>("wild-remove").style.display = model.CanRemove ? DisplayStyle.Flex : DisplayStyle.None;
  if (!model.CanRemove) _removalVisible = false;
  UpdateConfirmation();
  foreach (var id in NodeIds) {
   WildStarNodeModel data = null;
   foreach (var candidate in model.Nodes) if (candidate.Id == id) { data = candidate; break; }
   var node = _nodes[id];
   node.Q<Label>("node-title").text = data?.Title ?? id;
   var subtitle = node.Q<Label>("node-subtitle");
   subtitle.text = data?.Subtitle ?? "";
   subtitle.style.display = string.IsNullOrEmpty(subtitle.text) ? DisplayStyle.None : DisplayStyle.Flex;
   var state = data?.State ?? "locked";
   var marker = state == "completed" ? "● " : state == "pending" ? "○ " : state == "locked" ? "× " : "◇ ";
   var stateText = state == "completed" ? "已完成" : state == "pending" ? "施工中" : state == "locked" ? "未解锁" : "可选择";
   node.Q<Label>("node-status").text = (id == model.SelectedId ? "已选 · " : "") + marker + (string.IsNullOrEmpty(data?.StatusText) ? stateText : data.StatusText);
   node.EnableInClassList("is-completed", state == "completed");
   node.EnableInClassList("is-pending", state == "pending");
   node.EnableInClassList("is-locked", state == "locked");
   node.EnableInClassList("is-selected", id == model.SelectedId);
   node.tooltip = data?.Subtitle ?? "";
  }
  ApplyDisplay(_uiScale, _textScale, _highContrast);
  _routes.MarkDirtyRepaint();
 }

 public void ApplyDisplay(float uiScale, float textScale, bool highContrast) {
  _uiScale = uiScale; _textScale = textScale; _highContrast = highContrast;
  if (_panel != null) _panel.scale = uiScale;
  if (_root == null) return;
  _root.EnableInClassList("high-contrast", highContrast);
  _root.style.fontSize = 24 * textScale;
  _root.Query<TextElement>().ForEach(element => element.style.fontSize = 24 * textScale);
  FontSize("text-hero", 36); FontSize("text-heading", 27); FontSize("text-small", 22); FontSize("text-micro", 18);
  FontSize("node-title", 27); FontSize("node-subtitle", 20); FontSize("node-status", 18);
  // Text enlargement grows the diagram's scrollable canvas, never hides labels.
  float width = 1100 * Mathf.Max(1, textScale), height = 640 * Mathf.Max(1, textScale);
  float nodeWidth = 272 * Mathf.Max(1, textScale), nodeHeight = 126 * Mathf.Max(1, textScale);
  _graph.style.width = width; _graph.style.height = height;
  for (var i = 0; i < NodeIds.Length; i++) {
   var node = _nodes[NodeIds[i]];
   node.style.width = nodeWidth; node.style.height = nodeHeight;
   node.style.left = width * Positions[i].x - nodeWidth / 2;
   node.style.top = height * Positions[i].y - nodeHeight / 2;
  }
  for (var side = 0; side < 2; side++) {
   var label = _graph.Q<Label>("wild-bypass-" + side);
   label.style.top = height * .455f; label.style.width = 170 * Mathf.Max(1, textScale);
   if (side == 0) label.style.left = 12 * Mathf.Max(1, textScale);
   else label.style.right = 12 * Mathf.Max(1, textScale);
  }
  _routes.MarkDirtyRepaint();
 }

 public bool DismissConfirmation() {
  if (!IsOpen || !_removalVisible) return false;
  _removalVisible = false; UpdateConfirmation();
  _root.Q<Button>("wild-remove").Focus();
  return true;
 }
 public void Hide() {
  if (!IsOpen) return;
  IsOpen = false; _removalVisible = false;
  _select = null; _confirm = null; _remove = null; _close = null; _model = null;
  _root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
  _graph.Clear(); _nodes.Clear();
  _document.enabled = false; _root = null; _graph = null; _routes = null;
 }
 void EnsureDocument() {
  if (_document != null) return;
  var template = Resources.Load<VisualTreeAsset>("CityToolkit/WildStarWindow");
  var settings = Resources.Load<PanelSettings>("CityToolkit/UpgradePanel");
  if (template == null || settings == null) throw new InvalidOperationException("农场星图的 CityToolkit 资源不完整。");
  // Retain the serialized international text resources, independently scale this modal.
  _panel = Instantiate(settings); _panel.name = "MET Wild Star Panel (Runtime)"; _panel.scale = _uiScale;
  var go = new GameObject("Wild Star Toolkit Document"); go.SetActive(false); go.transform.SetParent(transform, false);
  _document = go.AddComponent<UIDocument>(); _document.panelSettings = _panel; _document.visualTreeAsset = template;
  _document.sortingOrder = 210; go.SetActive(true); _document.enabled = false;
 }
 void Bind(string id, Action action) { var button = _root.Q<Button>(id); button.clicked -= action; button.clicked += action; }
 void SetText(string id, string value) { var label = _root.Q<Label>(id); label.text = value ?? ""; label.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; }
 void FontSize(string className, float size) { _root.Query<VisualElement>(className: className).ForEach(element => { element.style.fontSize = size * _textScale; element.Query<TextElement>().ForEach(text => text.style.fontSize = size * _textScale); }); }
 void OnClose() { if (IsOpen) _close?.Invoke(); }
 void OnConfirm() { if (IsOpen && _model.CanConfirm) _confirm?.Invoke(); }
 void OnRequestRemove() { if (IsOpen && _model.CanRemove) { _removalVisible = true; UpdateConfirmation(); _root.Q<Button>("wild-keep").Focus(); _inspector.ScrollTo(_root.Q("wild-remove-confirmation")); } }
 void OnKeep() { DismissConfirmation(); }
 void OnRemove() { if (IsOpen && _removalVisible && _model.CanRemove) { _removalVisible = false; UpdateConfirmation(); _remove?.Invoke(); } }
 void UpdateConfirmation() { _root.Q("wild-remove-confirmation").style.display = _removalVisible ? DisplayStyle.Flex : DisplayStyle.None; }
 void OnKeyDown(KeyDownEvent evt) {
  if (evt.keyCode != KeyCode.Tab) return;
  var buttons = new List<Button>();
  _root.Query<Button>().ForEach(button => {
   if (!button.enabledInHierarchy) return;
   for (VisualElement parent = button; parent != null && parent != _root; parent = parent.parent)
    if (parent.resolvedStyle.display == DisplayStyle.None) return;
   buttons.Add(button);
  });
  if (buttons.Count == 0) return;
  var focused = _root.panel?.focusController.focusedElement as Button;
  var index = buttons.IndexOf(focused);
  var next = (index + (evt.shiftKey ? -1 : 1) + buttons.Count) % buttons.Count;
  buttons[next].Focus(); evt.StopImmediatePropagation(); evt.PreventDefault();
 }
 void OnDestroy() { if (_document != null) Destroy(_document.gameObject); if (_panel != null) Destroy(_panel); }

 sealed class StarRoutes : VisualElement {
  public Dictionary<string, Button> Nodes;
  public List<WildStarRouteModel> Edges = new List<WildStarRouteModel>();
  public StarRoutes() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
  void Draw(MeshGenerationContext context) {
   if (Nodes == null) return;
   var painter = context.painter2D;
   painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
   foreach (var route in Edges) {
    var from = Nodes[route.From]; var to = Nodes[route.To];
    var a = from.layout; var b = to.layout;
    if (float.IsNaN(a.x) || a.width <= 0 || b.width <= 0) continue;
    var start = new Vector2(a.center.x, a.yMax); var end = new Vector2(b.center.x, b.yMin);
    bool active = from.ClassListContains("is-completed");
    bool selected = active && to.ClassListContains("is-selected"), completed = active && to.ClassListContains("is-completed"), pending = active && to.ClassListContains("is-pending");
    painter.strokeColor = selected ? new Color(.55f, .90f, .81f) : completed ? new Color(.26f, .57f, .51f) : pending ? new Color(.65f, .52f, .31f) : new Color(.20f, .32f, .38f);
    painter.lineWidth = selected ? 3 : 2;
    if (route.Bypass) { DrawBypass(painter, a, b); continue; }
    // Separate edges must not merge into a bus that implies extra prerequisites.
    painter.BeginPath(); painter.MoveTo(start); painter.LineTo(end); painter.Stroke();
    // Small open port makes the route direction clear without decorative icons.
    painter.BeginPath(); painter.MoveTo(end + new Vector2(-4, -6)); painter.LineTo(end); painter.LineTo(end + new Vector2(4, -6)); painter.Stroke();
   }
  }
  void DrawBypass(Painter2D painter, Rect a, Rect b) {
   bool right = a.center.x > contentRect.width / 2;
   float edge = right ? contentRect.width - 12 : 12;
   var start = new Vector2(right ? a.xMax : a.xMin, a.center.y);
   var end = new Vector2(right ? b.xMax : b.xMin, b.center.y);
   Dashed(painter, start, new Vector2(edge, start.y));
   Dashed(painter, new Vector2(edge, start.y), new Vector2(edge, end.y));
   Dashed(painter, new Vector2(edge, end.y), end);
   float direction = right ? 1 : -1;
   painter.BeginPath(); painter.MoveTo(end + new Vector2(6 * direction, -4)); painter.LineTo(end); painter.LineTo(end + new Vector2(6 * direction, 4)); painter.Stroke();
  }
  static void Dashed(Painter2D painter, Vector2 a, Vector2 b) {
   var distance = Vector2.Distance(a, b);
   for (float offset = 0; offset < distance; offset += 15) {
    painter.BeginPath(); painter.MoveTo(Vector2.Lerp(a, b, offset / distance));
    painter.LineTo(Vector2.Lerp(a, b, Mathf.Min(offset + 8, distance) / distance)); painter.Stroke();
   }
  }
 }
}
}
