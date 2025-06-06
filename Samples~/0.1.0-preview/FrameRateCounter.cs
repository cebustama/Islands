using TMPro;
using UnityEngine;

namespace Islands
{
    public class FrameRateCounter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI display;

        public enum DisplayMode { FPS, MS }
        [SerializeField] private DisplayMode displayMode = DisplayMode.FPS;

        [SerializeField, Range(0.1f, 2f)] private float sampleDuration = 1f;

        private int frames;
        private float duration, bestDuration = float.MaxValue, worstDuration;

        private void Update()
        {
            float frameDuration = Time.unscaledDeltaTime;
            frames++;
            duration += frameDuration;

            if (frameDuration < bestDuration)
            {
                bestDuration = frameDuration;
            }

            if (frameDuration > worstDuration)
            {
                worstDuration = frameDuration;
            }

            if (duration > sampleDuration)
            {
                if (displayMode == DisplayMode.FPS)
                {
                    display.SetText(
                        "FPS\n{0:0}\n{1:0}\n{2:0}",
                        1f / bestDuration,
                        frames / duration,
                        1f / worstDuration
                    );
                }
                else if (displayMode == DisplayMode.MS)
                {
                    display.SetText(
                        "MS\n{0:1}\n{1:1}\n{2:1}",
                        bestDuration * 1000f,
                        duration * 1000f / frames,
                        worstDuration * 1000f
                    );
                }

                frames = 0;
                duration = 0f;
                bestDuration = float.MaxValue;
                worstDuration = 0f;
            }
        }
    }
}