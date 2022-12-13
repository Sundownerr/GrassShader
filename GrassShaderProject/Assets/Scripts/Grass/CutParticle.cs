using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Grass
{
    public class CutParticle : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private Vector2 _colorRange;
        [SerializeField] private int _emitCount;
        
        public void EmitAt(Vector3 position, Color from, Color to)
        {
            var mainModule = _particleSystem.main;

            var color = Color.Lerp(from, to, Random.Range(_colorRange.x, _colorRange.y));

            mainModule.startColor = color;

            _particleSystem.transform.position = position;
            _particleSystem.Emit(_emitCount);
        }
    }
}