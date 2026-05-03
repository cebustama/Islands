using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Islands.PCG.Adapters.Tilemap;
using Islands.PCG.Layout.Maps;

/// <summary>
/// EditMode tests for <see cref="TilesetConfig"/>.
/// Phase H3 / H3-fix / H4 / H6 / F4c.
/// </summary>
[TestFixture]
public class TilesetConfigTests
{
    // Phase L: order updated to 15 entries — Lakes inserted after DeepWater,
    // Rivers inserted between HillsL2 and Stairs (matches s_defaultPriorityOrder
    // in TilesetConfig.cs).
    private static readonly MapLayerId[] ExpectedDefaultOrder =
    {
        MapLayerId.DeepWater,       // 0
        MapLayerId.Lakes,           // 1 — Phase L
        MapLayerId.MidWater,        // 2 — F4c
        MapLayerId.ShallowWater,    // 3
        MapLayerId.Land,            // 4
        MapLayerId.LandInterior,    // 5
        MapLayerId.LandCore,        // 6
        MapLayerId.Vegetation,      // 7
        MapLayerId.HillsL1,         // 8
        MapLayerId.HillsL2,         // 9
        MapLayerId.Rivers,          // 10 — Phase L
        MapLayerId.Stairs,          // 11
        MapLayerId.LandEdge,        // 12
        MapLayerId.Walkable,        // 13
        MapLayerId.Paths,           // 14
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
        for (int i = 0; i < _config.layers.Length; i++)
            Assert.IsNull(_config.layers[i].animatedTile,
                $"Layer at position {i} ({_config.layers[i].layerId}) animatedTile should be null by default.");
    }

    [Test]
    public void DefaultLayers_AllRuleTilesNull()
    {
        for (int i = 0; i < _config.layers.Length; i++)
            Assert.IsNull(_config.layers[i].ruleTile,
                $"Layer at position {i} ({_config.layers[i].layerId}) ruleTile should be null by default.");
    }

    [Test]
    public void DefaultLayers_LabelsMatchLayerIdToString()
    {
        for (int i = 0; i < _config.layers.Length; i++)
            Assert.AreEqual(_config.layers[i].layerId.ToString(), _config.layers[i].label,
                $"Layer at position {i} label should match its layerId.ToString().");
    }

    // ------------------------------------------------------------------
    // Visual priority order
    // ------------------------------------------------------------------

    [Test]
    public void DefaultLayers_VisualPriorityOrder_HillsAfterVegetation()
    {
        int vegIdx = -1, hills1Idx = -1, hills2Idx = -1;
        for (int i = 0; i < _config.layers.Length; i++)
        {
            if (_config.layers[i].layerId == MapLayerId.Vegetation) vegIdx = i;
            if (_config.layers[i].layerId == MapLayerId.HillsL1) hills1Idx = i;
            if (_config.layers[i].layerId == MapLayerId.HillsL2) hills2Idx = i;
        }
        Assert.Greater(hills1Idx, vegIdx, "HillsL1 must appear after Vegetation.");
        Assert.Greater(hills2Idx, vegIdx, "HillsL2 must appear after Vegetation.");
    }

    [Test]
    public void DefaultLayers_VisualPriorityOrder_MidWaterBetweenDeepAndShallow()
    {
        // F4c: MidWater must sit between DeepWater and ShallowWater in priority.
        int deepIdx = -1, midIdx = -1, shallowIdx = -1;
        for (int i = 0; i < _config.layers.Length; i++)
        {
            if (_config.layers[i].layerId == MapLayerId.DeepWater) deepIdx = i;
            if (_config.layers[i].layerId == MapLayerId.MidWater) midIdx = i;
            if (_config.layers[i].layerId == MapLayerId.ShallowWater) shallowIdx = i;
        }
        Assert.Greater(midIdx, deepIdx, "MidWater must appear after DeepWater.");
        Assert.Greater(shallowIdx, midIdx, "ShallowWater must appear after MidWater.");
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
    // Explicit layerId
    // ------------------------------------------------------------------

    [Test]
    public void DefaultLayers_LayerIdIsExplicit_NotDerivedFromPosition()
    {
        // Phase L: positional layout updated. New positions:
        //   0=DeepWater, 1=Lakes, 2=MidWater, 3=ShallowWater, 4=Land, ...
        Assert.AreEqual(MapLayerId.DeepWater, _config.layers[0].layerId, "Position 0 should be DeepWater.");
        Assert.AreEqual(MapLayerId.Lakes, _config.layers[1].layerId, "Position 1 should be Lakes (Phase L).");
        Assert.AreEqual(MapLayerId.MidWater, _config.layers[2].layerId, "Position 2 should be MidWater.");
        Assert.AreEqual(MapLayerId.Vegetation, _config.layers[7].layerId, "Position 7 should be Vegetation.");
        Assert.AreEqual(MapLayerId.Rivers, _config.layers[10].layerId, "Position 10 should be Rivers (Phase L).");
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
        // Phase L: ToLayerEntries() returns an array indexed by (int)MapLayerId,
        // NOT by _config.layers[i] position. So the contract is:
        //   entries[(int)id].LayerId == id  for every id in 0..COUNT.
        // This guarantees a layer's tile is always findable by its enum value,
        // regardless of how the layers array is ordered or sized.
        TilemapLayerEntry[] entries = _config.ToLayerEntries();
        for (int i = 0; i < entries.Length; i++)
            Assert.AreEqual((MapLayerId)i, entries[i].LayerId,
                $"Entry {i}: output LayerId must equal (MapLayerId){i}.");
    }

    // ------------------------------------------------------------------
    // ToLayerEntries — null-means-skip
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_EnabledEntryNullTile_ProducesNullInTable()
    {
        TilemapLayerEntry[] entries = _config.ToLayerEntries();
        for (int i = 0; i < entries.Length; i++)
            Assert.IsNull(entries[i].Tile,
                $"Entry {i} ({_config.layers[i].layerId}): enabled with null tile must produce null.");
    }

    [Test]
    public void ToLayerEntries_DisabledEntry_ProducesNullInTable()
    {
        int vegIdx = -1;
        for (int i = 0; i < _config.layers.Length; i++)
            if (_config.layers[i].layerId == MapLayerId.Vegetation) { vegIdx = i; break; }
        Assert.GreaterOrEqual(vegIdx, 0);

        _config.layers[vegIdx].enabled = false;
        Assert.IsNull(_config.ToLayerEntries()[vegIdx].Tile,
            "Disabled entry must produce null tile.");
    }

    [Test]
    public void ToLayerEntries_FallbackTile_NotPropagatedToEntries()
    {
        TilemapLayerEntry[] entries = _config.ToLayerEntries();
        for (int i = 0; i < entries.Length; i++)
            Assert.IsNull(entries[i].Tile,
                $"Entry {i}: unassigned tile must be null — fallbackTile must not be propagated.");
    }

    // ------------------------------------------------------------------
    // ToLayerEntries — mismatch guard
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_MismatchedArrayLength_ReturnsNull()
    {
        _config.layers = new TilesetConfig.LayerEntry[0];
        Assert.IsNull(_config.ToLayerEntries());
    }

    [Test]
    public void ToLayerEntries_NullLayersArray_ReturnsNull()
    {
        _config.layers = null;
        Assert.IsNull(_config.ToLayerEntries());
    }

    // ------------------------------------------------------------------
    // H4 — animated tile precedence
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_AnimatedTileWinsOverStaticTile()
    {
        // Phase L: entries are indexed by (int)MapLayerId, so use the enum
        // value directly to look up the result — NOT the position in _config.layers.
        Tile staticTile = ScriptableObject.CreateInstance<Tile>();
        Tile animatedTile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            int idx = FindEntry(MapLayerId.DeepWater);
            _config.layers[idx].tile = staticTile;
            _config.layers[idx].animatedTile = animatedTile;
            _config.layers[idx].enabled = true;
            Assert.AreSame(animatedTile, _config.ToLayerEntries()[(int)MapLayerId.DeepWater].Tile,
                "animatedTile must take precedence over tile.");
        }
        finally { Object.DestroyImmediate(staticTile); Object.DestroyImmediate(animatedTile); }
    }

