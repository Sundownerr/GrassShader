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

        [SerializeField] public bool _update;
        [Space(2)] [SerializeField] private GrassPositionsScanner grassPositionsScanner;

        [Space(3)] [Header("- GRASS  SETTINGS -")] [SerializeField]
        private Mesh grassMesh;
        [SerializeField] public Material _grassShaderMaterial;
        [SerializeField] public ShadowCastingMode _shadowCastingMode;
        [SerializeField] public Vector3 _meshSize = Vector3.one;
        [SerializeField] public Vector3 _spawnPositionOffset;
        [SerializeField] public float _randomSpawnPositionOffset = 0.18f;

        [Space(3)] [Header("- FLATTEN  SETTINGS -")] [SerializeField]
        private List<GrassFlattener> _grassFlatteners;
        [SerializeField] public float _flattenSpeed = 4;
        [SerializeField] public float _raiseSpeed = 0.8f;

        [Space(3)] [Header("- CUT  SETTINGS -")] [SerializeField]
        private List<GrassCutter> _grassCutters;
        [SerializeField] public Vector2 _growSpeedRange = new(0.1f, 1);
        [SerializeField] public Vector2 _growDelayRange = new(2, 3);

        [Space(3)] [Header("- PARTICLE  SETTINGS -")] [SerializeField]
        private CutParticle[] _cutParticles;
        private CutterConfig[] _cutterConfigs;

        private Vector3[] _cutterPositions = new Vector3[1];
        private FlattenerConfig[] _flattenerConfigs;
        private Vector3[] _flattenerPositions = new Vector3[1];
        private Vector4[][] _grassBendings;
        private MaterialPropertyBlock _grassMaterialPropertyBlock;
        private Matrix4x4[][] _grassMatrix;
        private float[][] _grassRegrowths;
        private float[][] _growDelays;
        private float[][] _growSpeeds;
        private IReadOnlyList<Vector3> _scannedGrassPositions => grassPositionsScanner.Positions();

        private void Start()
        {
            _grassMaterialPropertyBlock = new MaterialPropertyBlock();
            var subCount = 1000;
            var count = _scannedGrassPositions.Count / subCount + 1;

            _grassBendings = new Vector4[count][];
            _grassMatrix = new Matrix4x4[count][];
            _growSpeeds = new float[count][];
            _growDelays = new float[count][];
            _grassRegrowths = new float[count][];

            for (var i = 0; i < count; i++)
            {
                _grassMatrix[i] = new Matrix4x4[subCount];
                _grassBendings[i] = new Vector4[subCount];
                _grassRegrowths[i] = new float[subCount];

                _growSpeeds[i] = new float[subCount];

                for (var j = 0; j < _growSpeeds[i].Length; j++)
                {
                    _growSpeeds[i][j] = Random.Range(_growSpeedRange.x, _growSpeedRange.y);
                }

                _growDelays[i] = new float[subCount];

                for (var j = 0; j < _growDelays[i].Length; j++)
                {
                    _growDelays[i][j] = Random.Range(_growDelayRange.x, _growDelayRange.y);
                }
            }

            FillGrassMatrix();
        }

        private void Update()
        {
            if (!_update)
            {
                return;
            }

            var thousands = 0;

            UpdateCuttersData();
            UpdateFlattenersData();

            var zero = Vector4.zero;
            var deltaTime = Time.deltaTime;
            var grassCount = _scannedGrassPositions.Count;
            var grassPosition = zero;

            for (var i = 0; i < grassCount; i++)
            {
                var subIndex = i % 1000;

                if (i != 0 && subIndex == 0)
                {
                    thousands += 1;
                }

                grassPosition.x = _grassMatrix[thousands][subIndex].m03;
                grassPosition.y = _grassMatrix[thousands][subIndex].m13;
                grassPosition.z = _grassMatrix[thousands][subIndex].m23;
                grassPosition.w = _grassMatrix[thousands][subIndex].m33;

                // flatten grass
                for (var j = 0; j < _flattenerPositions.Length; j++)
                {
                    var xDistance = grassPosition.x - _flattenerPositions[j].x;
                    var yDistance = grassPosition.y - _flattenerPositions[j].y;
                    var zDistance = grassPosition.z - _flattenerPositions[j].z;

                    var nextBending = zero;

                    var xyDistance = xDistance * xDistance + yDistance * yDistance;
                    var distanceToFlattener = xyDistance + zDistance * zDistance;
                    var bending = _grassBendings[thousands][subIndex];

                    if (distanceToFlattener > _flattenerConfigs[j].FlattenDistance)
                    {
                        // raise flattened grass

                        if (bending.x <= nextBending.x && bending.z <= nextBending.y)
                        {
                            _grassBendings[thousands][subIndex].x = 0;
                            _grassBendings[thousands][subIndex].z = 0;
                            continue;
                        }

                        _grassBendings[thousands][subIndex].x += (nextBending.x - bending.x) * deltaTime * _raiseSpeed;
                        _grassBendings[thousands][subIndex].z += (nextBending.z - bending.z) * deltaTime * _raiseSpeed;

                        continue;
                    }

                    var abc = new Vector2(xDistance, zDistance).normalized * _flattenerConfigs[j].BendForce;
                    var abcSqrMagnitude = abc.x * abc.x + abc.y * abc.y;
                    var bend = abc * Mathf.Clamp(1f / abcSqrMagnitude, 0, 1);

                    // Lay down
                    Vector2 currentDirection;

                    currentDirection.x = bending.x;
                    currentDirection.y = bending.z;
                    var currentDirectionMagnitude = currentDirection.x * currentDirection.x +
                                                    currentDirection.y * currentDirection.y;

                    if (currentDirectionMagnitude <= 0.4f)
                    {
                        nextBending.x = bend.x;
                        nextBending.z = bend.y;

                        _grassBendings[thousands][subIndex].x +=
                            (nextBending.x - bending.x) * deltaTime * _flattenSpeed;

                        _grassBendings[thousands][subIndex].z +=
                            (nextBending.z - bending.z) * deltaTime * _flattenSpeed;
                    }
                    else if (bend.sqrMagnitude > currentDirection.sqrMagnitude)
                    {
                        var newVec = currentDirection.normalized * bend.magnitude;
                        nextBending.x = newVec.x;
                        nextBending.z = newVec.y;

                        _grassBendings[thousands][subIndex].x +=
                            (nextBending.x - bending.x) * deltaTime * _flattenSpeed;

                        _grassBendings[thousands][subIndex].z +=
                            (nextBending.z - bending.z) * deltaTime * _flattenSpeed;
                    }
                }

                // cut grass
                for (var j = 0; j < _cutterPositions.Length; j++)
                {
                    var xDistance = grassPosition.x - _cutterPositions[j].x;
                    var yDistance = grassPosition.y - _cutterPositions[j].y;
                    var zDistance = grassPosition.z - _cutterPositions[j].z;

                    var distanceToCutter = xDistance * xDistance + yDistance * yDistance + zDistance * zDistance;
                    var isGrassGrowedEnough = _grassBendings[thousands][subIndex].y > 0.4f;

                    if (distanceToCutter < _cutterConfigs[j].CutDistance && isGrassGrowedEnough)
                    {
                        _grassBendings[thousands][subIndex].y = .0f;
                        _grassRegrowths[thousands][subIndex] = _growDelays[thousands][subIndex];

                        for (var k = 0; k < _cutParticles.Length; k++)
                        {
                            _cutParticles[k].EmitAt(grassPosition,
                                _grassShaderMaterial.GetColor(TintColor1),
                                _grassShaderMaterial.GetColor(TintColor2));
                        }
                    }
                }

                // decrease grass regrowth delay
                if (_grassRegrowths[thousands][subIndex] > 0)
                {
                    _grassRegrowths[thousands][subIndex] -= deltaTime;
                    continue;
                }

                // regrow grass
                if (_grassBendings[thousands][subIndex].y < 1)
                {
                    _grassBendings[thousands][subIndex].y += _growSpeeds[thousands][subIndex] * deltaTime;
                }
            }

            DrawGrassMeshes();
        }

        public void AddGrassCutter(GrassCutter grassCutter)
        {
            _grassCutters.Add(grassCutter);
        }

        public void AddGrassFlattener(GrassFlattener grassFlattener)
        {
            _grassFlatteners.Add(grassFlattener);
        }

        public void RemoveGrassCutter(GrassCutter grassCutter)
        {
            _grassCutters.Remove(grassCutter);
        }

        public void RemoveGrassFlattener(GrassFlattener grassFlattener)
        {
            _grassFlatteners.Remove(grassFlattener);
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

        private void UpdateFlattenersData()
        {
            if (_flattenerPositions.Length != _grassFlatteners.Count)
            {
                _flattenerPositions = new Vector3[_grassFlatteners.Count];
                _flattenerConfigs = new FlattenerConfig[_grassFlatteners.Count];
            }

            for (var i = 0; i < _grassFlatteners.Count; i++)
            {
                _flattenerPositions[i] = _grassFlatteners[i].transform.position;

                _flattenerConfigs[i] = new FlattenerConfig {
                    FlattenDistance = _grassFlatteners[i].FlattenDistance,
                    BendForce = _grassFlatteners[i].BendForce,
                };
            }
        }

        private void UpdateCuttersData()
        {
            if (_cutterPositions.Length != _grassCutters.Count)
            {
                _cutterPositions = new Vector3[_grassCutters.Count];
                _cutterConfigs = new CutterConfig[_grassCutters.Count];
            }

            for (var i = 0; i < _grassCutters.Count; i++)
            {
                _cutterPositions[i] = _grassCutters[i].transform.position;
                _cutterConfigs[i] = new CutterConfig { CutDistance = _grassCutters[i].CutDistance, };
            }
        }

        private void Vector4LerpUnclampedRef(ref Vector4 a, Vector4 b, float t)
        {
            a.x += (b.x - a.x) * t;
            a.y += (b.y - a.y) * t;
            a.z += (b.z - a.z) * t;
            a.w += (b.w - a.w) * t;
        }

        private struct CutterConfig
        {
            public float CutDistance;
        }

        private struct FlattenerConfig
        {
            public float FlattenDistance;
            public float BendForce;
        }
    }
}