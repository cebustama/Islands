using UnityEngine;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Minimal 4-directional player controller for the PCG Map Tilemap sample scene.
    ///
    /// Phase H7 — Map Navigation Sample.
    /// Pure sample-side MonoBehaviour. No pipeline dependency. Adapters-last invariant
    /// preserved: this script never reads or writes pipeline state.
    ///
    /// Requirements:
    ///   - Rigidbody2D (Dynamic, GravityScale=0, FreezeRotation Z)
    ///   - CircleCollider2D on the same GameObject
    ///   - Collider Tilemap must exist with CompositeCollider2D (H5 SetupCollider)
    ///   - Physics layer matrix: player layer collides with collider tilemap layer
    ///
    /// Optional:
    ///   - Assign <see cref="spriteRenderer"/> for automatic horizontal flip on East/West input.
    ///   - Assign <see cref="frontSprite"/>/<see cref="backSprite"/> for North/South facing swap.
    ///     When unassigned, facing logic is skipped silently.
    /// </summary>
    [AddComponentMenu("Islands/PCG/Map Player Controller 2D")]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MapPlayerController2D : MonoBehaviour
    {
        // =====================================================================
        // Inspector
        // =====================================================================

        [Header("Movement")]
        [Tooltip("Movement speed in world units per second.")]
        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Facing (optional)")]
        [Tooltip("Assign to enable horizontal flip on East/West movement.\n" +
                 "Leave null to disable facing logic entirely.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Sprite shown when facing South (default facing). Can be null.")]
        [SerializeField] private Sprite frontSprite;

        [Tooltip("Sprite shown when facing North. Can be null.")]
        [SerializeField] private Sprite backSprite;

        // =====================================================================
        // Runtime state
        // =====================================================================

        private Rigidbody2D rb;
        private Vector2 inputDir;
        private Vector2 lastNonZeroDir;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Enforce top-down configuration regardless of Inspector state.
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            lastNonZeroDir = Vector2.down; // default: facing south
        }

        private void Update()
        {
            // Read input every frame for responsiveness; apply in FixedUpdate.
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            inputDir = new Vector2(h, v);

            // Normalize diagonal input so speed is consistent.
            if (inputDir.sqrMagnitude > 1f)
                inputDir.Normalize();

            // Track last non-zero direction for facing.
            if (inputDir.sqrMagnitude > 0.01f)
                lastNonZeroDir = inputDir;
        }

        private void FixedUpdate()
        {
            rb.linearVelocity = inputDir * moveSpeed;
        }

        private void LateUpdate()
        {
            ApplyFacing();
        }

        // =====================================================================
        // Facing
        // =====================================================================

        /// <summary>
        /// Updates SpriteRenderer flip and sprite based on <see cref="lastNonZeroDir"/>.
        /// Silently skipped when <see cref="spriteRenderer"/> is null.
        /// </summary>
        private void ApplyFacing()
        {
            if (spriteRenderer == null)
                return;

            // Horizontal flip: face right by default; flip when moving left.
            if (Mathf.Abs(lastNonZeroDir.x) > 0.01f)
                spriteRenderer.flipX = lastNonZeroDir.x < 0f;

            // Vertical sprite swap (only if both sprites are assigned).
            if (frontSprite != null && backSprite != null)
                spriteRenderer.sprite = lastNonZeroDir.y > 0.1f ? backSprite : frontSprite;
        }
    }
}