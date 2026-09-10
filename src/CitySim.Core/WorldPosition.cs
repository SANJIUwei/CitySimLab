using System;

// 坐标和直线距离先放这里，避免路网类型一上来去依赖 Unity 或整座城市对象。
public class World
{
    // 建筑和路点共用世界坐标，不绑在道路格子上，方便以后自由摆放再谈接入。
    public struct WorldPosition
    {
        public float x;
        public float y;
        public float z;
    }

    // 只算空间直线距离。沿路多远要等有路段之后再算，这里不能当路径长度用。
    public double Distance(WorldPosition a, WorldPosition b)
    {
        // 逐分量 == 只覆盖“同一赋值”的点；独立算出来的 float 即使看起来该相等也可能走不到这支。
        if (a.x == b.x && a.y == b.y && a.z == b.z) return 0;
        double dx = a.x - b.x;
        double dy = a.y - b.y;
        double dz = a.z - b.z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
