using System.Collections.Generic;
using UnityEngine;

namespace Grass
{
    public class GrassPositionsScanner : MonoBehaviour
    {
        [SerializeField] private LayerMask _scanLayer;
        [SerializeField] private float _scanStep = 0.3f;
        [SerializeField] private Collider[] _collidersToScan;
        private readonly RaycastHit[] _hits = new RaycastHit[1];
        private readonly List<Vector3> _positions = new();

        private void OnDrawGizmosSelected()
        {
            var color = Color.cyan;
            color.a -= 0.7f;
            Gizmos.color = color;

            foreach (var grassCollider in _collidersToScan)
            {
                var bounds = grassCollider.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
                Gizmos.DrawCube(bounds.center, bounds.size * 0.99f);
                Gizmos.DrawLine(bounds.min, bounds.max);
            }
        }

        public IReadOnlyList<Vector3> Positions()
        {
            if (_positions.Count == 0)
            {
                Scan();
            }

            return _positions;
        }

        private void Scan(Collider grassCollider)
        {
            var bounds = grassCollider.bounds;
            var startSpot = bounds.min;
            var endSpot = bounds.max;
            var currentSpot = startSpot;
            var counter = 0;
            var height = bounds.size.y * 2f;

            while (counter < 100000 && (currentSpot.x < endSpot.x || currentSpot.z < endSpot.z))
            {
                counter++;
                var rayOrigin = currentSpot + Vector3.up * height;

                if (Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _hits, 1000f, _scanLayer) > 0)
                {
                    if (_hits[0].collider == grassCollider)
                    {
                        _positions.Add(_hits[0].point);
                    }
                }

                currentSpot += Vector3.forward * _scanStep;

                if (currentSpot.z > endSpot.z && currentSpot.x < endSpot.x)
                {
                    currentSpot.z = startSpot.z;
                    currentSpot += Vector3.right * _scanStep;
                }
            }
        }

        public void Scan()
        {
            _positions.Clear();

            foreach (var grassCollider in _collidersToScan)
            {
                Scan(grassCollider);
            }
        }
#if UNITY_EDITOR
        [ContextMenu("Visualize Positions")]
        private void VisualizePositions()
        {
            foreach (var position in _positions)
            {
                Debug.DrawRay(position, Vector3.up, Color.cyan, 1f);
            }
        }
#endif
    }
}