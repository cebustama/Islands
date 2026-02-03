using System;
using Islands.PCG.Core;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Immutable inputs for a MapPipeline run.
    /// Determinism rule: Seed is always sanitized to >= 1.
    /// </summary>
    public readonly struct MapInputs
    {
        public readonly uint Seed;
        public readonly GridDomain2D Domain;
        public readonly MapTunables2D Tunables;

        public MapInputs(uint seed, GridDomain2D domain, MapTunables2D tunables)
        {
            if (domain.Width <= 0 || domain.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(domain), "Domain must be >= 1x1.");

            Seed = (seed == 0u) ? 1u : seed;
            Domain = domain;
            Tunables = tunables; // already clamps deterministically in its ctor
        }

        public static MapInputs Create(uint seed, GridDomain2D domain) =>
            new MapInputs(seed, domain, MapTunables2D.Default);
    }
}
