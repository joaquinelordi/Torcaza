using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class Utiles
    {

        /// <summary>
        /// Implementacion de un vector en dos dimensiones con componentes double
        /// </summary>
        public struct Vector2d
        {
            public double X { get; }
            public double Y { get; }

            public Vector2d(double x, double y)
            {
                X = x;
                Y = y;
            }

            #region Propiedades útiles

            public static Vector2d Zero => new Vector2d(0.0, 0.0);
            public static Vector2d One => new Vector2d(1.0, 1.0);

            public double Length => Math.Sqrt(X * X + Y * Y);
            public double LengthSquared => X * X + Y * Y;

            #endregion

            #region Operadores aritméticos

            public static Vector2d operator +(Vector2d a, Vector2d b)
                => new Vector2d(a.X + b.X, a.Y + b.Y);

            public static Vector2d operator -(Vector2d a, Vector2d b)
                => new Vector2d(a.X - b.X, a.Y - b.Y);

            public static Vector2d operator -(Vector2d v)
                => new Vector2d(-v.X, -v.Y);

            public static Vector2d operator *(Vector2d v, double scalar)
                => new Vector2d(v.X * scalar, v.Y * scalar);

            public static Vector2d operator *(double scalar, Vector2d v)
                => new Vector2d(v.X * scalar, v.Y * scalar);

            public static Vector2d operator /(Vector2d v, double scalar)
            {
                if (scalar == 0)
                    throw new DivideByZeroException();
                return new Vector2d(v.X / scalar, v.Y / scalar);
            }

            #endregion

            #region Comparación

            public static bool operator ==(Vector2d a, Vector2d b)
                => a.X == b.X && a.Y == b.Y;

            public static bool operator !=(Vector2d a, Vector2d b)
                => !(a == b);

            public bool Equals(Vector2d other)
                => this == other;

            public override bool Equals(object obj)
                => obj is Vector2d other && Equals(other);

            public override int GetHashCode()
                => X.GetHashCode() ^ Y.GetHashCode();

            #endregion

            #region Operaciones vectoriales

            public static double Dot(Vector2d a, Vector2d b)
                => a.X * b.X + a.Y * b.Y;

            public static double Cross(Vector2d a, Vector2d b)
                => a.X * b.Y - a.Y * b.X; // escalar 2D

            public Vector2d Normalize()
            {
                double len = Length;
                if (len == 0)
                    throw new InvalidOperationException("No se puede normalizar un vector nulo.");
                return this / len;
            }

            public static double Distance(Vector2d a, Vector2d b)
                => (a - b).Length;

            public static double DistanceSquared(Vector2d a, Vector2d b)
                => (a - b).LengthSquared;

            #endregion

            public override string ToString()
                => $"({X}, {Y})";
        }
    }
}
