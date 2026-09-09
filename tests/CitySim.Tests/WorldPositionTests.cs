namespace CitySim.Tests;

public sealed class WorldPositionTests
{
    // 3-4-5 直角三角形：只验证三维直线距离，不是道路上的路径距离。
    [Fact]
    public void Distance_FromOriginToThreeFourZero_IsFive()
    {
        var world = new World();
        var a = new World.WorldPosition { x = 0f, y = 0f, z = 0f };
        var b = new World.WorldPosition { x = 3f, y = 4f, z = 0f };

        double actual = world.Distance(a, b);

        // 第三参数是小数精度位数，避免浮点开方的细微误差。
        Assert.Equal(5.0, actual, 5);
    }

    // 两点坐标完全相同时，Distance 应直接得到 0。
    [Fact]
    public void Distance_WhenPointsAreIdentical_IsZero()
    {
        var world = new World();
        var a = new World.WorldPosition { x = 1.5f, y = -2f, z = 8f };
        var b = new World.WorldPosition { x = 1.5f, y = -2f, z = 8f };

        double actual = world.Distance(a, b);

        Assert.Equal(0.0, actual, 5);
    }
}
