using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GrassGPUInstancing : MonoBehaviour
{
    private static readonly int CollisionBending = Shader.PropertyToID("_CollisionBending");
    [Space(2)] [Header("Grass settings")] [Space] [SerializeField]
    private Mesh grassMesh;
    [SerializeField] private Vector3 meshSize;
    [SerializeField] private float randomPositionOffset;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private ShadowCastingMode shadowCastingMode;
    [SerializeField] private Material grassMaterial;
    [Space(3)] [Header("Flatteners settings")] [Space] [SerializeField]
    private bool useFlatteners;
    [SerializeField] private float flattenDistance;
    [SerializeField] private float bendForce;
    [SerializeField] private Collider[] grassFlatteners = new Collider[20];
    [Space(2)] [Header("Cutters settings")] [Space] [SerializeField]
    private bool useCutters;
    [SerializeField] private Vector2 growSpeedRange;
    [SerializeField] private float growDelay;
    [SerializeField] private float cutDistance;
    [SerializeField] private Collider[] grassCutters = new Collider[5];
    [Space] [SerializeField] private Vector3[] grassPositions;
    [Space] [SerializeField] private ParticleSystem cutParticleSystem;

    private readonly List<Vector4[]> _collisionBendings = new();
    private readonly Vector3[] _cutPositions = new Vector3[1000];
    private readonly List<Matrix4x4[]> _matrixArrays = new();
    private int _cutAmount;
    private List<Vector3> _cutterPositions = new();
    private List<Vector3> _flattenerPositions = new();
    private MaterialPropertyBlock _grassMaterialPropertyBlock;
    private Matrix4x4 _matrix;

    private void Start()
    {
        _grassMaterialPropertyBlock = new MaterialPropertyBlock();

        for (var i = 0; i < grassPositions.Length / 1000 + 1; i++)
        {
            _matrixArrays.Add(new Matrix4x4[1000]);
            _collisionBendings.Add(new Vector4[1000]);
        }

        var thousands = 0;

        for (var i = 0; i < grassPositions.Length; i++)
        {
            var subIndex = i % 1000;

            if (i != 0 && subIndex == 0)
            {
                thousands += 1;
            }

            // var randomRotation = Random.rotation;
            // randomRotation.x = 0;
            // randomRotation.z = 0;

            var randomOffset = Random.insideUnitSphere * randomPositionOffset;
            randomOffset.y = 0;

            _matrix.SetTRS(grassPositions[i] + randomOffset + positionOffset, Quaternion.identity, meshSize);

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
        _cutAmount = 0;

        UpdateCuttersPositions();
        UpdateFlattenersPositions();

        for (var i = 0; i < grassPositions.Length; i++)
        {
            var subIndex = i % 1000;

            if (i != 0 && subIndex == 0)
            {
                thousands += 1;
            }

            var column = _matrixArrays[thousands][subIndex].GetColumn(3);

            if (useFlatteners)
            {
                Flatten(thousands, subIndex, column);
            }

            if (useCutters)
            {
                Cut(thousands, subIndex, column);
            }
        }

        if (_cutAmount > 0 && cutParticleSystem != null)
        {
            PlayCutParticle();
        }

        DrawGrassMeshes();
    }

    public void UpdateGrassPositions(IReadOnlyList<Vector3> positions)
    {
        grassPositions = new Vector3[positions.Count];

        for (var i = 0; i < positions.Count; i++)
        {
            grassPositions[i] = positions[i];
        }
    }

    private void DrawGrassMeshes()
    {
        for (var i = 0; i < _matrixArrays.Count; i++)
        {
            _grassMaterialPropertyBlock.SetVectorArray(CollisionBending, _collisionBendings[i]);

            Graphics.DrawMeshInstanced(
                grassMesh,
                0,
                grassMaterial,
                _matrixArrays[i],
                _matrixArrays[i].Length,
                _grassMaterialPropertyBlock,
                shadowCastingMode,
                false,
                0,
                null);
        }
    }

    private void PlayCutParticle()
    {
        for (var i = 0; i < _cutAmount; i++)
        {
            cutParticleSystem.transform.position = _cutPositions[i];
            cutParticleSystem.Play();
        }
    }

    private void UpdateFlattenersPositions()
    {
        if (_flattenerPositions.Count != grassFlatteners.Length)
        {
            _flattenerPositions = new List<Vector3>(grassFlatteners.Length);

            foreach (var grassFlattener in grassFlatteners)
            {
                _flattenerPositions.Add(grassFlattener.transform.position);
            }
        }

        for (var i = 0; i < grassFlatteners.Length; i++)
        {
            _flattenerPositions[i] = grassFlatteners[i].transform.position;
        }
    }

    private void UpdateCuttersPositions()
    {
        if (_cutterPositions.Count != grassCutters.Length)
        {
            _cutterPositions = new List<Vector3>(grassCutters.Length);

            foreach (var grassCutter in grassCutters)
            {
                _cutterPositions.Add(grassCutter.transform.position);
            }
        }

        for (var i = 0; i < grassCutters.Length; i++)
        {
            _cutterPositions[i] = grassCutters[i].transform.position;
        }
    }

    private void Cut(int thousands, int subindex, Vector4 column)
    {
        foreach (var position in _cutterPositions)
        {
            var xDistance = column.x - position.x;
            var yDistance = column.y - position.y;
            var zDistance = column.z - position.z;

            var abc = new Vector3(xDistance, yDistance, zDistance);

            if (abc.sqrMagnitude < cutDistance && _collisionBendings[thousands][subindex].y > 0.4f)
            {
                _collisionBendings[thousands][subindex].y = .0f;

                var cutPos = column;
                cutPos.y = position.y;
                _cutPositions[_cutAmount++] = cutPos;
                var growSpeed = Random.Range(growSpeedRange.x, growSpeedRange.y);
                StartCoroutine(Regrow(growDelay, growSpeed, thousands, subindex));
            }
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

    private void Flatten(int thousands, int subindex, Vector4 column)
    {
        foreach (var position in _flattenerPositions)
        {
            var xDistance = column.x - position.x;
            var yDistance = column.y - position.y;
            var zDistance = column.z - position.z;

            var lerpTarget = Vector4.zero;

            var distance = xDistance * xDistance + yDistance * yDistance + zDistance * zDistance;

            if (distance < flattenDistance)
            {
                var abc = new Vector2(xDistance, zDistance).normalized * bendForce;
                var bend = abc * Mathf.Clamp(1f / abc.sqrMagnitude, 0, 1);

                // Lay down
                Vector2 currentDirection;

                currentDirection.x = _collisionBendings[thousands][subindex].x;
                currentDirection.y = _collisionBendings[thousands][subindex].z;

                if (currentDirection.sqrMagnitude <= 0.4f)
                {
                    lerpTarget.x = bend.x;
                    lerpTarget.z = bend.y;

                    var lerpSpeed = Time.deltaTime * 10f;
                    var lerped = Vector4.Lerp(_collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);

                    // Apply changes
                    _collisionBendings[thousands][subindex].x = lerped.x;
                    _collisionBendings[thousands][subindex].z = lerped.z;
                }

                if (currentDirection.sqrMagnitude > 0.4f && bend.sqrMagnitude > currentDirection.sqrMagnitude)
                {
                    var newVec = currentDirection.normalized * bend.magnitude;
                    lerpTarget.x = newVec.x;
                    lerpTarget.z = newVec.y;

                    var lerpSpeed = Time.deltaTime * 10f;
                    var lerped = Vector4.Lerp(_collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);

                    // Apply changes
                    _collisionBendings[thousands][subindex].x = lerped.x;
                    _collisionBendings[thousands][subindex].z = lerped.z;
                }
            }
            else
            {
                // Raise
                var lerpSpeed = Time.deltaTime * .12f;
                var lerped = Vector4.Lerp(_collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);

                // Apply changes
                _collisionBendings[thousands][subindex].x = lerped.x;
                _collisionBendings[thousands][subindex].z = lerped.z;
            }
        }
    }
}