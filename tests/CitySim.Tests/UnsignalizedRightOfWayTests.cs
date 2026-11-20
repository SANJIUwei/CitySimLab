namespace CitySim.Tests;

public sealed class UnsignalizedRightOfWayTests
{
    [Fact]
    public void UncontrolledJunctionRequiresStopAndLookEvenWithoutConflicts()
    {
        var context = Context() with { HasStoppedAndLooked = false };
        Assert.Equal(RightOfWayDecision.StopAndLook,
            UnsignalizedRightOfWay.Evaluate(context, Array.Empty<ConflictingMotorVehicle>()));
        Assert.Equal(RightOfWayDecision.Proceed,
            UnsignalizedRightOfWay.Evaluate(context with { HasStoppedAndLooked = true },
                Array.Empty<ConflictingMotorVehicle>()));
    }

    [Fact]
    public void UncontrolledJunctionYieldsToRightAndNotToTheSameConflictFromLeft()
    {
        var context = Context();
        Assert.Equal(RightOfWayDecision.YieldToRight,
            Evaluate(context, MotorVehicleMovement.Straight, RelativeApproach.Right));
        Assert.Equal(RightOfWayDecision.Proceed,
            Evaluate(context, MotorVehicleMovement.Straight, RelativeApproach.Left));
    }

    [Fact]
    public void SignedPriorityIsExplicitAndDoesNotUseUncontrolledRightHandRule()
    {
        var context = Context(JunctionControl.Signed);
        Assert.Equal(RightOfWayDecision.YieldToSignedPriority,
            Evaluate(context, MotorVehicleMovement.Straight, RelativeApproach.Left, SignedPriority.OtherFirst));
        Assert.Equal(RightOfWayDecision.Proceed,
            Evaluate(context, MotorVehicleMovement.Straight, RelativeApproach.Right, SignedPriority.SubjectFirst));
        Assert.Equal(RightOfWayDecision.WaitForUnresolvedConflict,
            Evaluate(context, MotorVehicleMovement.Straight, RelativeApproach.Right));
    }

    [Theory]
    [InlineData(MotorVehicleMovement.Left)]
    [InlineData(MotorVehicleMovement.Right)]
    public void TurningVehicleYieldsToConflictingThroughTraffic(MotorVehicleMovement turn)
    {
        Assert.Equal(RightOfWayDecision.YieldToStraight,
            Evaluate(Context(movement: turn), MotorVehicleMovement.Straight, RelativeApproach.Opposite));
        Assert.Equal(RightOfWayDecision.Proceed,
            Evaluate(Context(), turn, RelativeApproach.Opposite));
    }

    [Fact]
    public void OpposingRightTurnYieldsToLeftTurn()
    {
        Assert.Equal(RightOfWayDecision.YieldToOpposingLeft,
            Evaluate(Context(movement: MotorVehicleMovement.Right), MotorVehicleMovement.Left, RelativeApproach.Opposite));
        Assert.Equal(RightOfWayDecision.Proceed,
            Evaluate(Context(movement: MotorVehicleMovement.Left), MotorVehicleMovement.Right, RelativeApproach.Opposite));
    }

    [Fact]
    public void SimultaneousCompetingClaimsHoldUntilClearNotByAnInventedTieBreak()
    {
        // Through vehicle has a movement claim, while the conflicting turn comes from the right.
        var context = Context();
        Assert.Equal(RightOfWayDecision.WaitForUnresolvedConflict,
            Evaluate(context, MotorVehicleMovement.Left, RelativeApproach.Right));
        Assert.Equal(RightOfWayDecision.Proceed,
            UnsignalizedRightOfWay.Evaluate(context, Array.Empty<ConflictingMotorVehicle>()));
    }

    [Fact]
    public void EnteringRoundaboutYieldsToTrafficAlreadyInside()
    {
        var context = Context() with { IsEnteringRoundabout = true };
        var circulating = new ConflictingMotorVehicle(MotorVehicleMovement.Straight, RelativeApproach.Left, IsCirculating: true);
        Assert.Equal(RightOfWayDecision.YieldToCircle,
            UnsignalizedRightOfWay.Evaluate(context, new[] { circulating }));
        var entering = new ConflictingMotorVehicle(MotorVehicleMovement.Straight, RelativeApproach.Right, IsEnteringRoundabout: true);
        Assert.Equal(RightOfWayDecision.Proceed,
            UnsignalizedRightOfWay.Evaluate(Context() with { IsCirculating = true }, new[] { entering }));
    }

    [Fact]
    public void BlockedExitPreventsEntryEvenWhenNoOtherVehicleHasPriority()
    {
        var context = Context(JunctionControl.Signed) with { ExitIsClear = false };
        Assert.Equal(RightOfWayDecision.WaitForClearExit,
            UnsignalizedRightOfWay.Evaluate(context, Array.Empty<ConflictingMotorVehicle>()));
        Assert.Equal(RightOfWayDecision.Proceed,
            UnsignalizedRightOfWay.Evaluate(context with { ExitIsClear = true }, Array.Empty<ConflictingMotorVehicle>()));
    }

    private static RightOfWayDecision Evaluate(UnsignalizedContext context, MotorVehicleMovement other,
        RelativeApproach approach, SignedPriority priority = SignedPriority.Unspecified) =>
        UnsignalizedRightOfWay.Evaluate(context, new[] { new ConflictingMotorVehicle(other, approach, priority) });

    private static UnsignalizedContext Context(JunctionControl control = JunctionControl.Uncontrolled,
        MotorVehicleMovement movement = MotorVehicleMovement.Straight) =>
        new(control, movement, HasStoppedAndLooked: true, IsEnteringRoundabout: false, ExitIsClear: true);
}
