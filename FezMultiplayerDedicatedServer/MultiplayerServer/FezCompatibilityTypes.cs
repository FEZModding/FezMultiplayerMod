using System;

namespace FezMultiplayerDedicatedServer
{
    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }
        public override string ToString()
        {
            string separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
            return $"<{X}{separator} {Y}{separator} {this.Z}>";
        }
        public Vector3 Round(int d)
        {
            return new Vector3((float)Math.Round(X, d), (float)Math.Round(Y, d), (float)Math.Round(Z, d));
        }
    }
    public struct TrileEmplacement
    {
        public int X, Y, Z;
        public TrileEmplacement(int x, int y, int z)
        {
            X = x; Y = y; Z = z;
        }
        public override string ToString()
        {
            string separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
            return $"<{X}{separator} {Y}{separator} {this.Z}>";
        }
    }
    public enum HorizontalDirection
    {
        None,
        Left,
        Right
    }
    public enum Viewpoint
    {
        None,
        Front,
        Right,
        Back,
        Left,
        Up,
        Down,
        Perspective
    }
    public enum ActorType
    {
        //TODO? only used for treasure actor type
    }
    public enum ActionType
    {
        None,
        Idle,
        LookingLeft,
        LookingRight,
        LookingUp,
        LookingDown,
        Walking,
        Running,
        Jumping,
        FrontClimbingLadder,
        BackClimbingLadder,
        SideClimbingLadder,
        CarryIdle,
        CarryWalk,
        CarryJump,
        CarrySlide,
        CarryEnter,
        CarryHeavyIdle,
        CarryHeavyWalk,
        CarryHeavyJump,
        CarryHeavySlide,
        CarryHeavyEnter,
        DropTrile,
        DropHeavyTrile,
        Throwing,
        ThrowingHeavy,
        Lifting,
        LiftingHeavy,
        Dying,
        Suffering,
        Falling,
        Bouncing,
        Flying,
        Dropping,
        Sliding,
        Landing,
        ReadingSign,
        FreeFalling,
        CollectingFez,
        Victory,
        EnteringDoor,
        Grabbing,
        Pushing,
        SuckedIn,
        FrontClimbingVine,
        FrontClimbingVineSideways,
        SideClimbingVine,
        BackClimbingVine,
        BackClimbingVineSideways,
        WakingUp,
        OpeningTreasure,
        OpeningDoor,
        WalkingTo,
        Treading,
        Swimming,
        Sinking,
        Teetering,
        HurtSwim,
        EnteringTunnel,
        PushingPivot,
        EnterDoorSpin,
        EnterDoorSpinCarry,
        EnterDoorSpinCarryHeavy,
        EnterTunnelCarry,
        EnterTunnelCarryHeavy,
        RunTurnAround,
        FindingTreasure,
        IdlePlay,
        IdleSleep,
        IdleLookAround,
        PullUpCornerLedge,
        LowerToCornerLedge,
        GrabCornerLedge,
        GrabLedgeFront,
        GrabLedgeBack,
        PullUpFront,
        PullUpBack,
        LowerToLedge,
        ShimmyFront,
        ShimmyBack,
        ToCornerFront,
        ToCornerBack,
        FromCornerBack,
        IdleToClimb,
        IdleToFrontClimb,
        IdleToSideClimb,
        JumpToClimb,
        JumpToSideClimb,
        ClimbOverLadder,
        GrabTombstone,
        PivotTombstone,
        LetGoOfTombstone,
        EnteringPipe,
        ExitDoor,
        ExitDoorCarry,
        ExitDoorCarryHeavy,
        LesserWarp,
        GateWarp,
        SleepWake,
        ReadTurnAround,
        EndReadTurnAround,
        TurnToBell,
        HitBell,
        TurnAwayFromBell,
        CrushHorizontal,
        CrushVertical,
        DrumsIdle,
        DrumsCrash,
        DrumsTom,
        DrumsTom2,
        DrumsToss,
        DrumsTwirl,
        DrumsHiHat,
        VictoryForever,
        Floating,
        Standing,
        StandWinking,
        IdleYawn
    }
}