    [Test]
    public void ToLayerEntries_AnimatedTileNull_FallsBackToStaticTile()
    {
        Tile staticTile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            int idx = FindEntry(MapLayerId.DeepWater);
            _config.layers[idx].tile = staticTile;
            _config.layers[idx].animatedTile = null;
            _config.layers[idx].enabled = true;
            Assert.AreSame(staticTile, _config.ToLayerEntries()[(int)MapLayerId.DeepWater].Tile,
                "When animatedTile is null, static tile must be used.");
        }
        finally { Object.DestroyImmediate(staticTile); }
    }

    // ------------------------------------------------------------------
    // H6 — rule tile precedence
    // ------------------------------------------------------------------

    [Test]
    public void ToLayerEntries_RuleTileWinsOverAnimatedAndStaticTile()
    {
        Tile s = ScriptableObject.CreateInstance<Tile>();
        Tile a = ScriptableObject.CreateInstance<Tile>();
        Tile r = ScriptableObject.CreateInstance<Tile>();
        try
        {
            int idx = FindEntry(MapLayerId.ShallowWater);
            _config.layers[idx].tile = s;
            _config.layers[idx].animatedTile = a;
            _config.layers[idx].ruleTile = r;
            _config.layers[idx].enabled = true;
            Assert.AreSame(r, _config.ToLayerEntries()[(int)MapLayerId.ShallowWater].Tile,
                "ruleTile must take precedence over animatedTile and tile.");
        }
        finally { Object.DestroyImmediate(s); Object.DestroyImmediate(a); Object.DestroyImmediate(r); }
    }

    [Test]
    public void ToLayerEntries_RuleTileNull_FallsBackToAnimatedTile()
    {
        Tile s = ScriptableObject.CreateInstance<Tile>();
        Tile a = ScriptableObject.CreateInstance<Tile>();
        try
        {
            int idx = FindEntry(MapLayerId.ShallowWater);
            _config.layers[idx].tile = s;
            _config.layers[idx].animatedTile = a;
            _config.layers[idx].ruleTile = null;
            _config.layers[idx].enabled = true;
            Assert.AreSame(a, _config.ToLayerEntries()[(int)MapLayerId.ShallowWater].Tile,
                "When ruleTile is null, animatedTile must win.");
        }
        finally { Object.DestroyImmediate(s); Object.DestroyImmediate(a); }
    }

    [Test]
    public void ToLayerEntries_DisabledEntry_RuleTileIgnored()
    {
        Tile r = ScriptableObject.CreateInstance<Tile>();
        try
        {
            int idx = FindEntry(MapLayerId.ShallowWater);
            _config.layers[idx].ruleTile = r;
            _config.layers[idx].enabled = false;
            Assert.IsNull(_config.ToLayerEntries()[idx].Tile,
                "Disabled entry with ruleTile must still produce null.");
        }
        finally { Object.DestroyImmediate(r); }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private int FindEntry(MapLayerId id)
    {
        for (int i = 0; i < _config.layers.Length; i++)
            if (_config.layers[i].layerId == id) return i;
        Assert.Fail($"{id} entry not found in default layers.");
        return -1;
    }
}