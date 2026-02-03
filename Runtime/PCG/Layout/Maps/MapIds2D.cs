namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Stable, index-based registry IDs for MaskGrid2D layers in the Map Pipeline.
    /// Keep ordering stable to preserve deterministic stage behavior + test hashes.
    /// </summary>
    public enum MapLayerId : byte
    {
        Land = 0,
        DeepWater = 1,
        ShallowWater = 2,
        HillsL1 = 3,
        HillsL2 = 4,
        Paths = 5,
        Stairs = 6,
        Vegetation = 7,
        Walkable = 8,

        COUNT = 9
    }

    /// <summary>
    /// Stable, index-based registry IDs for ScalarField2D fields in the Map Pipeline.
    /// </summary>
    public enum MapFieldId : byte
    {
        Height = 0,
        Moisture = 1,

        COUNT = 2
    }
}
