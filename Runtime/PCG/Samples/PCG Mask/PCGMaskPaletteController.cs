using UnityEngine;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Simple "scene UI" for the PCG mask demo:
    /// - Camera background color
    /// - Mask 0/1 colors (sent to PCGMaskVisualization -> MaterialPropertyBlock)
    /// Works at runtime and in-editor (ExecuteAlways).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PCGMaskPaletteController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private PCGMaskVisualization visualization;
        [SerializeField] private Camera targetCamera;

        [Header("Background (Camera)")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        [Header("Mask Colors (0/1)")]
        [SerializeField] private Color maskOffColor = Color.black; // value 0
        [SerializeField] private Color maskOnColor = Color.white;  // value 1

        [Header("Apply")]
        [Tooltip("If true, applies every frame (useful while driving via UI sliders).")]
        [SerializeField] private bool applyEveryFrame = true;

        private void Reset()
        {
            visualization = GetComponent<PCGMaskVisualization>();
            targetCamera = Camera.main;
        }

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        private void Update()
        {
            if (applyEveryFrame)
                Apply();
        }

        [ContextMenu("Apply Now")]
        public void Apply()
        {
            if (visualization == null)
                visualization = GetComponent<PCGMaskVisualization>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            // Camera background
            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = backgroundColor;
            }

            // Mask palette -> visualization -> shader MPB
            if (visualization != null)
                visualization.SetMaskColors(maskOffColor, maskOnColor);
        }
    }
}
