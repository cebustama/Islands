using UnityEngine;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;

public class MaskGrid2DBooleanOpsSmokeTest : MonoBehaviour
{
    private void Start()
    {
        var domain = new GridDomain2D(10, 10); // 100 bits => tail bits exist (2 words)
        var a = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var b = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
        var r = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

        // Tail-bit determinism check
        a.Fill(true);
        Debug.Assert(a.CountOnes() == 100, $"Fill(true) should set exactly 100 bits, got {a.CountOnes()}");

        a.Clear();
        b.Clear();

        // a: 5 cells along row y=0 (x=0..4)
        for (int x = 0; x < 5; x++) a.Set(x, 0, true);

        // b: 5 cells along column x=0 (y=0..4)
        for (int y = 0; y < 5; y++) b.Set(0, y, true);

        // Union: 5 + 5 - overlap(1) = 9
        r.CopyFrom(a);
        r.Or(b);
        Debug.Assert(r.CountOnes() == 9, $"Union expected 9, got {r.CountOnes()}");

        // Intersect: only (0,0)
        r.CopyFrom(a);
        r.And(b);
        Debug.Assert(r.CountOnes() == 1, $"Intersect expected 1, got {r.CountOnes()}");

        // Subtract: remove overlap from a => 4
        r.CopyFrom(a);
        r.AndNot(b);
        Debug.Assert(r.CountOnes() == 4, $"Subtract expected 4, got {r.CountOnes()}");

        Debug.Log("[MaskGrid2D] Boolean ops smoke test passed.");

        a.Dispose();
        b.Dispose();
        r.Dispose();
    }
}
