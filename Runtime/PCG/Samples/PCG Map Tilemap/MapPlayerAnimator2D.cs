using UnityEngine;

namespace Islands.PCG.Samples
{
    public class MapPlayerAnimator2D : MonoBehaviour
    {
        public Sprite[] walkDown, walkUp, walkLeft, walkRight;
        public Sprite idleDown, idleUp, idleLeft, idleRight;
        SpriteRenderer sr;
        float frameTimer;
        float frameRate = 0.15f;
        int currentFrame;
        Sprite[] currentAnim;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            currentAnim = walkDown;
        }

        void Update()
        {
            Vector2 input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            bool moving = input.sqrMagnitude > 0;
            if (moving)
            {
                // Elegir dirección (prioridad vertical)
                if (input.y < 0) currentAnim = walkDown;
                else if (input.y > 0) currentAnim = walkUp;
                else if (input.x < 0) currentAnim = walkLeft;
                else if (input.x > 0) currentAnim = walkRight;
                // Ciclar frames
                frameTimer += Time.deltaTime;
                if (frameTimer >= frameRate)
                {
                    frameTimer -= frameRate;
                    currentFrame = (currentFrame + 1) % currentAnim.Length;
                }
                sr.sprite = currentAnim[currentFrame];
            }
            else
            {
                // Idle: mostrar el sprite quieto de la última dirección
                currentFrame = 0;
                frameTimer = 0;
                if (currentAnim == walkDown) sr.sprite = idleDown;
                else if (currentAnim == walkUp) sr.sprite = idleUp;
                else if (currentAnim == walkLeft) sr.sprite = idleLeft;
                else sr.sprite = idleRight;
            }
        }
    }
}
