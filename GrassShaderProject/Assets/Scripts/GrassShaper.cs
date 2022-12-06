using System.Collections.Generic;
using UnityEngine;

public class GrassShaper : MonoBehaviour
{
    [SerializeField] private LayerMask grassPlacementLayer;
    [SerializeField] private float raycastHeight = 5f;
    [SerializeField] private float step;
    [SerializeField] private GrassGPUInstancing targetGrassSpawner;
    [SerializeField] private MeshRenderer meshRenderer;
    private List<Vector3> newSpots;

    private void OnDrawGizmosSelected()
    {
        var bounds = meshRenderer.bounds;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(bounds.min, bounds.max);
    }

    [ContextMenu("UPDATE GRASS POINTS")]
    private void UpdateGrass()
    {
        var bounds = meshRenderer.bounds;
        var startSpot = bounds.min;
        var endSpot = bounds.max;
        var currentSpot = startSpot;
        var counter = 0;

        Debug.DrawLine(startSpot, startSpot + Vector3.up * 7, Color.blue, 1f);
        Debug.DrawLine(endSpot, endSpot + Vector3.up * 7, Color.yellow, 1f);

        var hits = 0;
        newSpots = new List<Vector3>();

        while (counter < 100000 && (currentSpot.x < endSpot.x || currentSpot.z < endSpot.z))
        {
            counter++;

            if (Physics.Raycast(currentSpot + Vector3.up * raycastHeight, Vector3.down, out var hit, 30f,
                grassPlacementLayer))
            {
                hits += 1;
                newSpots.Add(hit.point);

                Debug.DrawLine(hit.point, hit.point + Vector3.up * 3, Color.cyan, 1f);
            }

            currentSpot += Vector3.forward * step;

            if (currentSpot.z > endSpot.z && currentSpot.x < endSpot.x)
            {
                currentSpot.z = startSpot.z;
                currentSpot += Vector3.right * step;
            }
        }
        
        Debug.Log($"{counter} scans, {hits} grass points ");
        Debug.Log("assigning new array size:" + newSpots.Count);
        
        targetGrassSpawner.UpdateGrassPositions(newSpots);
      
    }
}