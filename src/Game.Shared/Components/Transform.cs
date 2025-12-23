using System.Numerics;
using MessagePack;

namespace Game.Shared.Components;

[MessagePackObject]
public struct Position
{
    [Key(0)] public float X;
    [Key(1)] public float Y;

    public Position(float x, float y)
    {
        X = x;
        Y = y;
    }

    public Vector2 ToVector2() => new(X, Y);
    public static Position FromVector2(Vector2 v) => new(v.X, v.Y);
}

[MessagePackObject]
public struct Velocity
{
    [Key(0)] public float X;
    [Key(1)] public float Y;

    public Velocity(float x, float y)
    {
        X = x;
        Y = y;
    }
}

[MessagePackObject]
public struct Rotation
{
    [Key(0)] public float Radians;

    public Rotation(float radians)
    {
        Radians = radians;
    }

    public float Degrees => Radians * (180f / MathF.PI);
}
