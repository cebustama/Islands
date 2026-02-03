namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Deterministic stage contract for Map Pipeline by Layers.
    /// Stages must:
    /// - read/write only MapContext2D + MapInputs
    /// - avoid nondeterministic iteration sources (HashSet/Dictionary traversal)
    /// - use ctx.Rng (Unity.Mathematics.Random) for any randomness
    /// </summary>
    public interface IMapStage2D
    {
        string Name { get; }

        void Execute(ref MapContext2D ctx, in MapInputs inputs);
    }
}
