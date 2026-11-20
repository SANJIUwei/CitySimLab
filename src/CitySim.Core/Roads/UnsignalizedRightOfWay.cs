using System;
using System.Collections.Generic;

namespace CitySim.Core;

public enum JunctionControl { Uncontrolled, Signed }
public enum MotorVehicleMovement { Straight, Left, Right, Other }
public enum RelativeApproach { Unknown, Left, Right, Opposite }
public enum SignedPriority { Unspecified, SubjectFirst, OtherFirst }

// These outcomes describe whether to enter now. None is a permanent routing restriction.
public enum RightOfWayDecision
{
    Proceed,
    StopAndLook,
    WaitForClearExit,
    YieldToCircle,
    YieldToSignedPriority,
    YieldToRight,
    YieldToStraight,
    YieldToOpposingLeft,
    WaitForUnresolvedConflict
}

public readonly record struct UnsignalizedContext(
    JunctionControl Control,
    MotorVehicleMovement Movement,
    bool HasStoppedAndLooked,
    bool IsEnteringRoundabout,
    bool ExitIsClear,
    bool IsCirculating = false);

public readonly record struct ConflictingMotorVehicle(
    MotorVehicleMovement Movement,
    RelativeApproach Approach,
    SignedPriority Priority = SignedPriority.Unspecified,
    bool IsCirculating = false,
    bool IsEnteringRoundabout = false);

// Mainland China implementation regulation arts. 51(ii), 52, and 53.
// Caller supplies only active trajectory conflicts, not simply nearby traffic.
// Does not apply to junctions directed by police or signals, or set movement legality.
public static class UnsignalizedRightOfWay
{
    public static RightOfWayDecision Evaluate(UnsignalizedContext context,
        IEnumerable<ConflictingMotorVehicle> conflicts)
    {
        if (conflicts == null)
            throw new ArgumentNullException(nameof(conflicts));
        if (!context.ExitIsClear)
            return RightOfWayDecision.WaitForClearExit;
        if (context.Control == JunctionControl.Uncontrolled && !context.HasStoppedAndLooked)
            return RightOfWayDecision.StopAndLook;

        var obligations = RightOfWayDecision.Proceed;
        bool unresolved = false;
        foreach (var other in conflicts)
        {
            var decision = EvaluateConflict(context, other);
            if (decision == RightOfWayDecision.WaitForUnresolvedConflict)
                unresolved = true;
            else if (decision != RightOfWayDecision.Proceed &&
                     (obligations == RightOfWayDecision.Proceed || decision < obligations))
                obligations = decision; // stable diagnostic only; any obligation means wait
        }
        return unresolved ? RightOfWayDecision.WaitForUnresolvedConflict : obligations;
    }

    static RightOfWayDecision EvaluateConflict(UnsignalizedContext context, ConflictingMotorVehicle other)
    {
        // Explicit entering/circulating positions take precedence over an approach
        // direction that is not meaningful for a vehicle already in the roundabout.
        if (context.IsEnteringRoundabout && other.IsCirculating)
            return RightOfWayDecision.YieldToCircle;
        if (context.IsCirculating && other.IsEnteringRoundabout)
            return RightOfWayDecision.Proceed;

        bool subjectHasClaim = false;
        var obligation = RightOfWayDecision.Proceed;
        if (context.Control == JunctionControl.Signed)
        {
            subjectHasClaim = other.Priority == SignedPriority.SubjectFirst;
            if (other.Priority == SignedPriority.OtherFirst)
                obligation = RightOfWayDecision.YieldToSignedPriority;
        }
        else if (context.Control == JunctionControl.Uncontrolled)
        {
            subjectHasClaim = other.Approach == RelativeApproach.Left;
            if (other.Approach == RelativeApproach.Right)
                obligation = RightOfWayDecision.YieldToRight;
        }

        if (IsTurning(context.Movement) && other.Movement == MotorVehicleMovement.Straight)
        {
            if (obligation == RightOfWayDecision.Proceed)
                obligation = RightOfWayDecision.YieldToStraight;
        }
        else if (context.Movement == MotorVehicleMovement.Straight && IsTurning(other.Movement))
        {
            subjectHasClaim = true;
        }

        if (other.Approach == RelativeApproach.Opposite)
        {
            if (context.Movement == MotorVehicleMovement.Right &&
                other.Movement == MotorVehicleMovement.Left &&
                obligation == RightOfWayDecision.Proceed)
            {
                obligation = RightOfWayDecision.YieldToOpposingLeft;
            }
            else if (context.Movement == MotorVehicleMovement.Left &&
                     other.Movement == MotorVehicleMovement.Right)
            {
                subjectHasClaim = true;
            }
        }

        // No first-to-arrive or road-grade tiebreaker. An unknown or contradictory
        // simultaneous claim is a hold until the conflicting traffic clears.
        if (subjectHasClaim)
            return obligation == RightOfWayDecision.Proceed
                ? RightOfWayDecision.Proceed
                : RightOfWayDecision.WaitForUnresolvedConflict;
        return obligation == RightOfWayDecision.Proceed
            ? RightOfWayDecision.WaitForUnresolvedConflict
            : obligation;
    }

    static bool IsTurning(MotorVehicleMovement movement) =>
        movement is MotorVehicleMovement.Left or MotorVehicleMovement.Right;
}
