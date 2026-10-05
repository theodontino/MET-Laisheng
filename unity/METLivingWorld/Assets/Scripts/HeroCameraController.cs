using UnityEngine;

namespace MET.LivingWorld
{
    [RequireComponent(typeof(Camera))]
    public sealed class HeroCameraController : MonoBehaviour
    {
        private Camera worldCamera;
        private Vector3 initialPosition, initialTarget, up;
        private Vector3 offsetTarget, offsetCurrent;
        private float originalSpan, spanTarget;
        private Vector3 lastMouse;
        private bool dragging;
        public int CameraRevision { get; private set; }
        public Camera WorldCamera => worldCamera;

        public void Initialize(HeroCamera data)
        {
            worldCamera = GetComponent<Camera>();
            initialPosition = CoordinateConvention.Vector(data.position);
            initialTarget = CoordinateConvention.Vector(data.target);
            up = CoordinateConvention.Vector(data.up);
            originalSpan = spanTarget = data.vertical_span;
            worldCamera.orthographic = true;
            worldCamera.aspect = 1920f / 1080f; // Golden fixture also projects in headless geometry validation.
            worldCamera.nearClipPlane = data.near;
            worldCamera.farClipPlane = data.far;
            ResetView();
        }

        public void ResetView()
        {
            offsetTarget = offsetCurrent = Vector3.zero;
            spanTarget = originalSpan;
            worldCamera.orthographicSize = originalSpan * .5f;
            ApplyTransform();
            CameraRevision++;
        }

        public void Pan(Vector2 pixelDelta)
        {
            var delta = -transform.right * pixelDelta.x - transform.up * pixelDelta.y;
            delta.y = 0;
            offsetTarget += delta * (spanTarget / Mathf.Max(1, Screen.height));
            CameraRevision++;
        }

        public void Zoom(float multiplier)
        {
            spanTarget = Mathf.Clamp(spanTarget * multiplier, 7, 24);
            CameraRevision++;
        }

        private void Update()
        {
            if (worldCamera == null) return;
            if (Input.GetKeyDown(KeyCode.Home)) ResetView();
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            { dragging = true; lastMouse = Input.mousePosition; }
            if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2)) dragging = false;
            if (dragging)
            {
                var mouse = Input.mousePosition;
                Pan(mouse - lastMouse);
                lastMouse = mouse;
            }
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > .01f) Zoom(Mathf.Pow(.9f, scroll));
            var blend = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
            offsetCurrent = Vector3.Lerp(offsetCurrent, offsetTarget, blend);
            worldCamera.orthographicSize = Mathf.Lerp(worldCamera.orthographicSize, spanTarget * .5f, blend);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.position = initialPosition + offsetCurrent;
            transform.rotation = Quaternion.LookRotation(initialTarget - initialPosition, up);
        }
    }
}
