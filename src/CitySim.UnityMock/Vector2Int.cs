using System;

namespace CitySim.UnityMock
{
    public readonly struct Vector2Int : IEquatable<Vector2Int>
    {
        public Vector2Int(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(Vector2Int other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object? obj)
        {
            return obj is Vector2Int other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }
}
