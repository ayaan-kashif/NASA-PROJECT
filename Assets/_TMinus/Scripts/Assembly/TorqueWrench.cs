using TMinus.Core;
using UnityEngine;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.Assembly
{
    /// <summary>
    /// The 3D torque wrench shown while a bolt is being tightened: a socket on the bolt head, a long handle
    /// pointing away from the rocket, swinging round the bolt axis as torque builds (a ratchet stroke per 10%).
    /// Purely visual; BoltTool owns the torque numbers.
    /// </summary>
    public sealed class TorqueWrench : MonoBehaviour
    {
        Transform _pivot;

        public static TorqueWrench Create(Transform parent)
        {
            var go = new GameObject("TorqueWrench");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<TorqueWrench>();
            w._pivot = new GameObject("Pivot").transform;
            w._pivot.SetParent(go.transform, false);
            // Unit-sized tool (bolt size = 1), scaled per joint in Show.
            Piece(w._pivot, PrimitiveType.Cylinder, new Vector3(1.5f, 0.9f, 1.5f), new Vector3(0, 1.3f, 0), Quaternion.identity, M.MetalGrey);                 // socket
            Piece(w._pivot, PrimitiveType.Cube, new Vector3(2.2f, 0.8f, 2.2f), new Vector3(0, 2.5f, 0), Quaternion.identity, M.EngineDark);                      // ratchet head
            Piece(w._pivot, PrimitiveType.Cylinder, new Vector3(0.8f, 6f, 0.8f), new Vector3(7f, 2.5f, 0), Quaternion.Euler(0, 0, 90), M.MetalGrey);             // handle
            Piece(w._pivot, PrimitiveType.Cylinder, new Vector3(1.25f, 2.6f, 1.25f), new Vector3(12.5f, 2.5f, 0), Quaternion.Euler(0, 0, 90), M.GantryRed);      // grip
            go.SetActive(false);
            return w;
        }

        static void Piece(Transform parent, PrimitiveType type, Vector3 scale, Vector3 pos, Quaternion rot, string mat)
        {
            var p = GameObject.CreatePrimitive(type);
            Destroy(p.GetComponent<Collider>());      // never block clicks on the bolts
            p.transform.SetParent(parent, false);
            p.transform.localScale = scale; p.transform.localPosition = pos; p.transform.localRotation = rot;
            p.GetComponent<Renderer>().sharedMaterial = M.Get(mat);
        }

        /// <summary>Place the wrench on a bolt. fraction = torque / target (1 = fully torqued).</summary>
        public void Show(Transform bolt, Vector3 rocketAxisPoint, float boltSize, float fraction)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.position = bolt.position;
            // Handle points outward from the rocket's axis so it never clips into the tank.
            var outward = bolt.position - rocketAxisPoint; outward -= Vector3.Project(outward, bolt.up);
            if (outward.sqrMagnitude < 1e-6f) outward = bolt.right;
            transform.rotation = Quaternion.LookRotation(Vector3.Cross(outward.normalized, bolt.up), bolt.up);
            transform.localScale = Vector3.one * boltSize * 1.5f;
            // Each 10% of torque is one ratchet stroke: swing 40 degrees clockwise, then spring back.
            float stroke = Mathf.Repeat(fraction * 10f, 1f);
            _pivot.localRotation = Quaternion.Euler(0, 25f - 40f * stroke, 0);
        }

        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); }
    }
}
