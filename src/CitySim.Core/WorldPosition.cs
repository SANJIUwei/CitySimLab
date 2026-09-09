using System;
public class World
{
    public struct WorldPosition
    {
        public float x;
        public float y;
        public float z;
    }
    public double Distance(WorldPosition a,WorldPosition b)
    {
        if (a.x == b.x && a.y == b.y && a.z == b.z) return 0;//边界处理，节省性能。
        double distance;//这是实际上距离，但这仅仅是3D位置的距离，不是路径距离。
        double dx = a.x - b.x;
        double dy = a.y - b.y;
        double dz = a.z - b.z;
        distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return distance;
    }

}

