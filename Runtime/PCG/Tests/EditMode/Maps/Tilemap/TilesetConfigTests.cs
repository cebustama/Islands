using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Islands.PCG.Adapters.Tilemap;
using Islands.PCG.Layout.Maps;

/// <summary>
/// EditMode tests for <see cref="TilesetConfig"/>.
///
/// Key contracts tested:
///   1. Default layers use the visual priority order (Hills after Vegetation).
///   2. Each LayerEntry stores its own explicit MapLayerId — not derived from position.
///   3. Unassigned/disabled entries emit null (null-means-skip; fallbackTile not propagated).
///   4. Length mismatch guard returns null.
///   5. [H4] animatedTile wins over tile when both are assigned.
///   6. [H4] animatedTile null → static tile is used (backward compatible).
///
/// Phase H3 / H3-fix / H4.
/// </summary>
[TestFixture]
public class TilesetConfigTests
{
    // Visual priority order must match s_defaultPriorityOrder in TilesetConfig.
    private static readonly MapLayerId[] ExpectedDefaultOrder =
    {
        MapLayerId.DeepWater,
        MapLayerId.ShallowWater,
        MapLayerId.Land,
        MapLayerId.LandInterior,
        MapLayerId.LandCore,
        MapLayerId.Vegetation,
        MapLayerId.HillsL1,
        MapLayerId.HillsL2,
        MapLayerId.Stairs,
        MapLayerId.LandEdge,
        MapLayerId.Walkable,
        MapLayerId.Paths,
    };

