using UnityEngine;

namespace Grass
{
    public class GrassCutter : MonoBehaviour
    {
        [SerializeField] private float cutDistance;

        public float CutDistance => cutDistance;
    }
}