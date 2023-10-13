using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;

[Serializable]
public struct StepTargetInfo
{
    public Transform target;
    public Transform ikTarget;
    public float verticalOffset;
    public float rayDistance;
    public float minBound;
    public float maxBound;
    public float smoothTime;
    public float stepHeight;
}
public enum EUpdateMode
{
    Simple,// 离开范围就开始更新
    AllTogether,// 任意离开范围，全部更新
    ZigZag,// 同时更新间隔的
    Sequential// 按顺序依次更新
}
public class ProceduralStep : MonoBehaviour
{
    [Header("Steps")]
    [Tooltip("更新模式\n目前只实现了 Sequential")] public EUpdateMode updateMode;
    [Tooltip("地面对应的LayerMask")] public LayerMask groundLayer;
    [Tooltip("移动时跟踪点的偏移")] public float stepDistanceRatio = 1f;
    [Tooltip("达到最大偏移所需要的速度的平方")] public float maxOffsetSqrVelocity = 25f;
    [Tooltip("更新跟踪点时新位置在移动方向上的偏移")] public float velocityOffsetMultiply = 0.1f;
    [Tooltip("脚步信息")] public List<StepTargetInfo> stepTargetInfos;

    [Header("Body Rotation")]
    [Tooltip("躯干，更新旋转时会更新这个物体的")] public Transform body;
    [Tooltip("加速度旋转的最大角度")] public float maxAngle = 30f;
    
    
    private Rigidbody _rigidbody;
    private Vector3 _curVelocity;
    private Quaternion _accRotation;// 加速度旋转
    private Vector3 _acceleration;
    private void Start()
    {
        if (updateMode == EUpdateMode.ZigZag && stepTargetInfos.Count % 2 > 0)
        {
            Debug.LogWarning("[Warning] Update mode is ZigZag, but the count of StepTargetInfo is odd.");
        }

        _rigidbody = GetComponent<Rigidbody>();
        _curVelocity = _rigidbody.velocity;
    }
    
    private void Update()
    {
        UpdateStep();
        UpdateBodyRotation();
    }

    
    private void FixedUpdate()
    {
        var curVelocity = _rigidbody.velocity;
        _acceleration = (curVelocity - _curVelocity)/Time.fixedDeltaTime;
        _curVelocity = curVelocity;
        
        
    }

    public float bodyPositionSmooth = 1;
    public float bodyRotationSmooth = 1;
    public float angleSmooth = 10f;
    private void UpdateBodyRotation()
    {
        var bodyPosition = body.position;
        CalculateUpAxis(out var center, out var up);
        var offset = bodyPosition - center;
        var forward = Vector3.Cross(body.right, up);

        var offsetRotation = Quaternion.LookRotation(forward, up);
        _accRotation = Quaternion.AngleAxis(
            angle: Mathf.Clamp(angleSmooth * _acceleration.sqrMagnitude, -maxAngle, maxAngle), 
            axis: offsetRotation * Vector3.Cross(Vector3.up, _acceleration));
        
        var targetPosition = center;
        var targetRotation = offsetRotation * _accRotation;

        body.position = Vector3.Lerp(bodyPosition, targetPosition, bodyPositionSmooth * Time.deltaTime);
        body.rotation = Quaternion.Slerp(body.rotation, targetRotation, bodyRotationSmooth * Time.deltaTime);
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        for (var i = 0; i < stepTargetInfos.Count; i++)
        {
            Gizmos.color = Color.green;
            if (GetTargetPosition(i, out var pos, out var _))
                Gizmos.DrawWireSphere(
                    center: pos,
                    radius: Mathf.Lerp(
                        a: stepTargetInfos[i].minBound, b: stepTargetInfos[i].maxBound,
                        t: _rigidbody.velocity.sqrMagnitude / maxOffsetSqrVelocity));
        }
    }
    #endif

    private int _infoPointer;
    private bool _updating;
    private RaycastHit[] _results = new RaycastHit[1];

    private bool GetTargetPosition(int index, out Vector3 pos, out bool outOfBound)
    {
        // cache info
        var info = stepTargetInfos[index];
        
        // RayCast
        var count = Physics.RaycastNonAlloc(
            origin: info.target.position +
                    Vector3.up * info.verticalOffset + 
                    _rigidbody.velocity * velocityOffsetMultiply,
            direction: Vector3.down, 
            results: _results,
            maxDistance: info.rayDistance,
            layerMask: groundLayer
        );
        
        // handle if hit something
        if (count > 0)
        {
            var curPos = info.ikTarget.position;
            var bound = Mathf.Lerp(info.minBound, info.maxBound, _rigidbody.velocity.sqrMagnitude / maxOffsetSqrVelocity);
            pos = Vector3.LerpUnclamped(curPos, _results[0].point, stepDistanceRatio);
            outOfBound = Vector3.Distance(pos, curPos) > bound;
            return true;
        }
        
        // Didn't hit anything
        pos = Vector3.zero;
        outOfBound = false;
        return false;
    }
    private void UpdateStep()
    {
        switch (updateMode)
        {
            case EUpdateMode.Simple:
                break;
            case EUpdateMode.AllTogether:
                break;
            case EUpdateMode.ZigZag:
                break;
            case EUpdateMode.Sequential:
                if (_updating) break;
                if (GetTargetPosition(_infoPointer, out Vector3 pos, out bool outOfBound))
                {
                    if (outOfBound)
                    {
                        StartCoroutine(UpdateSteps(_infoPointer, pos));
                        _infoPointer++;
                        _infoPointer %= stepTargetInfos.Count;
                    }
                }
                break;
            default:
                Debug.LogError("[Error] Illegal update mode!");
                break;
        }
    }
    private IEnumerator UpdateSteps(int i, Vector3 destination)
    {
        // 设置 updating 为 true
        // set updating to true
        _updating = true;
        
        // 缓存信息
        // cache info
        var info = stepTargetInfos[i];

        // 初始化变量
        // Initialize variables
        var velocity = Vector3.zero;
        var timer = info.smoothTime;

        // 主循环
        // Main loop
        while (timer > 0)
        {
            var p = timer / info.smoothTime;
            info.ikTarget.position = Vector3.SmoothDamp(info.ikTarget.position, destination, ref velocity, timer) +
                                     4 * info.stepHeight * p * (1 - p) * Vector3.up;
            timer -= Time.deltaTime;
            yield return null;
        }

        // 结束循环后，直接将 IK 移动到目标点
        // Move IK Target to destination directly after loop
        info.ikTarget.position = destination;
        
        // 重设 updating 为 false
        // reset updating to false
        _updating = false;
    }
    
    private void CalculateUpAxis(out Vector3 center, out Vector3 up)
    {
        var pl = stepTargetInfos[0].ikTarget.position;
        var pr = stepTargetInfos[1].ikTarget.position;
        var pb = stepTargetInfos[2].ikTarget.position;
        
        center = (pl + pr + pb) / 3;
        
        var v1 = pl - pb;
        var v2 = pr - pb;
        
        up = Vector3.Cross(v1, v2).normalized;
    }
}
