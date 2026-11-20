// HeadlessSim: functional stand-in for the Unity math types. Not part of the Unity project.
using System;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
        public static Vector2 up => new Vector2(0f, 1f);
        public static Vector2 down => new Vector2(0f, -1f);
        public static Vector2 left => new Vector2(-1f, 0f);
        public static Vector2 right => new Vector2(1f, 0f);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float nx, float ny) { x = nx; y = ny; }
        public float this[int i] { get => i == 0 ? x : y; set { if (i == 0) x = value; else y = value; } }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 Scale(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
        public static Vector2 Min(Vector2 a, Vector2 b) => new Vector2(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);
        public bool Equals(Vector2 other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vector2 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => $"({x:0.00}, {y:0.00})";
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public const float kEpsilon = 1e-5f;
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0f; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);
        public static Vector3 positiveInfinity => new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector3 negativeInfinity => new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        public float magnitude => (float)Math.Sqrt(x * (double)x + y * (double)y + z * (double)z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > kEpsilon ? new Vector3(x / m, y / m, z / m) : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float nx, float ny, float nz) { x = nx; y = ny; z = nz; }
        public void Scale(Vector3 s) { x *= s.x; y *= s.y; z *= s.z; }
        public float this[int i]
        {
            get => i == 0 ? x : (i == 1 ? y : z);
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float Magnitude(Vector3 a) => a.magnitude;
        public static float SqrMagnitude(Vector3 a) => a.sqrMagnitude;
        public static Vector3 Normalize(Vector3 a) => a.normalized;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public static Vector3 Project(Vector3 v, Vector3 onNormal)
        {
            float sqr = Dot(onNormal, onNormal);
            if (sqr < Mathf.Epsilon) return zero;
            return onNormal * (Dot(v, onNormal) / sqr);
        }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 planeNormal)
        {
            float sqr = Dot(planeNormal, planeNormal);
            if (sqr < Mathf.Epsilon) return v;
            return v - planeNormal * (Dot(v, planeNormal) / sqr);
        }
        public static Vector3 Reflect(Vector3 d, Vector3 n) => d - 2f * Dot(n, d) * n;
        public static float Angle(Vector3 from, Vector3 to)
        {
            float denominator = (float)Math.Sqrt(from.sqrMagnitude * (double)to.sqrMagnitude);
            if (denominator < 1e-15f) return 0f;
            float dot = Mathf.Clamp(Dot(from, to) / denominator, -1f, 1f);
            return (float)Math.Acos(dot) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float unsigned = Angle(from, to);
            float sign = Mathf.Sign(Dot(axis, Cross(from, to)));
            return unsigned * sign;
        }
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 delta = target - current;
            float distance = delta.magnitude;
            if (distance <= maxDistanceDelta || distance == 0f) return target;
            return current + delta / distance * maxDistanceDelta;
        }
        public static Vector3 ClampMagnitude(Vector3 v, float maxLength)
        {
            float sqr = v.sqrMagnitude;
            if (sqr > maxLength * maxLength) return v.normalized * maxLength;
            return v;
        }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime)
        {
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.deltaTime);
        }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed)
        {
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
        }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            float vx = currentVelocity.x, vy = currentVelocity.y, vz = currentVelocity.z;
            var result = new Vector3(
                Mathf.SmoothDamp(current.x, target.x, ref vx, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(current.y, target.y, ref vy, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(current.z, target.z, ref vz, smoothTime, maxSpeed, deltaTime));
            currentVelocity = new Vector3(vx, vy, vz);
            return result;
        }
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => Lerp(a, b, t);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is Vector3 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public override string ToString() => $"({x:0.00}, {y:0.00}, {z:0.00})";
        public string ToString(string format) => $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)})";
    }

    public struct Vector4 : IEquatable<Vector4>
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => new Vector4(0f, 0f, 0f, 0f);
        public static Vector4 one => new Vector4(1f, 1f, 1f, 1f);
        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0f);
        public static implicit operator Vector3(Vector4 v) => new Vector3(v.x, v.y, v.z);
        public static implicit operator Vector4(Vector2 v) => new Vector4(v.x, v.y, 0f, 0f);
        public static Vector4 operator *(Vector4 a, float d) => new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        public bool Equals(Vector4 other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override bool Equals(object obj) => obj is Vector4 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1);
        public override string ToString() => $"({x:0.00}, {y:0.00}, {z:0.00}, {w:0.00})";
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int one => new Vector2Int(1, 1);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public static implicit operator Vector2(Vector2Int v) => new Vector2(v.x, v.y);
        public bool Equals(Vector2Int other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Vector2Int v && Equals(v);
        public override int GetHashCode() => x * 73856093 ^ y * 19349663;
        public override string ToString() => $"({x}, {y})";
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3Int zero => new Vector3Int(0, 0, 0);
        public static Vector3Int one => new Vector3Int(1, 1, 1);
        public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new Vector3Int(a.x - b.x, a.y - b.y, a.z - b.z);
        public static bool operator ==(Vector3Int a, Vector3Int b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3Int a, Vector3Int b) => !(a == b);
        public static implicit operator Vector3(Vector3Int v) => new Vector3(v.x, v.y, v.z);
        public bool Equals(Vector3Int other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is Vector3Int v && Equals(v);
        public override int GetHashCode() => x * 73856093 ^ y * 19349663 ^ z * 83492791;
        public override string ToString() => $"({x}, {y}, {z})";
    }

    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public Quaternion normalized
        {
            get
            {
                float m = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
                return m < 1e-9f ? identity : new Quaternion(x / m, y / m, z / m, w / m);
            }
        }
        public void Normalize() { this = normalized; }

        /// <summary>Unity convention: rotate z, then x, then y (extrinsic), i.e. q = qy * qx * qz.</summary>
        public static Quaternion Euler(float x, float y, float z)
        {
            double hx = x * Math.PI / 360.0, hy = y * Math.PI / 360.0, hz = z * Math.PI / 360.0;
            var qx = new Quaternion((float)Math.Sin(hx), 0f, 0f, (float)Math.Cos(hx));
            var qy = new Quaternion(0f, (float)Math.Sin(hy), 0f, (float)Math.Cos(hy));
            var qz = new Quaternion(0f, 0f, (float)Math.Sin(hz), (float)Math.Cos(hz));
            return qy * qx * qz;
        }
        public static Quaternion Euler(Vector3 euler) => Euler(euler.x, euler.y, euler.z);

        public Vector3 eulerAngles
        {
            get
            {
                // Inverse of Euler(): matrix elements of R = Ry * Rx * Rz.
                Quaternion q = normalized;
                double xx = q.x * q.x, yy = q.y * q.y, zz = q.z * q.z;
                double xy = q.x * q.y, xz = q.x * q.z, yz = q.y * q.z, wx = q.w * q.x, wy = q.w * q.y, wz = q.w * q.z;
                double m12 = 2.0 * (yz - wx);   // -sin(x)
                double sinX = -m12;
                double ex, ey, ez;
                if (sinX > 0.99999)
                {
                    ex = Math.PI / 2.0;
                    ey = Math.Atan2(2.0 * (xy - wz), 1.0 - 2.0 * (yy + zz));
                    ez = 0.0;
                }
                else if (sinX < -0.99999)
                {
                    ex = -Math.PI / 2.0;
                    ey = Math.Atan2(-(2.0 * (xy - wz)), 1.0 - 2.0 * (yy + zz));
                    ez = 0.0;
                }
                else
                {
                    ex = Math.Asin(sinX);
                    ey = Math.Atan2(2.0 * (xz + wy), 1.0 - 2.0 * (xx + yy));
                    ez = Math.Atan2(2.0 * (xy + wz), 1.0 - 2.0 * (xx + zz));
                }

                return new Vector3(Wrap((float)(ex * 180.0 / Math.PI)), Wrap((float)(ey * 180.0 / Math.PI)), Wrap((float)(ez * 180.0 / Math.PI)));
            }
            set { this = Euler(value); }
        }

        private static float Wrap(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f) degrees += 360f;
            if (degrees > 359.9995f || degrees < 0.0005f) degrees = 0f;
            return degrees;
        }

        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            Vector3 n = axis.normalized;
            if (n.sqrMagnitude < 1e-12f) return identity;
            double half = angle * Math.PI / 360.0;
            float s = (float)Math.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, (float)Math.Cos(half));
        }

        public void ToAngleAxis(out float angle, out Vector3 axis)
        {
            Quaternion q = normalized;
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            angle = 2f * (float)Math.Acos(Mathf.Clamp(q.w, -1f, 1f)) * Mathf.Rad2Deg;
            float s = (float)Math.Sqrt(Math.Max(0.0, 1.0 - q.w * (double)q.w));
            axis = s < 1e-6f ? Vector3.right : new Vector3(q.x / s, q.y / s, q.z / s);
        }

        public static Quaternion Inverse(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (n < 1e-12f) return identity;
            return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n);
        }

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = Math.Min(Math.Abs(Dot(a.normalized, b.normalized)), 1f);
            return dot > 0.999999f ? 0f : (float)(Math.Acos(dot) * 2.0 * 180.0 / Math.PI);
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            a = a.normalized; b = b.normalized;
            float dot = Dot(a, b);
            if (dot < 0f) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); dot = -dot; }
            if (dot > 0.9995f)
            {
                return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
            }

            double theta0 = Math.Acos(dot);
            double theta = theta0 * t;
            double sinTheta = Math.Sin(theta), sinTheta0 = Math.Sin(theta0);
            float s0 = (float)(Math.Cos(theta) - dot * sinTheta / sinTheta0);
            float s1 = (float)(sinTheta / sinTheta0);
            return new Quaternion(a.x * s0 + b.x * s1, a.y * s0 + b.y * s1, a.z * s0 + b.z * s1, a.w * s0 + b.w * s1).normalized;
        }

        public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            if (Dot(a, b) < 0f) b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
        }

        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)
        {
            float angle = Angle(from, to);
            if (angle == 0f) return to;
            return SlerpUnclamped(from, to, Math.Min(1f, maxDegreesDelta / angle));
        }

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards)
        {
            Vector3 f = forward.normalized;
            if (f.sqrMagnitude < 1e-10f) return identity;
            Vector3 r = Vector3.Cross(upwards, f);
            if (r.sqrMagnitude < 1e-10f)
            {
                // forward is parallel to up: pick any perpendicular axis.
                r = Vector3.Cross(Math.Abs(f.y) < 0.99f ? Vector3.up : Vector3.forward, f);
                if (r.sqrMagnitude < 1e-10f) r = Vector3.right;
            }

            r = r.normalized;
            Vector3 u = Vector3.Cross(f, r);
            return FromBasis(r, u, f);
        }

        // Rotation whose columns are the given orthonormal basis (x, y, z axes).
        internal static Quaternion FromBasis(Vector3 r, Vector3 u, Vector3 f)
        {
            float m00 = r.x, m01 = u.x, m02 = f.x;
            float m10 = r.y, m11 = u.y, m12 = f.y;
            float m20 = r.z, m21 = u.z, m22 = f.z;
            float trace = m00 + m11 + m22;
            Quaternion q;
            if (trace > 0f)
            {
                float s = (float)Math.Sqrt(trace + 1f) * 2f;
                q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
                q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }

            return q.normalized;
        }

        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            Vector3 a = from.normalized, b = to.normalized;
            if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f) return identity;
            float dot = Vector3.Dot(a, b);
            if (dot > 0.999999f) return identity;
            if (dot < -0.999999f)
            {
                Vector3 axis = Vector3.Cross(Vector3.right, a);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, a);
                return AngleAxis(180f, axis);
            }

            Vector3 cross = Vector3.Cross(a, b);
            return new Quaternion(cross.x, cross.y, cross.z, 1f + dot).normalized;
        }

        public void SetLookRotation(Vector3 view) { this = LookRotation(view); }
        public void SetLookRotation(Vector3 view, Vector3 up) { this = LookRotation(view, up); }

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2f, y2 = q.y * 2f, z2 = q.z * 2f;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2;
            float xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3(
                (1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }

        public static bool operator ==(Quaternion a, Quaternion b) => Dot(a, b) > 0.999999f;
        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);
        public bool Equals(Quaternion other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override bool Equals(object obj) => obj is Quaternion q && Equals(q);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1);
        public override string ToString() => $"({x:0.000}, {y:0.000}, {z:0.000}, {w:0.000})";
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static readonly float Epsilon = 1.401298E-45f;

        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int v) => Math.Abs(v);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(float a, float b, float c) => Min(Min(a, b), c);
        public static float Min(params float[] values) { float m = values[0]; for (int i = 1; i < values.Length; i++) if (values[i] < m) m = values[i]; return m; }
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(float a, float b, float c) => Max(Max(a, b), c);
        public static float Max(params float[] values) { float m = values[0]; for (int i = 1; i < values.Length; i++) if (values[i] > m) m = values[i]; return m; }
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Exp(float power) => (float)Math.Exp(power);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Log(float f, float p) => (float)Math.Log(f, p);
        public static float Log10(float f) => (float)Math.Log10(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Round(float f) => (float)Math.Round(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
        public static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float LerpAngle(float a, float b, float t)
        {
            float delta = Repeat(b - a, 360f);
            if (delta > 180f) delta -= 360f;
            return a + delta * Clamp01(t);
        }
        public static float InverseLerp(float a, float b, float value) => a != b ? Clamp01((value - a) / (b - a)) : 0f;
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (-maxDelta < delta && delta < maxDelta) return target;
            target = current + delta;
            return MoveTowards(current, target, maxDelta);
        }
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }
        public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - Abs(t - length);
        }
        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime)
        {
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, Infinity, Time.deltaTime);
        }
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed)
        {
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
        }
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            float maxChange = maxSpeed * smoothTime;
            change = Clamp(change, -maxChange, maxChange);
            target = current - change;
            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if (originalTo - current > 0f == output > originalTo)
            {
                output = originalTo;
                currentVelocity = (output - originalTo) / deltaTime;
            }
            return output;
        }
        public static float PerlinNoise(float x, float y) => 0.5f + 0.5f * Sin(x * 12.9898f + y * 78.233f);
        public static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
        public static int NextPowerOfTwo(int value) { int p = 1; while (p < value) p <<= 1; return p; }
    }

    public struct Color : IEquatable<Color>
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color blue => new Color(0f, 0f, 1f, 1f);
        public static Color yellow => new Color(1f, 0.92f, 0.016f, 1f);
        public static Color cyan => new Color(0f, 1f, 1f, 1f);
        public static Color magenta => new Color(1f, 0f, 1f, 1f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color grey => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public float maxColorComponent => Math.Max(Math.Max(r, g), b);
        public Color linear => this;
        public Color gamma => this;
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        public static Color LerpUnclamped(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color operator -(Color a, Color b) => new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator *(Color a, float d) => new Color(a.r * d, a.g * d, a.b * d, a.a * d);
        public static Color operator *(float d, Color a) => new Color(a.r * d, a.g * d, a.b * d, a.a * d);
        public static Color operator /(Color a, float d) => new Color(a.r / d, a.g / d, a.b / d, a.a / d);
        public static bool operator ==(Color a, Color b) => Math.Abs(a.r - b.r) < 1e-5f && Math.Abs(a.g - b.g) < 1e-5f && Math.Abs(a.b - b.b) < 1e-5f && Math.Abs(a.a - b.a) < 1e-5f;
        public static bool operator !=(Color a, Color b) => !(a == b);
        public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
        public static implicit operator Color(Vector4 v) => new Color(v.x, v.y, v.z, v.w);
        public static Color HSVToRGB(float h, float s, float v)
        {
            float c = v * s, hp = (h % 1f) * 6f, x = c * (1f - Math.Abs(hp % 2f - 1f)), m = v - c;
            float r1 = 0, g1 = 0, b1 = 0;
            if (hp < 1) { r1 = c; g1 = x; } else if (hp < 2) { r1 = x; g1 = c; } else if (hp < 3) { g1 = c; b1 = x; }
            else if (hp < 4) { g1 = x; b1 = c; } else if (hp < 5) { r1 = x; b1 = c; } else { r1 = c; b1 = x; }
            return new Color(r1 + m, g1 + m, b1 + m, 1f);
        }
        public bool Equals(Color other) => r == other.r && g == other.g && b == other.b && a == other.a;
        public override bool Equals(object obj) => obj is Color c && Equals(c);
        public override int GetHashCode() => r.GetHashCode() ^ (g.GetHashCode() << 2) ^ (b.GetHashCode() >> 2) ^ (a.GetHashCode() >> 1);
        public override string ToString() => $"RGBA({r:0.000}, {g:0.000}, {b:0.000}, {a:0.000})";
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
        public static implicit operator Color32(Color c) => new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f));
    }

    public struct Bounds : IEquatable<Bounds>
    {
        private Vector3 m_Center, m_Extents;
        public Bounds(Vector3 center, Vector3 size) { m_Center = center; m_Extents = size * 0.5f; }
        public Vector3 center { get => m_Center; set => m_Center = value; }
        public Vector3 size { get => m_Extents * 2f; set => m_Extents = value * 0.5f; }
        public Vector3 extents { get => m_Extents; set => m_Extents = value; }
        public Vector3 min { get => m_Center - m_Extents; set => SetMinMax(value, max); }
        public Vector3 max { get => m_Center + m_Extents; set => SetMinMax(min, value); }
        public void SetMinMax(Vector3 min, Vector3 max) { m_Extents = (max - min) * 0.5f; m_Center = min + m_Extents; }
        public void Encapsulate(Vector3 point) { SetMinMax(Vector3.Min(min, point), Vector3.Max(max, point)); }
        public void Encapsulate(Bounds bounds) { Encapsulate(bounds.min); Encapsulate(bounds.max); }
        public void Expand(float amount) { amount *= 0.5f; m_Extents += new Vector3(amount, amount, amount); }
        public void Expand(Vector3 amount) { m_Extents += amount * 0.5f; }
        public bool Contains(Vector3 p) { Vector3 a = min, b = max; return p.x >= a.x && p.x <= b.x && p.y >= a.y && p.y <= b.y && p.z >= a.z && p.z <= b.z; }
        public bool Intersects(Bounds o) { Vector3 a = min, b = max, c = o.min, d = o.max; return a.x <= d.x && b.x >= c.x && a.y <= d.y && b.y >= c.y && a.z <= d.z && b.z >= c.z; }
        public Vector3 ClosestPoint(Vector3 p) { Vector3 a = min, b = max; return new Vector3(Mathf.Clamp(p.x, a.x, b.x), Mathf.Clamp(p.y, a.y, b.y), Mathf.Clamp(p.z, a.z, b.z)); }
        public float SqrDistance(Vector3 p) => (ClosestPoint(p) - p).sqrMagnitude;
        public static bool operator ==(Bounds a, Bounds b) => a.center == b.center && a.extents == b.extents;
        public static bool operator !=(Bounds a, Bounds b) => !(a == b);
        public bool Equals(Bounds other) => this == other;
        public override bool Equals(object obj) => obj is Bounds b && this == b;
        public override int GetHashCode() => m_Center.GetHashCode() ^ (m_Extents.GetHashCode() << 2);
        public override string ToString() => $"Center: {m_Center}, Extents: {m_Extents}";
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Rect(Vector2 position, Vector2 size) { x = position.x; y = position.y; width = size.x; height = size.y; }
        public static Rect zero => new Rect(0f, 0f, 0f, 0f);
        public float xMin { get => x; set { float max = xMax; x = value; width = max - x; } }
        public float yMin { get => y; set { float max = yMax; y = value; height = max - y; } }
        public float xMax { get => x + width; set => width = value - x; }
        public float yMax { get => y + height; set => height = value - y; }
        public Vector2 position { get => new Vector2(x, y); set { x = value.x; y = value.y; } }
        public Vector2 size { get => new Vector2(width, height); set { width = value.x; height = value.y; } }
        public Vector2 center { get => new Vector2(x + width * 0.5f, y + height * 0.5f); set { x = value.x - width * 0.5f; y = value.y - height * 0.5f; } }
        public Vector2 min => new Vector2(xMin, yMin);
        public Vector2 max => new Vector2(xMax, yMax);
        public bool Contains(Vector2 p) => p.x >= xMin && p.x < xMax && p.y >= yMin && p.y < yMax;
        public bool Contains(Vector3 p) => p.x >= xMin && p.x < xMax && p.y >= yMin && p.y < yMax;
        public bool Overlaps(Rect o) => o.xMax > xMin && o.xMin < xMax && o.yMax > yMin && o.yMin < yMax;
        public override string ToString() => $"(x:{x:0.00}, y:{y:0.00}, width:{width:0.00}, height:{height:0.00})";
    }

    public struct RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
    }

    public static class Random
    {
        private static System.Random generator = new System.Random(12345);
        public static void InitState(int seed) { generator = new System.Random(seed); }
        public static float value => (float)generator.NextDouble();
        public static float Range(float minInclusive, float maxInclusive) => minInclusive + (maxInclusive - minInclusive) * (float)generator.NextDouble();
        public static int Range(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : generator.Next(minInclusive, maxExclusive);
        public static Vector3 insideUnitSphere
        {
            get
            {
                for (int i = 0; i < 32; i++)
                {
                    var v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                    if (v.sqrMagnitude <= 1f) return v;
                }
                return Vector3.zero;
            }
        }
        public static Vector3 onUnitSphere { get { Vector3 v = insideUnitSphere; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up; } }
        public static Vector2 insideUnitCircle { get { Vector3 v = insideUnitSphere; return new Vector2(v.x, v.y); } }
        public static Quaternion rotation => Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));
    }

    public struct Matrix4x4
    {
        // Only what is needed to carry a TRS around; multiplication is delegated to Transform math.
        public Vector3 translation;
        public Quaternion rotationPart;
        public Vector3 scalePart;
        public static Matrix4x4 identity => new Matrix4x4 { translation = Vector3.zero, rotationPart = Quaternion.identity, scalePart = Vector3.one };
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s) => new Matrix4x4 { translation = pos, rotationPart = q, scalePart = s };
        public Vector3 MultiplyPoint3x4(Vector3 p) => translation + rotationPart * Vector3.Scale(scalePart, p);
        public Vector3 MultiplyPoint(Vector3 p) => MultiplyPoint3x4(p);
        public Vector3 MultiplyVector(Vector3 v) => rotationPart * Vector3.Scale(scalePart, v);
    }
}
