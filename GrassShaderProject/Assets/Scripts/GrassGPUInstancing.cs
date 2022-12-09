using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Grass
{
    public class GrassGPUInstancing : MonoBehaviour
    {
        private static readonly int CollisionBending = Shader.PropertyToID("_CollisionBending");
        private static readonly int TintColor1 = Shader.PropertyToID("_TintColor1");
        private static readonly int TintColor2 = Shader.PropertyToID("_TintColor2");

        [Space(2)] [SerializeField] private GrassPositionsScanner grassPositionsScanner;

        [Space(3)] [Header("- GRASS  SETTINGS -")] [SerializeField]
        private Mesh grassMesh;
        [SerializeField] private Material _grassShaderMaterial;
        [SerializeField] private ShadowCastingMode _shadowCastingMode;
        [SerializeField] private Vector3 _meshSize;
        [SerializeField] private Vector3 _spawnPositionOffset;
        [SerializeField] private float _randomSpawnPositionOffset;

        [Space(3)] [Header("- FLATTEN  SETTINGS -")] [SerializeField]
        private GrassFlattener[] _grassFlatteners;
        [SerializeField] private float _flattenSpeed;
        [SerializeField] private float _raiseSpeed;

        [Space(3)] [Header("- CUT  SETTINGS -")] [SerializeField]
        private GrassCutter[] _grassCutters;
        [SerializeField] private Vector2 _growSpeedRange;
        [SerializeField] private Vector2 _growDelayRange;

        [Space(3)] [Header("- PARTICLE  SETTINGS -")] [SerializeField]
        private CutParticle[] _cutParticles;

        private Vector3[] _cutterPositions = new Vector3[1];
        private Vector3[] _flattenerPositions = new Vector3[1];
        private Vector4[][] _grassBendings;
        private MaterialPropertyBlock _grassMaterialPropertyBlock;
        private Matrix4x4[][] _grassMatrix;
        private IReadOnlyList<Vector3> _scannedGrassPositions => grassPositionsScanner.Positions;

        private void Start()
        {
            _grassMaterialPropertyBlock = new MaterialPropertyBlock();

            _grassBendings = new Vector4[_scannedGrassPositions.Count / 1000 + 1][];
            _grassMatrix = new Matrix4x4[_scannedGrassPositions.Count / 1000 + 1][];

            for (var i = 0; i < _scannedGrassPositions.Count / 1000 + 1; i++)
            {
                _grassMatrix[i] = new Matrix4x4[1000];
                _grassBendings[i] = new Vector4[1000];
            }

            FillGrassMatrix();
        }

        private void Update()
        {
            var thousands = 0;

            UpdateCuttersPositions();
            UpdateFlattenersPositions();

            var zero = Vector4.zero;
            var deltaTime = Time.deltaTime;
            var count = _scannedGrassPositions.Count;

            for (var i = 0; i < count; i++)
            {
                var subIndex = i % 1000;

                if (i != 0 && subIndex == 0)
                {
                    thousands += 1;
                }

                var column = _grassMatrix[thousands][subIndex].GetColumn(3);

                if (_grassFlatteners.Length > 0)
                {
                    Flatten(thousands, subIndex, column, deltaTime, zero);
                }

                if (_grassCutters.Length > 0)
                {
                    Cut(thousands, subIndex, column);
                }
            }

            DrawGrassMeshes();
        }

        private void FillGrassMatrix()
        {
            var thousands = 0;
            var grassMatrix = new Matrix4x4();

            for (var i = 0; i < _scannedGrassPositions.Count; i++)
            {
                var subIndex = i % 1000;

                if (i != 0 && subIndex == 0)
                {
                    thousands += 1;
                }

                var spawnPosition = GetGrassSpawnPosition(_scannedGrassPositions[i]);

                grassMatrix.SetTRS(spawnPosition, Quaternion.identity, _meshSize);

                _grassMatrix[thousands][subIndex] = grassMatrix;
                _grassBendings[thousands][subIndex].y = 1f;
            }
        }

        private Vector3 GetGrassSpawnPosition(Vector3 scannedGrassPosition)
        {
            var randomSpawnOffset = Random.insideUnitSphere * _randomSpawnPositionOffset;
            randomSpawnOffset.y = 0;

            var spawnPosition = scannedGrassPosition + randomSpawnOffset + _spawnPositionOffset;
            return spawnPosition;
        }

        private void DrawGrassMeshes()
        {
            for (var i = 0; i < _grassMatrix.Length; i++)
            {
                _grassMaterialPropertyBlock.SetVectorArray(CollisionBending, _grassBendings[i]);

                Graphics.DrawMeshInstanced(
                    grassMesh,
                    0,
                    _grassShaderMaterial,
                    _grassMatrix[i],
                    _grassMatrix[i].Length,
                    _grassMaterialPropertyBlock,
                    _shadowCastingMode,
                    false,
                    0,
                    null);
            }
        }

        private void UpdateFlattenersPositions()
        {
            if (_flattenerPositions.Length != _grassFlatteners.Length)
            {
                _flattenerPositions = new Vector3[_grassFlatteners.Length];
            }

            for (var i = 0; i < _grassFlatteners.Length; i++)
            {
                _flattenerPositions[i] = _grassFlatteners[i].transform.position;
            }
        }

        private void UpdateCuttersPositions()
        {
            if (_cutterPositions.Length != _grassCutters.Length)
            {
                _cutterPositions = new Vector3[_grassCutters.Length];
            }

            for (var i = 0; i < _grassCutters.Length; i++)
            {
                _cutterPositions[i] = _grassCutters[i].transform.position;
            }
        }

        private void Cut(int thousands, int subindex, Vector4 grassPosition)
        {
            var count = _cutterPositions.Length;

            for (var i = 0; i < count; i++)
            {
                var xDistance = grassPosition.x - _cutterPositions[i].x;
                var yDistance = grassPosition.y - _cutterPositions[i].y;
                var zDistance = grassPosition.z - _cutterPositions[i].z;

                var abc = new Vector3(xDistance, yDistance, zDistance);
                var isGrassGrowedEnough = _grassBendings[thousands][subindex].y > 0.4f;

                if (abc.sqrMagnitude < _grassCutters[i].CutDistance && isGrassGrowedEnough)
                {
                    _grassBendings[thousands][subindex].y = .0f;

                    var growSpeed = Random.Range(_growSpeedRange.x, _growSpeedRange.y);
                    var growDelay = Random.Range(_growDelayRange.x, _growDelayRange.y);

                    StartCoroutine(Regrow(growDelay, growSpeed, thousands, subindex));

                    for (var j = 0; j < _cutParticles.Length; j++)
                    {
                        _cutParticles[j].EmitAt(grassPosition,
                            _grassShaderMaterial.GetColor(TintColor1),
                            _grassShaderMaterial.GetColor(TintColor2));
                    }
                }
            }
        }

        private IEnumerator Regrow(float delay, float speed, int thousands, int subIndex)
        {
            yield return new WaitForSeconds(delay);

            while (_grassBendings[thousands][subIndex].y < 1)
            {
                _grassBendings[thousands][subIndex].y += speed * Time.deltaTime;
                yield return null;
            }
        }

        private void Flatten(int thousands, int subindex, Vector4 grassPosition, float deltaTime, Vector4 zero)
        {
            var count = _flattenerPositions.Length;

            for (var i = 0; i < count; i++)
            {
                var xDistance = grassPosition.x - _flattenerPositions[i].x;
                var yDistance = grassPosition.y - _flattenerPositions[i].y;
                var zDistance = grassPosition.z - _flattenerPositions[i].z;

                var lerpTarget = zero;

                var distanceToFlattener = xDistance * xDistance + yDistance * yDistance + zDistance * zDistance;

                if (distanceToFlattener > _grassFlatteners[i].FlattenDistance)
                {
                    // Raise

                    var lerpSpeed = deltaTime * _raiseSpeed;
                    var lerped = Vector4.LerpUnclamped(_grassBendings[thousands][subindex], lerpTarget, lerpSpeed);
                    _grassBendings[thousands][subindex].x = lerped.x;
                    _grassBendings[thousands][subindex].z = lerped.z;
                    continue;
                }

                var abc = new Vector2(xDistance, zDistance).normalized * _grassFlatteners[i].BendForce;
                var bend = abc * Mathf.Clamp(1f / abc.sqrMagnitude, 0, 1);

                // Lay down
                Vector2 currentDirection;

                currentDirection.x = _grassBendings[thousands][subindex].x;
                currentDirection.y = _grassBendings[thousands][subindex].z;

                if (currentDirection.sqrMagnitude <= 0.4f)
                {
                    lerpTarget.x = bend.x;
                    lerpTarget.z = bend.y;

                    var lerpSpeed = deltaTime * _flattenSpeed;
                    var lerped = Vector4.LerpUnclamped(_grassBendings[thousands][subindex], lerpTarget, lerpSpeed);
                    _grassBendings[thousands][subindex].x = lerped.x;
                    _grassBendings[thousands][subindex].z = lerped.z;
                }
                else if (bend.sqrMagnitude > currentDirection.sqrMagnitude)
                {
                    var newVec = currentDirection.normalized * bend.magnitude;
                    lerpTarget.x = newVec.x;
                    lerpTarget.z = newVec.y;

                    var lerpSpeed = deltaTime * _flattenSpeed;
                    var lerped = Vector4.LerpUnclamped(_grassBendings[thousands][subindex], lerpTarget, lerpSpeed);
                    _grassBendings[thousands][subindex].x = lerped.x;
                    _grassBendings[thousands][subindex].z = lerped.z;
                }
            }
        }

        private void Vector4LerpUnclampedRef(ref Vector4 source, Vector4 target, float t)
        {
            var lerped = Vector4.LerpUnclamped(source, target, t);
            source.x = lerped.x;
            source.z = lerped.z;
        }
    }
}