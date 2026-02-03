using System;
using Unity.Collections;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Deterministic runner for a sequence of IMapStage2D stages.
    /// Execution order is the array order (stable).
    /// </summary>
    public static class MapPipelineRunner2D
    {
        public static void Run(
            ref MapContext2D ctx,
            in MapInputs inputs,
            IMapStage2D[] stages,
            bool clearLayers = true)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (stages == null) throw new ArgumentNullException(nameof(stages));

            ctx.BeginRun(in inputs, clearLayers: clearLayers);

            for (int i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                if (stage == null)
                    throw new ArgumentException($"Stage at index {i} is null.", nameof(stages));

                stage.Execute(ref ctx, in inputs);
            }
        }

        public static MapContext2D RunNew(
            in MapInputs inputs,
            IMapStage2D[] stages,
            Allocator allocator,
            bool clearLayers = true)
        {
            var ctx = new MapContext2D(inputs.Domain, allocator);
            Run(ref ctx, in inputs, stages, clearLayers);
            return ctx;
        }
    }
}
