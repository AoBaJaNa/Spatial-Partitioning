using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.Mathematics;
using System.Linq;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine.Jobs;
public sealed class UnitNativeData : IDisposable
{
    bool _dispose;
    public NativeArray<float3> pos;
    public NativeArray<float3> targetPos ;
    public NativeArray<float> speed;


    public UnitNativeData(int count)
    {
        pos = new NativeArray<float3>(count, Allocator.Persistent);
        targetPos = new NativeArray<float3>(count, Allocator.Persistent);
        speed = new NativeArray<float>(count, Allocator.Persistent);
    }
    public void Dispose()
    {
        if (_dispose)
            return;
        if(pos.IsCreated)pos.Dispose();
        if(targetPos.IsCreated)targetPos.Dispose();
        if(speed.IsCreated)speed.Dispose();

        _dispose = true;

        GC.SuppressFinalize(this);
    }
}
public class UnitMovementManager : MonoBehaviour
{
    private static readonly ProfilerMarker AwakeMarker =
        new ProfilerMarker("RTS.UnitMovementManager.Awake");
    private static readonly ProfilerMarker StartMarker =
        new ProfilerMarker("RTS.UnitMovementManager.Start");
    private static readonly ProfilerMarker UpdateMarker =
        new ProfilerMarker("RTS.UnitMovementManager.Update");
    private static readonly ProfilerMarker ScheduleJobMarker =
        new ProfilerMarker("RTS.UnitMovementManager.ScheduleJob");
    private static readonly ProfilerMarker CompleteJobMarker =
        new ProfilerMarker("RTS.UnitMovementManager.CompleteJob");
    private static readonly ProfilerMarker ManagerCalcMarker =
        new ProfilerMarker("RTS.ManagerCalc");
    private static readonly ProfilerMarker ManagerTransformMarker =
    new ProfilerMarker("RTS.ManagerTransform");

    private List<UnitMovement> unitMovements = new();
    private UnitNativeData unitNativeData;
    private Transform[] unitsTransform;
    private TransformAccessArray transformAccessArray;
    private int unitCount = 0;

    SpatialTestManager testManager;
    private void Awake()
    {
        using (AwakeMarker.Auto())
        {
            testManager = GetComponent<SpatialTestManager>();
            unitMovements = GetComponentsInChildren<UnitMovement>().ToList();
        }
    }
    void Start()
    {
        using (StartMarker.Auto())
        {
        unitCount = unitMovements.Count;
        unitsTransform = new Transform[unitCount];
        unitNativeData = new UnitNativeData(unitCount);


        for (int i = 0; i < unitCount; i++)
        {
            UnitMovement unit = unitMovements[i];
            unit.Index = i;
            unitNativeData.pos[i] = unit.transform.position;
            unitNativeData.targetPos[i] = unit.transform.position;
            unitNativeData.speed[i] = testManager.moveSpeed;
            unitsTransform[i] = unit.transform;
        }
        transformAccessArray = new TransformAccessArray(unitsTransform);
        }
    }
    public void SetTargetPos(int index, Vector3 pos)
    {
        unitNativeData.targetPos[index] = pos;
    }
    public void SetSpeed(int index, float speed)
    {
        unitNativeData.speed[index] = speed;
    }
    void Update()
    {
        UnitMoveTransformJob unitMoveTransformJob = new UnitMoveTransformJob(unitNativeData.pos, unitNativeData.targetPos, unitNativeData.speed, Time.deltaTime);

        JobHandle handle;
        handle = unitMoveTransformJob.Schedule(transformAccessArray);

        handle.Complete();
/*        using (UpdateMarker.Auto())
        {
            UnitMoveJob unitMoveJob = new UnitMoveJob(unitNativeData.pos, unitNativeData.targetPos, unitNativeData.speed, Time.deltaTime);

            JobHandle handle;
            using (ScheduleJobMarker.Auto())
            {
                handle = unitMoveJob.Schedule(unitCount, 64);

            }

            using (CompleteJobMarker.Auto())
            {
                handle.Complete();
            }

        }
        using (ManagerTransformMarker.Auto())
        {
            for (int i = 0; i < unitCount; i++)
            {
                unitsTransform[i].position = unitNativeData.pos[i];
            }
        }*/

        /*        using (ManagerCalcMarker.Auto())
                {
                    float dt = Time.deltaTime;

                    for (int i = 0; i < unitCount; i++)
                    {
                        float3 current = unitNativeData.pos[i];
                        float3 target = unitNativeData.targetPos[i];
                        float speed = unitNativeData.speed[i];

                        float3 dir = target - current;
                        float dist = math.length(dir);
                        float step = speed * dt;

                        if (dist <= 0.001f || dist <= step)
                            unitNativeData.pos[i] = target;
                        else
                            unitNativeData.pos[i] = current + (dir / dist) * step;
                    }
                }*/
    }
    private void OnDestroy()
    {
        unitNativeData?.Dispose();
        transformAccessArray.Dispose();
    }

    public float3 MoveToWards(float3 current, float3 target, float speed)
    {
        float3 dir = target - current;
        float dist = math.length(dir);
        float step =  speed * Time.deltaTime;

        if (dist <= 0.001f || dist <= step)
            return target;
        else
            return current + (dir / dist) * step;
        
    }
}
