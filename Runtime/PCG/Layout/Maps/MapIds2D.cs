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

        // F3 append-only additions (do not reorder existing IDs)
        LandEdge = 9,
        LandInterior = 10,

        // Phase G append-only additions (do not reorder existing IDs)
        LandCore = 11,

        // Phase F4c append-only additions (do not reorder existing IDs)
        MidWater = 12,

        COUNT = 13
    }

    /// <summary>
    /// Stable, index-based registry IDs for ScalarField2D fields in the Map Pipeline.
    /// </summary>
    public enum MapFieldId : byte
    {
        Height = 0,
        Moisture = 1,

        // Phase G append-only additions (do not reorder existing IDs)
        CoastDist = 2,

        COUNT = 3
    }
}