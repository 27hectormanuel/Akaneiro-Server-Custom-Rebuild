// Minimal stand-ins for the UnityEngine types referenced by the shared client protocol code.
// Only what the protocol files need is provided here.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);

        public override string ToString() => $"({x:0.00}, {y:0.00}, {z:0.00})";
    }
}
