using Unity.Mathematics;

namespace Islands
{
    public static partial class Noise
    {

        public interface IVoronoiFunction
        {
            Sample4 Evaluate(VoronoiData data);
        }

        public struct F1 : IVoronoiFunction
        {

            public Sample4 Evaluate(VoronoiData data) => data.a;
        }

        public struct F2 : IVoronoiFunction
        {

            public Sample4 Evaluate(VoronoiData data) => data.b;
        }

        public struct F2MinusF1 : IVoronoiFunction
        {

            public Sample4 Evaluate(VoronoiData data) => data.b - data.a;
        }

        /// <summary>Turns smooth-F1 into a 0-1 island (plateau) height-field.</summary>
        public readonly struct CellAsIslands : IVoronoiFunction
        {
            public Sample4 Evaluate(VoronoiData data)
            {
                // start from the already-smoothed F1 in data.a
                Sample4 s = data.a;

                // invert value and flip derivatives so the gradient
                // still points uphill toward the cell centre
                s.v = 1f - s.v;
                s.dx = -s.dx;
                s.dy = -s.dy;
                s.dz = -s.dz;
                return s;
            }
        }
    }
}