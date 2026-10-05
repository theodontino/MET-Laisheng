using System.Collections.Generic;
using UnityEngine;

namespace MET.LivingWorld
{
    public sealed class HeroInteraction : MonoBehaviour
    {
        private HeroWorld world;
        private readonly List<LineRenderer> outlines = new List<LineRenderer>();
        private LineRenderer hoverLine;
        private Material overlayMaterial;
        private bool showGrid, showToolbar = true;
        private string hovered = "", selected = "";
        public string HoveredTile => hovered;
        public string SelectedTile => selected;

        public void Initialize(HeroWorld value)
        {
            world = value;
            var importedMaterial = Resources.Load<Material>("Rendering/HeroOverlay");
            HeroContract.Require(importedMaterial != null, "Generate the serialized official URP overlay material first.");
            overlayMaterial = new Material(importedMaterial) { name = "HexAnalysisOverlay" };
            foreach (var tile in world.Contract.tiles)
            {
                var line = MakeOutline(tile, "Analysis_" + tile.key);
                line.enabled = false;
                outlines.Add(line);
            }
            hoverLine = MakeOutline(world.Contract.tiles[0], "ReadOnlyHover");
            hoverLine.widthMultiplier = .025f;
            hoverLine.enabled = false;
            if (HeroEvidence.HasFlag("--capture-hero")) showToolbar = false;
        }

        private LineRenderer MakeOutline(HeroTile tile, string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = overlayMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = .012f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            SetOutline(line, tile);
            return line;
        }

        private static void SetOutline(LineRenderer line, HeroTile tile)
        {
            var positions = new Vector3[tile.outline.Length];
            for (var index = 0; index < positions.Length; ++index)
                positions[index] = CoordinateConvention.Vector(tile.outline[index]) + Vector3.up * .012f;
            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }

        public void SetAnalysis(bool enabled)
        {
            showGrid = enabled;
            foreach (var line in outlines) line.enabled = enabled;
        }

        public string PickTile(Vector2 screenPosition)
        {
            var ray = world.CameraController.WorldCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 200, 1 << HeroWorld.GroundLayer)) return "";
            var nearest = float.PositiveInfinity;
            var key = "";
            foreach (var tile in world.Contract.tiles)
            {
                var center = CoordinateConvention.Vector(tile.center);
                var delta = new Vector2(center.x - hit.point.x, center.z - hit.point.z);
                if (delta.sqrMagnitude < nearest)
                { nearest = delta.sqrMagnitude; key = tile.key; }
            }
            return nearest <= 1.3f * 1.3f ? key : "";
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) SetAnalysis(!showGrid);
            if (Input.GetKeyDown(KeyCode.F1)) showToolbar = !showToolbar;
            var pointer = Input.mousePosition;
            if (showToolbar && pointer.y >= Screen.height - 72) return;
            hovered = PickTile(pointer);
            hoverLine.enabled = hovered != "" && !HeroEvidence.HasFlag("--capture-hero");
            if (hoverLine.enabled)
                foreach (var tile in world.Contract.tiles)
                    if (tile.key == hovered) { SetOutline(hoverLine, tile); break; }
            if (Input.GetMouseButtonDown(0)) selected = hovered;
        }

        private void OnGUI()
        {
            if (!showToolbar || world == null) return;
            GUI.Box(new Rect(16, 16, 750, 58), "");
            GUI.Label(new Rect(28, 21, 720, 22),
                "MET · Hero Valley | Unity URP | Read-only visual migration, no gameplay or save writes");
            if (GUI.Button(new Rect(28, 47, 125, 22), showGrid ? "Hide Hex (H)" : "Show Hex (H)"))
                SetAnalysis(!showGrid);
            if (GUI.Button(new Rect(164, 47, 110, 22), "Reset (Home)")) world.CameraController.ResetView();
            GUI.Label(new Rect(286, 47, 470, 22),
                "Middle/right drag · wheel zoom · F1 hide | Hover " + hovered + " · selected " + selected);
        }

        private void OnDestroy()
        {
            if (overlayMaterial != null) Destroy(overlayMaterial);
        }
    }
}
