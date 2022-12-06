using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class GrassGPUInstancing : MonoBehaviour
{
    private static readonly int CollisionBending = Shader.PropertyToID("_CollisionBending");
    public Mesh grassMesh;
    public Material grassMaterial;
    public Collider[] grassFlatteners = new Collider[20];
    public Collider[] grassCutters = new Collider[5];
    public Vector3[] grassPositions;

    // Particle System stuff
    public ParticleSystem particleSys;
    [SerializeField] private float flattenDistance;
    [SerializeField] private bool useFlatteners;
    [SerializeField] private float growSpeed;
    [SerializeField] private float cutDistance;
    [SerializeField] private Vector3 meshSize;
    private readonly List<Vector4[]> collisionBendings = new();
    private readonly Vector3[] cutPositions = new Vector3[1000];
    private readonly List<Matrix4x4[]> matrixArrays = new();

    private int cutAmount;
    private Matrix4x4 matrix;
    private ParticleSystem.Particle[] particleArray;
    private MaterialPropertyBlock propertyBlock;

    private void Start()
    {
        particleArray = new ParticleSystem.Particle[particleSys.main.maxParticles];

        propertyBlock = new MaterialPropertyBlock();

        matrixArrays.Clear();
        collisionBendings.Clear();

        for (var i = 0; i < grassPositions.Length / 1000 + 1; i++)
        {
            matrixArrays.Add(new Matrix4x4[1000]);
            collisionBendings.Add(new Vector4[1000]);
        }

        var thousands = 0;

        for (var i = 0; i < grassPositions.Length; i++)
        {
            var subIndex = i % 1000;

            if (i != 0 && subIndex == 0)
            {
                thousands += 1;
            }

            var randomRotation = Random.rotation;
            randomRotation.x = 0;
            randomRotation.z = 0;

            matrix.SetTRS(grassPositions[i], Quaternion.identity, meshSize);

            matrixArrays[thousands][subIndex] = matrix;

            var xbend = 0f;
            var zbend = 0f;

            collisionBendings[thousands][subIndex].x = xbend;
            collisionBendings[thousands][subIndex].z = zbend;

            if (collisionBendings[thousands][subIndex].y == 0)
            {
                collisionBendings[thousands][subIndex].y = 1f;
            }
        }
    }

    // struct CutJob : IJobParallelFor
    // {
    //     private NativeArray<NativeArray<Matrix4x4>> matrixArrays;
    //     private NativeArray<NativeArray<Vector4>> collisionBendings;
    //     private NativeArray<Vector3> cutterPositions;
    //     private int thousands;
    //     private float cutDistance;
    //     
    //     public void Execute(int i)
    //     {
    //         var subindex = i % 1000;
    //
    //         if (i != 0 && subindex == 0)
    //         {
    //             thousands += 1;
    //         }
    //         
    //         foreach (var position in cutterPositions)
    //         {
    //             var xDistance = matrixArrays[thousands][subindex].GetColumn(3).x - position.x;
    //             var yDistance = matrixArrays[thousands][subindex].GetColumn(3).y - position.y;
    //             var zDistance = matrixArrays[thousands][subindex].GetColumn(3).z - position.z;
    //
    //             var abc = new Vector3(xDistance, yDistance, zDistance);
    //
    //             var collisionBendingsThousands = collisionBendings[thousands];
    //             var collisionBending = collisionBendings[thousands][subindex];
    //
    //             if (abc.sqrMagnitude < cutDistance && collisionBending.y > 0.4f)
    //             {
    //                 // Cut the Grass:
    //                 
    //                 collisionBending.y = .2f;
    //
    //                 collisionBendingsThousands[subindex] = collisionBending;
    //                 collisionBendings[thousands] = collisionBendingsThousands;
    //                 
    //                 cutPositions[cutAmount++] = matrixArrays[thousands][subindex].GetColumn(3);
    //             }
    //         }
    //     }
    // }
    private void Update()
    {
        var thousands = 0;
        cutAmount = 0;

        var cutterPositions = grassCutters.Select(x => x.transform.position).ToArray();
       
        for (var i = 0; i < grassPositions.Length; i++)
        {
            var subindex = i % 1000;

            if (i != 0 && subindex == 0)
            {
                thousands += 1;
            }

            if (useFlatteners)
            {
                Flatten(thousands, subindex);
            }

            // Check for Cutting
            var column = matrixArrays[thousands][subindex].GetColumn(3);
            
            foreach (var position in cutterPositions)
            {
              
                var xDistance = column.x - position.x;
                var yDistance = column.y - position.y;
                var zDistance = column.z - position.z;

                var abc = new Vector3(xDistance, yDistance, zDistance);

                if (abc.sqrMagnitude < cutDistance && collisionBendings[thousands][subindex].y > 0.4f)
                {
                    // Cut the Grass:
                    collisionBendings[thousands][subindex].y = .2f;
                    cutPositions[cutAmount++] = column;
                }
            }

            // regrow Grass 
            if (collisionBendings[thousands][subindex].y < 1f)
            {
                collisionBendings[thousands][subindex].y += growSpeed * Time.deltaTime;
            }
        }

        // Because we have multiple grass blades per mesh, we increase the single_grass particles per Grass cut:
        cutAmount *= 6;

        // Particle Effect:
        if (cutAmount > 0)
        {
            var old_amount = particleSys.particleCount;
            particleSys.Emit(cutAmount);
        
            var newAmount = particleSys.GetParticles(particleArray);
        
            for (var i = old_amount; i < old_amount + cutAmount; i++)
            {
                particleArray[i].position = cutPositions[(i - old_amount) / 6];
            }
        
            particleSys.SetParticles(particleArray, newAmount);
        }

        for (var i = 0; i < matrixArrays.Count; i++)
        {
            propertyBlock.SetVectorArray(CollisionBending, collisionBendings[i]);

            Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, matrixArrays[i], matrixArrays[i].Length,
                propertyBlock, ShadowCastingMode.Off, false, 0, null);
        }
    }

    private void Flatten(int thousands, int subindex)
    {
        foreach (var grassFlattener in grassFlatteners)
        {
            var position = grassFlattener.transform.position;
            var xDistance = matrixArrays[thousands][subindex].GetColumn(3).x - position.x;
            var yDistance = matrixArrays[thousands][subindex].GetColumn(3).y - position.y;
            var zDistance = matrixArrays[thousands][subindex].GetColumn(3).z - position.z;
            var abc = new Vector3(xDistance, yDistance, zDistance);

            var bend = abc.normalized * Mathf.Clamp(1f / abc.sqrMagnitude, 0, 1);

            var lerpTarget = Vector4.zero;
            Vector4 lerped;

            if (xDistance * xDistance + yDistance * yDistance + zDistance * zDistance < flattenDistance)
            {
                // Lay down
                Vector2 currentDirection;

                currentDirection.x = collisionBendings[thousands][subindex].x;
                currentDirection.y = collisionBendings[thousands][subindex].z;

                if (currentDirection.sqrMagnitude <= 0.4f)
                {
                    lerpTarget.x = bend.x;
                    lerpTarget.z = bend.y;

                    var lerpSpeed = Time.deltaTime * 10f;
                    lerped = Vector4.Lerp(collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);

                    // Apply changes
                    collisionBendings[thousands][subindex].x = lerped.x;
                    collisionBendings[thousands][subindex].z = lerped.z;
                }

                if (currentDirection.sqrMagnitude > 0.4f && bend.sqrMagnitude > currentDirection.sqrMagnitude)
                {
                    var newVec = currentDirection.normalized * bend.magnitude;
                    lerpTarget.x = newVec.x;
                    lerpTarget.z = newVec.y;

                    var lerpSpeed = Time.deltaTime * 10f;
                    lerped = Vector4.Lerp(collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);

                    // Apply changes
                    collisionBendings[thousands][subindex].x = lerped.x;
                    collisionBendings[thousands][subindex].z = lerped.z;
                }
            }
            else
            {
                // Raise
                var lerpSpeed = Time.deltaTime * .12f;
                lerped = Vector4.Lerp(collisionBendings[thousands][subindex], lerpTarget, lerpSpeed);

                // Apply changes
                collisionBendings[thousands][subindex].x = lerped.x;
                collisionBendings[thousands][subindex].z = lerped.z;
            }
        }
    }
}