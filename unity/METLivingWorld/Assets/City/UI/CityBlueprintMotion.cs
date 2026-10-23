using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MET.City
{
    /// <summary>Presentation-only pointer/keyboard feedback. Attach to the selectable root; animate a child Visual.</summary>
    [AddComponentMenu("MET/City/Blueprint Motion")]
    public class CityBlueprintMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public CityBlueprintArt Art;
        public RectTransform Visual;
        public bool Chosen;
        public Func<bool> ReducedMotion;
        private bool hovered;
        private bool focused;
        private bool captured;
        private Transform target;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private Vector2 basePosition;
        private float amount;
        private float fill;

        private void OnEnable()
        {
            Capture();
            amount = 0f; fill = Chosen ? 1f : 0f;
            Apply(0f, fill);
        }
        private void Capture()
        {
            Transform wanted = Visual != null ? Visual : (Art != null ? Art.transform : transform);
            if (captured && target == wanted) return;
            Restore();
            target = wanted;
            baseScale = target.localScale;
            baseRotation = target.localRotation;
            RectTransform rt = target as RectTransform;
            basePosition = rt != null ? rt.anchoredPosition : Vector2.zero;
            captured = true;
        }
        public void SetChosen(bool chosen)
        {
            Chosen = chosen;
            if (!isActiveAndEnabled || IsReduced())
            {
                fill = Chosen ? 1f : (hovered || focused ? .35f : 0f);
                if (Art != null) { Art.Fill = fill; Art.SetVerticesDirty(); }
            }
        }
        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; }
        public void OnSelect(BaseEventData eventData) { focused = true; }
        public void OnDeselect(BaseEventData eventData) { focused = false; }
        private bool IsReduced() { return ReducedMotion != null && ReducedMotion(); }
        private void Update()
        {
            Capture();
            bool reduced = IsReduced();
            float desired = hovered || focused || Chosen ? 1f : 0f;
            float desiredFill = Chosen ? 1f : (hovered || focused ? .35f : 0f);
            if (reduced) { amount = 0f; fill = desiredFill; }
            else
            {
                float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
                amount = Mathf.Lerp(amount, desired, blend);
                fill = Mathf.Lerp(fill, desiredFill, blend);
                if (Mathf.Abs(amount - desired) < .001f) amount = desired;
                if (Mathf.Abs(fill - desiredFill) < .001f) fill = desiredFill;
            }
            Apply(amount, fill);
        }
        private void Apply(float lift, float painted)
        {
            if (captured && target != null)
            {
                target.localScale = baseScale * (1f + lift * .035f);
                target.localRotation = baseRotation * Quaternion.Euler(0f, 0f, lift * -.8f);
                // A layout group's direct child retains its anchored position; only a separate visual child lifts.
                RectTransform rt = target as RectTransform;
                if (rt != null && target != transform) rt.anchoredPosition = basePosition + Vector2.up * (lift * 7f);
            }
            if (Art != null && !Mathf.Approximately(Art.Fill, painted))
            { Art.Fill = painted; Art.SetVerticesDirty(); }
        }
        private void Restore()
        {
            if (!captured || target == null) return;
            target.localScale = baseScale;
            target.localRotation = baseRotation;
            RectTransform rt = target as RectTransform;
            if (rt != null && target != transform) rt.anchoredPosition = basePosition;
        }
        private void OnDisable()
        {
            hovered = false; focused = false; amount = 0f; fill = Chosen ? 1f : 0f;
            Restore();
            if (Art != null) { Art.Fill = fill; Art.SetVerticesDirty(); }
        }
    }
}
