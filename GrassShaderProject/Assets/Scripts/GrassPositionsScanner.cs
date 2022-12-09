using System.Collections.Generic;
using UnityEngine;

namespace Grass
{
    public class GrassPositionsScanner : MonoBehaviour
    {
        [SerializeField] private LayerMask _scanLayer;
        [SerializeField] private Collider _scanBox;
        [SerializeField] private float _scanStep;
        [SerializeField] private Collider[] _collidersToScan;
        [SerializeField] private List<Vector3> _positions;
        public IReadOnlyList<Vector3> Positions => _positions;

        private void OnDrawGizmosSelected()
        {
            if (_scanBox == null)
            {
                return;
            }

            var bounds = _scanBox.bounds;

            var color = Color.cyan;
            color.a -= 0.9f;
            Gizmos.color = color;
            Gizmos.DrawCube(bounds.center, bounds.size * 0.97f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(bounds.min, bounds.max);
        }

        [ContextMenu("SCAN")]
        private void Scan()
        {
            var bounds = _scanBox.bounds;
            var startSpot = bounds.min;
            var endSpot = bounds.max;
            var currentSpot = startSpot;
            var counter = 0;
            var height = bounds.max.y - bounds.min.y;

            var hits = 0;
            _positions.Clear();

            while (counter < 100000 && (currentSpot.x < endSpot.x || currentSpot.z < endSpot.z))
            {
                counter++;

                var rayOrigin = currentSpot + Vector3.up * height;


                if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 1000f, _scanLayer))
                {
                    for (var i = 0; i < _collidersToScan.Length; i++)
                    {
                        if (hit.collider != _collidersToScan[i])
                        {
                            continue;
                        }

                        hits += 1;
                        _positions.Add(hit.point);

                        break;
                    }
                }

                currentSpot += Vector3.forward * _scanStep;

                if (currentSpot.z > endSpot.z && currentSpot.x < endSpot.x)
                {
                    currentSpot.z = startSpot.z;
                    currentSpot += Vector3.right * _scanStep;
                }
            }

#if UNITY_EDITOR
            Debug.Log($"[{nameof(GrassPositionsScanner)}] {counter} scans, {hits} grass points ");
            
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}