using ClaudePet.Art;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Les poses de Pet pour chaque état (images clés en pixels art).
internal sealed partial class Pet
{
    /// <summary>
    /// Pose à afficher. <paramref name="clipY"/> : ligne écran sous laquelle rien n'est dessiné (creuser), ou NaN.
    /// </summary>
    public PetPose GetPose(out double clipY)
    {
        clipY = double.NaN;
        PetPose p;
        switch (State)
        {
            case St.Walk: p = WalkPose(); break;
            case St.Sit: p = SitPose(); break;
            case St.Stretch: p = StretchPose(); break;
            case St.JumpPrep: p = CrouchPose(); break;
            case St.Air: p = AirPose(); break;
            case St.Land: p = LandPose(); break;
            case St.Stunned: p = StunPose(); break;
            case St.GetUp: p = GetUpPose(); break;
            case St.Dragged: p = DragPose(); break;
            case St.ExitDoor:
            case St.HomeJump:
            case St.EnterDoor:
            case St.Dig:
            case St.Emerge:
                p = HomePose(out clipY);
                break;
            case St.Tapped:
            case St.HangCursor:
            case St.ClimbCursor:
            case St.SitCursor:
            case St.ReachUp:
            case St.Sleep:
            case St.WakeUp:
                p = MousePose();
                break;
            case St.MouthOpen:
            case St.Eat:
                p = FeedPose();
                break;
            case St.Climb:
                p = ClimbPose();
                break;
            case St.Petted:
                p = PettedPose();
                break;
            case St.Coffee:
            case St.Laptop:
            case St.Music:
            case St.Read:
            case St.Bubbles:
                p = ActivityPose();
                break;
            default: p = IdlePose(); break;
        }
        p = ExtraPose(p, ref clipY);
        p = FeedRetouch(p);
        p = ApplyMood(p);
        // pas de bouche prévue (il ne fait rien de spécial) : celle de son humeur ; pas quand il dort
        if (p.Mouth == Mouth.None && p.Eyes != Eyes.Closed) p.Mouth = MoodMouth;
        p.Eyes = BlinkEyes(p.Eyes);
        lastPose = p;
        return p;
    }

    private partial PetPose ExtraPose(PetPose p, ref double clipY);

    private PetPose IdlePose()
    {
        var p = PetPose.Stand with { LookX = lookX, LookY = lookY };
        return p;
    }

    private PetPose WalkPose()
    {
        double frameLen = BodyW * (running ? 0.21 : 0.15);
        int f = (int)(walkDist / frameLen) & 3;
        int lift = running ? 2 : 1;
        int swing = running ? 2 : 1;
        var p = PetPose.Stand with
        {
            LiftA = f == 0 ? lift : 0,
            LiftB = f == 2 ? lift : 0,
            L = Arm.Side.Shift(0, f == 0 ? -swing : f == 2 ? swing : 0),
            R = Arm.Side.Shift(0, f == 0 ? swing : f == 2 ? -swing : 0),
            LookX = 1,
            DX = running ? 1 : 0,
            Mouth = running ? Mouth.SmallO : Mouth.None,
        };
        if (running && (f == 0 || f == 2)) p.BodyBottom = -7;
        return p;
    }

    private PetPose SitPose()
    {
        if (T < 0.12) return PetPose.Stand with { BodyBottom = -4, Legs = Legs.Kneel, L = Arm.Down, R = Arm.Down };
        var p = PetPose.Sitting with { LookX = lookX, LookY = lookY };
        // de temps en temps, il balance les bras
        if (((int)(T * 0.7)) % 5 == 4) p = p with { L = Arm.Side.Shift(0, 1), R = Arm.Side.Shift(0, 1) };
        return p;
    }