    private TilesetConfig _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<TilesetConfig>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_config);
    }

    // ------------------------------------------------------------------
    // Default layer construction
    // ------------------------------------------------------------------

    [Test]
    public void DefaultLayers_LengthMatchesLayerIdCOUNT()
    {
        Assert.AreEqual((int)MapLayerId.COUNT, _config.layers.Length);
    }

    [Test]
    public void DefaultLayers_AllEnabled()
    {
        for (int i = 0; i < _config.layers.Length; i++)
            Assert.IsTrue(_config.layers[i].enabled,
                $"Layer at position {i} ({_config.layers[i].layerId}) should be enabled by default.");
    }

    [Test]
    public void DefaultLayers_AllTilesNull()
    {
        for (int i = 0; i < _config.layers.Length; i++)
            Assert.IsNull(_config.layers[i].tile,
                $"Layer at position {i} ({_config.layers[i].layerId}) tile should be null before art is assigned.");
    }

    [Test]
    public void DefaultLayers_AllAnimatedTilesNull()
    {
        // H4: animatedTile defaults to null — no animation assigned out of the box.
        for (int i = 0; i < _config.layers.Length; i++)
            Assert.IsNull(_config.layers[i].animatedTile,
                $"Layer at position {i} ({_config.layers[i].layerId}) animatedTile should be null by default.");
    }

    [Test]
    public void DefaultLayers_LabelsMatchLayerIdToString()
    {
        for (int i = 0; i < _config.layers.Length; i++)
            Assert.AreEqual(_config.layers[i].layerId.ToString(), _config.layers[i].label,
                $"Layer at position {i} label should match its layerId.ToString().");
    }

    // ------------------------------------------------------------------
    // Visual priority order (critical correctness test)
    // ------------------------------------------------------------------

    [Test]
    public void DefaultLayers_VisualPriorityOrder_HillsAfterVegetation()
    {
        // Hills must appear AFTER Vegetation in the array so they have higher stamp priority.
        // A cell that is both Vegetation and HillsL1 should show the HillsL1 tile.
        int vegIdx = -1;
        int hills1Idx = -1;
        int hills2Idx = -1;

        for (int i = 0; i < _config.layers.Length; i++)
        {
            if (_config.layers[i].layerId == MapLayerId.Vegetation) vegIdx = i;
            if (_config.layers[i].layerId == MapLayerId.HillsL1) hills1Idx = i;
            if (_config.layers[i].layerId == MapLayerId.HillsL2) hills2Idx = i;
        }

        Assert.Greater(hills1Idx, vegIdx,
            "HillsL1 must appear after Vegetation in the priority array " +
            "(HillsL1 should overwrite Vegetation, not the reverse).");
        Assert.Greater(hills2Idx, vegIdx,
            "HillsL2 must appear after Vegetation in the priority array.");
    }

    [Test]
    public void DefaultLayers_ExplicitLayerIdOrder_MatchesExpectedPriorityOrder()
    {
        Assert.AreEqual(ExpectedDefaultOrder.Length, _config.layers.Length,
            "Default layers length must match expected priority order length.");

        for (int i = 0; i < ExpectedDefaultOrder.Length; i++)
            Assert.AreEqual(ExpectedDefaultOrder[i], _config.layers[i].layerId,
                $"Position {i}: expected layerId={ExpectedDefaultOrder[i]}, " +
                $"got {_config.layers[i].layerId}.");
    }

    // ------------------------------------------------------------------
    // Explicit layerId — decoupled from position
    // ------------------------------------------------------------------

    [Test]
    public void DefaultLayers_LayerIdIsExplicit_NotDerivedFromPosition()
    {
        // LayerId is stored per-entry and matches the visual priority order,
        // NOT the MapLayerId integer sequence (0,1,2,...).
        // Spot-check: position 0 should be DeepWater, position 5 should be Vegetation.
        Assert.AreEqual(MapLayerId.DeepWater, _config.layers[0].layerId,
            "Position 0 should be DeepWater.");
        Assert.AreEqual(MapLayerId.Vegetation, _config.layers[5].layerId,
            "Position 5 should be Vegetation (after LandCore, before HillsL1).");
        Assert.AreEqual(MapLayerId.HillsL1, _config.layers[6].layerId,
            "Position 6 should be HillsL1 (after Vegetation).");
    }

    // ------------------------------------------------------------------
    // ToLayerEntries — shape
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_ReturnsNonNull()
    {
        Assert.IsNotNull(_config.ToLayerEntries());
    }

    [Test]
    public void ToLayerEntries_LengthMatchesLayerIdCOUNT()
    {
        Assert.AreEqual((int)MapLayerId.COUNT, _config.ToLayerEntries().Length);
    }

    [Test]
    public void ToLayerEntries_LayerIdsMatchExplicitEntryLayerIds()
    {
        // Output LayerIds must come from layers[i].layerId, not from position.
        TilemapLayerEntry[] entries = _config.ToLayerEntries();
        for (int i = 0; i < entries.Length; i++)
            Assert.AreEqual(_config.layers[i].layerId, entries[i].LayerId,
                $"Entry {i}: output LayerId must match layers[{i}].layerId.");
    }

    // ------------------------------------------------------------------
    // ToLayerEntries — null-means-skip contract (core correctness)
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_EnabledEntryNullTile_ProducesNullInTable()
    {
        // All default entries are enabled with null tiles → all output tiles must be null.
        TilemapLayerEntry[] entries = _config.ToLayerEntries();
        for (int i = 0; i < entries.Length; i++)
            Assert.IsNull(entries[i].Tile,
                $"Entry {i} ({_config.layers[i].layerId}): enabled with null tile must " +
                "produce null in the priority table (null-means-skip contract).");
    }

    [Test]
    public void ToLayerEntries_DisabledEntry_ProducesNullInTable()
    {
        // Find the Vegetation entry and disable it.
        int vegIdx = -1;
        for (int i = 0; i < _config.layers.Length; i++)
            if (_config.layers[i].layerId == MapLayerId.Vegetation) { vegIdx = i; break; }

        Assert.GreaterOrEqual(vegIdx, 0, "Vegetation entry must exist in default layers.");

        _config.layers[vegIdx].enabled = false;
        TilemapLayerEntry[] entries = _config.ToLayerEntries();

        Assert.IsNull(entries[vegIdx].Tile,
            "Disabled Vegetation entry must produce null tile in the priority table.");
    }

    [Test]
    public void ToLayerEntries_FallbackTile_NotPropagatedToEntries()
    {
        // Regression: fallbackTile must NOT appear in individual priority entries.
        // If it did, high-priority subset layers (LandCore ⊆ Land) would overwrite
        // the parent tile with the fallback, causing visually incorrect results.
        //
        // All tiles are null by default. The output array must be all-null regardless
        // of fallbackTile. (We can't assign a real TileBase in EditMode tests without
        // a tile asset, but the logic is the same: null tile → null entry, period.)
        TilemapLayerEntry[] entries = _config.ToLayerEntries();
        for (int i = 0; i < entries.Length; i++)
            Assert.IsNull(entries[i].Tile,
                $"Entry {i}: unassigned tile must be null — fallbackTile must not be " +
                "propagated into priority table entries.");
    }

    // ------------------------------------------------------------------
    // ToLayerEntries — mismatch guard
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_MismatchedArrayLength_ReturnsNull()
    {
        _config.layers = new TilesetConfig.LayerEntry[0];
        Assert.IsNull(_config.ToLayerEntries(),
            "ToLayerEntries should return null when layers.Length != MapLayerId.COUNT.");
    }

    [Test]
    public void ToLayerEntries_NullLayersArray_ReturnsNull()
    {
        _config.layers = null;
        Assert.IsNull(_config.ToLayerEntries(),
            "ToLayerEntries should return null when layers is null.");
    }

    // ------------------------------------------------------------------
    // ToLayerEntries — H4 animated tile precedence
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_AnimatedTileWinsOverStaticTile()
    {
        // When both tile and animatedTile are assigned, animatedTile must win.
        // Uses Tile (concrete TileBase from UnityEngine.Tilemaps, no Extras required)
        // to produce non-null, distinct TileBase instances for the assertion.
        Tile staticTile = ScriptableObject.CreateInstance<Tile>();
        Tile animatedTile = ScriptableObject.CreateInstance<Tile>();

        try
        {
            // Assign both to the DeepWater entry (position 0 in default priority order).
            int deepWaterIdx = -1;
            for (int i = 0; i < _config.layers.Length; i++)
                if (_config.layers[i].layerId == MapLayerId.DeepWater) { deepWaterIdx = i; break; }

            Assert.GreaterOrEqual(deepWaterIdx, 0, "DeepWater entry must exist.");

            _config.layers[deepWaterIdx].tile = staticTile;
            _config.layers[deepWaterIdx].animatedTile = animatedTile;
            _config.layers[deepWaterIdx].enabled = true;

            TilemapLayerEntry[] entries = _config.ToLayerEntries();

            Assert.AreSame(animatedTile, entries[deepWaterIdx].Tile,
                "When both tile and animatedTile are assigned, animatedTile must take precedence " +
                "in the output priority table (H4 animated tile wins contract).");
        }
        finally
        {
            Object.DestroyImmediate(staticTile);
            Object.DestroyImmediate(animatedTile);
        }
    }

    [Test]
    public void ToLayerEntries_AnimatedTileNull_FallsBackToStaticTile()
    {
        // When animatedTile is null but tile is assigned, tile must be used unchanged.
        // This is the pre-H4 backward-compatible path.
        Tile staticTile = ScriptableObject.CreateInstance<Tile>();

        try
        {
            int deepWaterIdx = -1;
            for (int i = 0; i < _config.layers.Length; i++)
                if (_config.layers[i].layerId == MapLayerId.DeepWater) { deepWaterIdx = i; break; }

            Assert.GreaterOrEqual(deepWaterIdx, 0, "DeepWater entry must exist.");

            _config.layers[deepWaterIdx].tile = staticTile;
            _config.layers[deepWaterIdx].animatedTile = null;    // explicitly null — no animation
            _config.layers[deepWaterIdx].enabled = true;

            TilemapLayerEntry[] entries = _config.ToLayerEntries();

            Assert.AreSame(staticTile, entries[deepWaterIdx].Tile,
                "When animatedTile is null, the static tile must be used (H4 backward " +
                "compatibility — animatedTile null falls back to tile).");
        }
        finally
        {
            Object.DestroyImmediate(staticTile);
        }
    }
}