using System;

namespace CitySim.Core;

public struct WorldPosition
{
    public float x;
    public float y;
    public float z;

    public static double Distance(WorldPosition a, WorldPosition b)
    {
        if (a.x == b.x && a.y == b.y && a.z == b.z) return 0;
        double dx = a.x - b.x;
        double dy = a.y - b.y;
        double dz = a.z - b.z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
