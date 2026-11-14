namespace Aster.Core.Logic;

public struct SpringState
{
    public const double Stiffness = 240.0;
    public const double BouncyDamping = 21.0;
    public const double NormalDamping = 27.0;
    public const double MaxDeltaSeconds = 0.05;
    public const double SettlePosition = 0.2;
    public const double SettleVelocity = 0.8;

    public double Value;
    public double Velocity;

    public bool Step(double target, double dt, bool bouncy)
    {
        dt = Math.Clamp(dt, 0, MaxDeltaSeconds);
        var damping = bouncy ? BouncyDamping : NormalDamping;
        var force = (target - Value) * Stiffness;

        Velocity += force * dt;
        Velocity *= Math.Exp(-damping * dt);
        Value += Velocity * dt;

        if (Math.Abs(target - Value) < SettlePosition && Math.Abs(Velocity) < SettleVelocity)
        {
            Value = target;
            Velocity = 0;
            return true;
        }

        return false;
    }
}
