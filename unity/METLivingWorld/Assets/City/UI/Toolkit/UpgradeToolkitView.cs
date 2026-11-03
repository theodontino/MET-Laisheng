using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MET.City {
// One modal surface; all simulation and command ownership stays with CityInterface.
public sealed class UpgradeToolkitView : MonoBehaviour {
 UIDocument _document;
 PanelSettings _panel;
 VisualTreeAsset _candidateTemplate, _rowTemplate;
 VisualElement _root;
 ScrollView _body;
 Foldout _comparison;
 Action<string> _select;
 Action _confirm, _cancel;
 float _uiScale = 1f, _textScale = 1f;
 bool _highContrast;
 public bool IsOpen { get; private set; }
 public VisualElement Root => _root;

 public void Show(UpgradeWindowModel model, Action<string> select, Action confirm, Action cancel) {
  if (model == null) throw new ArgumentNullException(nameof(model));
  if (IsOpen) { _select = select; _confirm = confirm; _cancel = cancel; Refresh(model); return; }
  EnsureDocument();
  _select = select; _confirm = confirm; _cancel = cancel;
  _document.enabled = true;
  _root = _document.rootVisualElement;
  _root.AddToClassList("upgrade-root");
  var font = Resources.Load<Font>("CityFonts/NotoSansSC");
  if (font != null) _root.style.unityFontDefinition = FontDefinition.FromFont(font);
  _body = _root.Q<ScrollView>("upgrade-body");
  _body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
  _body.verticalScrollerVisibility = ScrollerVisibility.Auto;
  _comparison = _root.Q<Foldout>("upgrade-city-comparison");
  _comparison.value = false;
  _root.Q<Button>("upgrade-confirm").clicked -= OnConfirm;
  _root.Q<Button>("upgrade-confirm").clicked += OnConfirm;
  _root.Q<Button>("upgrade-cancel").clicked -= OnCancel;
  _root.Q<Button>("upgrade-cancel").clicked += OnCancel;
  _root.Q<Button>("upgrade-close").clicked -= OnCancel;
  _root.Q<Button>("upgrade-close").clicked += OnCancel;
  IsOpen = true;
  Refresh(model);
  _body.scrollOffset = Vector2.zero;
  _root.schedule.Execute(() => {
   if (!IsOpen) return;
   var selected = _root.Q<Button>(CandidateName(model.SelectedId));
   if (selected != null && selected.enabledInHierarchy) selected.Focus();
   else _root.Q<Button>("upgrade-cancel").Focus();
  });
 }

 public void Refresh(UpgradeWindowModel model) {
  if (!IsOpen || model == null) return;
  var scroll = _body.scrollOffset;
  var focusName = (_root.panel?.focusController.focusedElement as VisualElement)?.name;
  SetText("upgrade-title", model.Title);
  SetText("upgrade-subtitle", model.Subtitle);
  SetText("upgrade-tier", model.CurrentTier);
  SetText("upgrade-selected-title", model.SelectedTitle);
  SetText("upgrade-selected-summary", model.SelectedSummary);
  SetText("upgrade-cost", model.Cost);
  SetText("upgrade-material", model.MaterialConstraint);
  SetText("upgrade-power", model.PowerConstraint);
  SetText("upgrade-balance", model.BalanceAfter);
  SetText("upgrade-pay-now", "即付 " + model.Cost);
  SetText("upgrade-disabled-reason", model.DisabledReason);
  _root.Q<Button>("upgrade-confirm").SetEnabled(model.CanConfirm);
  var choices = _root.Q<VisualElement>("upgrade-candidates");
  choices.Clear();
  foreach (var candidate in model.Candidates) {
   var instance = _candidateTemplate.Instantiate();
   var card = instance.Q<Button>("candidate-button");
   card.name = CandidateName(candidate.Id);
   card.EnableInClassList("is-selected", candidate.Id == model.SelectedId);
   card.EnableInClassList("is-unavailable", !candidate.Available);
   // Unaffordable plans remain inspectable: confirmation alone owns the constraint.
   card.Q<Label>("candidate-title").text = candidate.Title;
   card.Q<Label>("candidate-tier").text = candidate.Tier;
   card.Q<Label>("candidate-traits").text = candidate.Traits;
   card.Q<Label>("candidate-slots").text = candidate.Slots;
   card.Q<Label>("candidate-cost").text = candidate.Cost;
   var status = card.Q<Label>("candidate-status");
   status.text = candidate.Id == model.SelectedId ? "已选方案" : "选择方案";
   card.tooltip = candidate.DisabledReason;
   var id = candidate.Id;
   card.clicked += () => _select?.Invoke(id);
   choices.Add(card);
  }
  BindRows("upgrade-primary-rows", model.PrimaryRows);
  BindRows("upgrade-city-rows", model.CityRows);
  ApplyDisplay(_uiScale, _textScale, _highContrast);
  _body.scrollOffset = scroll;
  if (!string.IsNullOrEmpty(focusName)) {
   var target = _root.Q<VisualElement>(focusName);
   if (target != null && target.enabledInHierarchy) target.Focus();
  }
 }

 public void ApplyDisplay(float uiScale, float textScale, bool highContrast) {
  _uiScale = uiScale; _textScale = textScale; _highContrast = highContrast;
  if (_panel != null) _panel.scale = _uiScale;
  if (_root == null) return;
  _root.EnableInClassList("high-contrast", highContrast);
  _root.style.fontSize = 24f * _textScale;
  _root.Query<TextElement>().ForEach(element => element.style.fontSize = 24f * _textScale);
  SetFontSize("text-hero", 38f);
  SetFontSize("text-heading", 28f);
  SetFontSize("text-small", 21f);
  SetFontSize("text-micro", 18f);
 }

 public void Hide() {
  if (!IsOpen) return;
  IsOpen = false;
  _select = null; _confirm = null; _cancel = null;
  _document.enabled = false;
  _root = null; _body = null; _comparison = null;
 }

 void EnsureDocument() {
  if (_document != null) return;
  var template = Resources.Load<VisualTreeAsset>("CityToolkit/UpgradeWindow");
  _candidateTemplate = Resources.Load<VisualTreeAsset>("CityToolkit/UpgradeCandidate");
  _rowTemplate = Resources.Load<VisualTreeAsset>("CityToolkit/UpgradeStatRow");
  var panelAsset = Resources.Load<PanelSettings>("CityToolkit/UpgradePanel");
  if (template == null || _candidateTemplate == null || _rowTemplate == null || panelAsset == null)
   throw new InvalidOperationException("建筑改造界面的 CityToolkit 资源不完整。");
  // Clone the serialized resource to retain Unity's international text data while
  // keeping this modal's UI scale independent of the asset and any other panel.
  _panel = Instantiate(panelAsset);
  _panel.name = "MET Upgrade Panel (Runtime)";
  _panel.scale = _uiScale;
  var go = new GameObject("Upgrade Toolkit Document");
  go.SetActive(false);
  go.transform.SetParent(transform, false);
  _document = go.AddComponent<UIDocument>();
  _document.panelSettings = _panel;
  _document.visualTreeAsset = template;
  _document.sortingOrder = 200;
  go.SetActive(true);
  _document.enabled = false;
 }

 void BindRows(string name, System.Collections.Generic.List<UpgradeMetricRow> rows) {
  var parent = _root.Q<VisualElement>(name);
  parent.Clear();
  var index = 0;
  foreach (var data in rows) {
   var instance = _rowTemplate.Instantiate();
   var row = instance.Q<VisualElement>("stat-row");
   row.Q<Label>("stat-label").text = data.Label;
   row.Q<Label>("stat-before").text = data.Baseline;
   row.Q<Label>("stat-after").text = data.Predicted;
   row.Q<Label>("stat-delta").text = data.Delta;
   row.Q<Label>("stat-unit").text = data.Unit;
   row.tooltip = data.Hint;
   row.EnableInClassList("is-warning", data.Warning);
   row.EnableInClassList("alternate-row", index++ % 2 == 1);
   parent.Add(row);
  }
 }
 void SetText(string name, string value) {
  var label = _root.Q<Label>(name);
  label.text = value ?? "";
  label.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
 }
 void SetFontSize(string className, float size) {
  _root.Query<VisualElement>(className: className).ForEach(element => {
   element.style.fontSize = size * _textScale;
   element.Query<TextElement>().ForEach(text => text.style.fontSize = size * _textScale);
  });
 }
 static string CandidateName(string id) => "candidate-" + id;
 void OnConfirm() { if (IsOpen && _root.Q<Button>("upgrade-confirm").enabledInHierarchy) _confirm?.Invoke(); }
 void OnCancel() { if (IsOpen) _cancel?.Invoke(); }
 void OnDestroy() {
  if (_document != null) Destroy(_document.gameObject);
  if (_panel != null) Destroy(_panel);
 }
}
}
