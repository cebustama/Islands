namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// A single 2×2 mega-tile placement in pipeline coordinates.
    /// Origin is the bottom-left cell of the block (y=0 at bottom).
    /// Phase H8.
    /// </summary>
    public readonly struct MegaTilePlacement
    {
        /// <summary>Bottom-left X of the 2×2 block (pipeline coords).</summary>
        public readonly int X;
        /// <summary>Bottom-left Y of the 2×2 block (pipeline coords).</summary>
        public readonly int Y;
        /// <summary>Index into the MegaTileRule array that produced this placement.</summary>
        public readonly int RuleIndex;

        public MegaTilePlacement(int x, int y, int ruleIndex)
        {
            X = x;
            Y = y;
            RuleIndex = ruleIndex;
        }

        public override string ToString() => $"MegaTile({X},{Y} rule={RuleIndex})";
    }
}