using System;
using Unity.VisualScripting;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    public InputManager input;
    public float maxForce = 1f;
    public float maxVelocity = 5f;
    public float rotateSmooth = 1f;

    public float gravity = -10f;
    public float floatForce = 5f;
    public float floatHeight = 0.2f;
    public LayerMask groundMask; 
        
    private Transform _camera;
    private Rigidbody _rigidbody;
    private SphereCollider _collider;
    // Start is called before the first frame update
    private void Start()
    {
        if (Camera.main)
        {
            _camera = Camera.main.transform;
        }
        else
        {
            Debug.LogError("[Error] There is no camera in the scene!");
        }
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<SphereCollider>();
    }
    
    
    // Update is called once per frame
    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        var moveInput = input.Move;
        Vector3 force;
        var grounded = GroundCheck(out var normal, out var distance);
        if (grounded)
        {
            force = new Vector3(0f, (floatHeight - distance) * floatForce, 0f);            
        }
        else
        {
            force = new Vector3(0, gravity, 0);
        }
        
        if (moveInput.sqrMagnitude > 0)
        {
            var forward = Vector3.ProjectOnPlane(_camera.forward, normal).normalized;
            var right = Vector3.ProjectOnPlane(_camera.right, normal).normalized;

            var moveDirection = forward * moveInput.y + right * moveInput.x;
            
            force += moveDirection * maxForce;
            transform.rotation = Quaternion.Slerp(
                a: transform.rotation, 
                b: Quaternion.LookRotation(moveDirection),
                t: rotateSmooth * Time.fixedDeltaTime);
        }

        _rigidbody.AddForce(force);
        
        if (_rigidbody.velocity.magnitude > maxVelocity)
            _rigidbody.velocity = _rigidbody.velocity.normalized * maxVelocity;
    }

    private RaycastHit[] _groundCheckResult = new RaycastHit[1];
    
    private bool GroundCheck(out Vector3 normal, out float distance)
    {
        var count = Physics.SphereCastNonAlloc(
            origin: transform.TransformPoint(_collider.center),
            radius: _collider.radius,
            results: _groundCheckResult,
            direction: Vector3.down,
            maxDistance: 1f,
            layerMask: groundMask
        );
        if (count > 0)
        {
            normal = _groundCheckResult[0].normal;
            distance = _groundCheckResult[0].distance; 
            return true;
        }
        normal = Vector3.up;
        distance = -1;
        return false;
    }
}
