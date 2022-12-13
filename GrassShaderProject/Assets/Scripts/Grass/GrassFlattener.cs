using UnityEngine;

namespace Grass
{
    public class GrassFlattener : MonoBehaviour
    {
        [SerializeField] private float flattenDistance;
        [SerializeField] private float bendForce;

        public float FlattenDistance => flattenDistance;
        public float BendForce => bendForce;
    }
}