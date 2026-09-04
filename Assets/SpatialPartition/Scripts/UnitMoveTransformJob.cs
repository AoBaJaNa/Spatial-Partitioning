using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct UnitMoveTransformJob : IJobParallelForTransform
{
    public NativeArray<float3> pos;
    [ReadOnly]
    public NativeArray<float3> targetPos;
    [ReadOnly]
    public NativeArray<float> speed;
    public float deltaTime;
    public UnitMoveTransformJob(NativeArray<float3> pos, NativeArray<float3> targetPos, NativeArray<float> speed, float deltaTime)
    {
        this.pos = pos;
        this.targetPos = targetPos;
        this.speed = speed;
        this.deltaTime = deltaTime;
    }

    public void Execute(int index, TransformAccess transform)
    {
        float3 current = pos[index];
        float3 target = targetPos[index];
        float currentSpeed = speed[index];

        float3 dir = target - current;
        float dist = math.length(dir);
        float step = currentSpeed * deltaTime;

        if (dist <= 0.001f || dist <= step)
            pos[index] = target;
        else
            pos[index] = current + (dir / dist) * step;

        transform.position = pos[index];
    }
}