    private PetPose StretchPose()
    {
        if (T < 0.3) return PetPose.Stand with { L = Arm.Raised, R = Arm.Raised, Eyes = Eyes.Happy };
        if (T < 1.35)
            return PetPose.Stand with
            {
                BodyW = 22, BodyH = 18, L = Arm.Up, R = Arm.Up, Eyes = Eyes.Closed, Mouth = Mouth.Yawn,
                LiftA = 0, LiftB = 0,
            };
        return PetPose.Stand with { L = Arm.Raised, R = Arm.Raised, Eyes = Eyes.Happy, Mouth = Mouth.Smile };
    }

    private PetPose CrouchPose() => PetPose.Stand with
    {
        BodyW = 26, BodyH = 13, BodyBottom = -4, Legs = Legs.Kneel, L = Arm.Down, R = Arm.Down, LookY = -1, LookX = 1,
    };

    private PetPose AirPose()
    {
        if (air == AirKind.Jump || air == AirKind.Hop)
        {
            if (VY < 0)
                return PetPose.Stand with
                {
                    BodyW = 22, BodyH = 18, Legs = Legs.Tuck, L = Arm.Raised, R = Arm.Raised,
                    Eyes = Eyes.Happy, Mouth = Mouth.Smile,
                };
            return PetPose.Stand with { Legs = Legs.Dangle, L = Arm.SideUp, R = Arm.SideUp, LookY = 1, LookX = 1 };
        }
        // chute ou lancer : il gigote
        int f = (int)(T * 10) & 1;
        bool fast = MathX.Hypot(VX, VY) > 1800 * K;
        return PetPose.Stand with
        {
            Legs = Legs.Dangle, Kick = f,
            L = f == 0 ? Arm.Up : Arm.Raised, R = f == 0 ? Arm.Raised : Arm.Up,
            Eyes = fast ? Eyes.Squeeze : Eyes.Wide, Mouth = fast ? Mouth.Open : Mouth.SmallO,
        };
    }

    private PetPose LandPose()
    {
        bool sliding = Math.Abs(VX) > 60 * K;
        if (T < 0.08 || (sliding && stunAfterLand))
            return PetPose.Stand with
            {
                BodyW = 28, BodyH = 12, BodyBottom = -4, Legs = Legs.Spread, L = Arm.Out, R = Arm.Out,
                Eyes = stunAfterLand ? Eyes.Squeeze : Eyes.Closed,
            };
        return PetPose.Stand with { BodyW = 26, BodyH = 14, BodyBottom = -5, Eyes = stunAfterLand ? Eyes.Dizzy : Eyes.Open };
    }

    private PetPose StunPose()
    {
        int f = (int)(T * 5) & 3;
        int sway = f == 1 ? 1 : f == 3 ? -1 : 0;
        return PetPose.Sitting with
        {
            DX = sway, Eyes = ((int)(T * 2) & 1) == 0 ? Eyes.Dizzy : Eyes.Swirl,
            Mouth = ((int)(T * 1.5) & 1) == 0 ? Mouth.Frown : Mouth.SmallO,
        };
    }

    private PetPose GetUpPose()
    {
        if (T < 0.2) return PetPose.Sitting with { Eyes = Eyes.Half };
        if (T < 0.38) return PetPose.Stand with { BodyBottom = -4, Legs = Legs.Kneel, L = Arm.Down, R = Arm.Down };
        int f = (int)(T * 12) & 1;
        return PetPose.Stand with { LookX = f == 0 ? -1 : 1 };
    }

    private PetPose DragPose()
    {
        int f = (int)(T * 7) & 1;
        var (vx, vy) = app.Cursor.Velocity(Clock.Now);
        bool fast = MathX.Hypot(vx, vy) > 1500 * K;
        return PetPose.Stand with
        {
            Legs = Legs.Dangle, Kick = f,
            L = fast ? (f == 0 ? Arm.Up : Arm.Out) : Arm.Raised,
            R = fast ? (f == 0 ? Arm.Out : Arm.Up) : Arm.Raised,
            Eyes = fast ? Eyes.Squeeze : Eyes.Wide,
            Mouth = fast ? Mouth.Open : Mouth.SmallO,
        };
    }
}
