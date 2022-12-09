using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    private ParticleSystem[] _cutParticleSystems;
    [SerializeField] private Vector2 _colorRange;

    private Vector4[][] _collisionBendings;
    private Vector3[] _cutterPositions = new Vector3[1];
    private Vector3[] _flattenerPositions = new Vector3[1];
    private MaterialPropertyBlock _grassMaterialPropertyBlock;
    private Matrix4x4 _matrix;
    private Matrix4x4[][] _matrixArrays;
    private IReadOnlyList<Vector3> _grassPositions => grassPositionsScanner.Positions;

    private void Start()
    {
        _grassMaterialPropertyBlock = new MaterialPropertyBlock();
        _collisionBendings = new Vector4[_grassPositions.Count / 1000 + 1][];
        _matrixArrays = new Matrix4x4[_grassPositions.Count / 1000 + 1][];

        for (var i = 0; i < _grassPositions.Count / 1000 + 1; i++)
        {
            _matrixArrays[i] = new Matrix4x4[1000];
            _collisionBendings[i] = new Vector4[1000];
        }

        var thousands = 0;

        for (var i = 0; i < _grassPositions.Count; i++)
        {
            var subIndex = i % 1000;

            if (i != 0 && subIndex == 0)
            {
                thousands += 1;
            }

            var randomOffset = Random.insideUnitSphere * _randomSpawnPositionOffset;
            randomOffset.y = 0;

            _matrix.SetTRS(_grassPositions[i] + randomOffset + _spawnPositionOffset, Quaternion.identity, _meshSize);

            _matrixArrays[thousands][subIndex] = _matrix;

            _collisionBendings[thousands][subIndex].x = 0;
            _collisionBendings[thousands][subIndex].z = 0;

            if (_collisionBendings[thousands][subIndex].y == 0)
            {
                _collisionBendings[thousands][subIndex].y = 1f;
            }
        }
    }

    private void Update()
    {
        var thousands = 0;

        UpdateCuttersPositions();
        UpdateFlattenersPositions();

        var zero = Vector4.zero;
        var deltaTime = Time.deltaTime;
        var count = _grassPositions.Count;

        for (var i = 0; i < count; i++)
        {
            var subIndex = i % 1000;

            if (i != 0 && subIndex == 0)
            {
                thousands += 1;
            }

            var column = _matrixArrays[thousands][subIndex].GetColumn(3);

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

    private void DrawGrassMeshes()
    {
        for (var i = 0; i < _matrixArrays.Length; i++)
        {
            _grassMaterialPropertyBlock.SetVectorArray(CollisionBending, _collisionBendings[i]);

            Graphics.DrawMeshInstanced(
                grassMesh,
                0,
                _grassShaderMaterial,
                _matrixArrays[i],
                _matrixArrays[i].Length,
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

    private void Cut(int thousands, int subindex, Vector4 column)
    {
        var count = _cutterPositions.Length;

        for (var i = 0; i < count; i++)
        {
            var position = _cutterPositions[i];
            var xDistance = column.x - position.x;
            var yDistance = column.y - position.y;
            var zDistance = column.z - position.z;

            var abc = new Vector3(xDistance, yDistance, zDistance);
            var isGrassGrowedEnough = _collisionBendings[thousands][subindex].y > 0.4f;

            if (abc.sqrMagnitude < _grassCutters[i].CutDistance && isGrassGrowedEnough)
            {
                _collisionBendings[thousands][subindex].y = .0f;

                var growSpeed = Random.Range(_growSpeedRange.x, _growSpeedRange.y);
                var growDelay = Random.Range(_growDelayRange.x, _growDelayRange.y);

                StartCoroutine(Regrow(growDelay, growSpeed, thousands, subindex));
                PlayCutParticle(column);
            }
        }
    }

    private void PlayCutParticle(Vector4 cutPos)
    {
        foreach (var cutParticleSystem in _cutParticleSystems)
        {
            var mainModule = cutParticleSystem.main;
            
            var color = Color.Lerp(
                _grassShaderMaterial.GetColor(TintColor1), 
                _grassShaderMaterial.GetColor(TintColor2),
                Random.Range(_colorRange.x, _colorRange.y));
            
            mainModule.startColor = color;

            cutParticleSystem.transform.position = cutPos;
            cutParticleSystem.Emit(1);
        }
    }

    private IEnumerator Regrow(float delay, float speed, int thousands, int subIndex)
    {
        yield return new WaitForSeconds(delay);

        while (_collisionBendings[thousands][subIndex].y < 1)
        {
            _collisionBendings[thousands][subIndex].y += speed * Time.deltaTime;
            yield return null;
        }
    }

    private void Flatten(int thousands, int subindex, Vector4 column, float deltaTime, Vector4 zero)
    {
        var count = _flattenerPositions.Length;

        for (var i = 0; i < count; i++)
        {
            var position = _flattenerPositions[i];
            var xDistance = column.x - position.x;
            var yDistance = column.y - position.y;
            var zDistance = column.z - position.z;

            var lerpTarget = zero;

            var distanceToFlattener = xDistance * xDistance + yDistance * yDistance + zDistance * zDistance;

            if (distanceToFlattener > _grassFlatteners[i].FlattenDistance)
            {
                // Raise

                var lerpSpeed = deltaTime * _raiseSpeed;
                var lerped = Vector4.LerpUnclamped(_collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);
                _collisionBendings[thousands][subindex].x = lerped.x;
                _collisionBendings[thousands][subindex].z = lerped.z;
                continue;
            }

            var abc = new Vector2(xDistance, zDistance).normalized * _grassFlatteners[i].BendForce;
            var bend = abc * Mathf.Clamp(1f / abc.sqrMagnitude, 0, 1);

            // Lay down
            Vector2 currentDirection;

            currentDirection.x = _collisionBendings[thousands][subindex].x;
            currentDirection.y = _collisionBendings[thousands][subindex].z;

            if (currentDirection.sqrMagnitude <= 0.4f)
            {
                lerpTarget.x = bend.x;
                lerpTarget.z = bend.y;

                var lerpSpeed = deltaTime * _flattenSpeed;
                var lerped = Vector4.LerpUnclamped(_collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);
                _collisionBendings[thousands][subindex].x = lerped.x;
                _collisionBendings[thousands][subindex].z = lerped.z;
            }
            else if (bend.sqrMagnitude > currentDirection.sqrMagnitude)
            {
                var newVec = currentDirection.normalized * bend.magnitude;
                lerpTarget.x = newVec.x;
                lerpTarget.z = newVec.y;

                var lerpSpeed = deltaTime * _flattenSpeed;
                var lerped = Vector4.LerpUnclamped(_collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);
                _collisionBendings[thousands][subindex].x = lerped.x;
                _collisionBendings[thousands][subindex].z = lerped.z;
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