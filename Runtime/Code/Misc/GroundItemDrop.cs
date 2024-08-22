using UnityEngine;
using UnityEngine.Scripting;
using VoxelWorldStuff;

[LuauAPI]
[Preserve]
public class GroundItemDrop : MonoBehaviour {
    private const float DestroyHeight = -100f;
    
    [SerializeField] private float gravityFactor = 1f;
    [SerializeField] private float bounceFactor = 1f;
    [SerializeField] private LayerMask blockMask;

    private static VoxelWorld voxelWorld;
    private static bool searchedForVoxelWorld = false;

    private Vector3 _velocity = Vector3.zero;
    private bool _grounded;
    private bool _tryUnground;
    private float _allowGround;
    public BoxCollider boxCollider;
    private Vector3 _boundsExtents;

    private bool _boxHit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reload() {
        searchedForVoxelWorld = false;
    }

    private void OnEnable() {
        if (!searchedForVoxelWorld) {
            searchedForVoxelWorld = true;
            
            voxelWorld = VoxelWorld.GetFirstInstance(); 
        }

        if (voxelWorld) {
            voxelWorld.VoxelPlaced += OnVoxelPlaced;
            voxelWorld.VoxelChunkUpdated += OnVoxelChunkUpdated;
        }

        _grounded = false;
        _allowGround = Time.time + 0.1f;
        _boundsExtents = boxCollider.bounds.extents;
        _boundsExtents.y /= 2f;
    }

    private void OnDisable() {
        if (voxelWorld) {
            voxelWorld.VoxelPlaced -= OnVoxelPlaced;
            voxelWorld.VoxelChunkUpdated -= OnVoxelChunkUpdated;
        }
    }

    private RaycastHit _result;
    private void Update() {
        var t = transform;
        var pos = t.position;
        if (pos.y < DestroyHeight) {
            Destroy(gameObject);
        }
        
        if (_grounded && !_tryUnground) return;
        
        var rot = t.rotation;
        
        _velocity.y += Physics.gravity.y * Time.deltaTime * gravityFactor;
        var movement = _velocity * Time.deltaTime;

        var boxHit = Physics.BoxCast(pos, _boundsExtents, _velocity.normalized, out _result, rot, 10f, blockMask);
        
        _boxHit = boxHit;
        
        if (boxHit && _result.distance > movement.magnitude) {
            boxHit = false;
        }

        if (_tryUnground) {
            if (!boxHit) {
                _tryUnground = false;
                _grounded = false;
                _allowGround = Time.time + 0.1f;
            } else {
                if (Time.time > _allowGround) {
                    _tryUnground = false;
                }
                return;
            }
        }

        var newPos = boxHit ? _result.point : pos + movement;
        
        // Bump out of collision boxes:
        var hitNonTopSurface = false;
        if (boxHit) {
            if (Physics.ComputePenetration(this.boxCollider, newPos, rot, _result.collider, _result.transform.position,
                    _result.transform.rotation, out var dir, out var dist)) {
                if (Vector3.Dot(_result.normal, Vector3.up) < 0.95f && dist > 0.05f) {
                    newPos += dir * dist;
                    hitNonTopSurface = true;
                }

                // Bounce velocity, but only if not hitting top surface (otherwise it bounces on the ground like a bouncy ball):
                if (hitNonTopSurface && Vector3.Dot(_velocity.normalized, _result.normal) < 0f) {
                    var newDirection = Vector3.Reflect(_velocity.normalized, _result.normal);
                    _velocity = newDirection * (_velocity.magnitude * bounceFactor);
                }
            }
        }
        
        // Mark item as grounded:
        if (boxHit && !hitNonTopSurface && _velocity.y <= 0f) {
            newPos.y = _result.point.y + this.boxCollider.bounds.extents.y;
            if (Time.time >= _allowGround) {
                _grounded = true;
            }
            _velocity = Vector3.zero;
        }

        transform.position = newPos;
    }

    private void BumpUpwardsIfNeeded(Vector3 voxelPosition) {
        for (var y = 0; y < 1000; y++) {
            // Check for intersection across adjacent voxels:
            var intersects = false;
            for (var x = -1; x <= 1; x++) {
                for (var z = -1; z <= 1; z++) {
                    var v = voxelWorld.GetVoxelAt(voxelPosition + new Vector3(x, y, z));
                    if (v == 0) continue;
                    var voxelBounds = new Bounds(voxelPosition + new Vector3(x, y, z), Vector3.one);
                    intersects = this.boxCollider.bounds.Intersects(voxelBounds);
                    if (intersects) break;
                }
                if (intersects) break;
            }
            
            if (!intersects) break;
            _grounded = true;
            transform.position += Vector3.up;
        }
    }

    private void OnVoxelPlaced(object oVoxel, object ox, object oy, object oz) {
        if (!_grounded) return;
        
        // Ensure it was a block removal:
        if (voxelWorld.GetCollisionType((ushort)oVoxel) != VoxelBlocks.CollisionType.None) return;
        
        var pos = new Vector3((int)ox + 0.5f, (int)oy + 0.5f, (int)oz + 0.5f);
        var sqrDist = Vector3.SqrMagnitude(pos - transform.position);
        if (sqrDist < 4f) {
            var x = (int)ox;
            var y = (int)oy;
            var z = (int)oz;
            var voxelBounds = new Bounds(pos, Vector3.one);
            if (this.boxCollider.bounds.Intersects(voxelBounds)) {
                _tryUnground = true;
                _allowGround = Time.time + 1f;
            }
        }
    }

    private void OnVoxelChunkUpdated(Chunk chunk) {
        if (!_grounded) return;
        if (!this.boxCollider.bounds.Intersects(chunk.bounds)) return;
        
        BumpUpwardsIfNeeded(transform.position);
    }

    private void OnDrawGizmos() {
        Gizmos.color = _grounded ? Color.green : _boxHit ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, _grounded ? 0.5f : 0.7f);
        if (!_grounded) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, _velocity.normalized);
        }
    }

#region Luau APIs
    public bool IsGrounded() {
        return _grounded;
    }

    public void SetGrounded(bool grounded) {
        _grounded = grounded;
    }

    public void SetPosition(Vector3 position) {
        transform.position = position;
    }

    public void SetVelocity(Vector3 velocity) {
        _velocity = velocity;
    }

    public void SetSpinActive(bool active) {
        var spinner = this.gameObject.GetComponentInChildren<EasySpinner>();
        if (spinner != null) {
            spinner.enabled = active;
        }
    }

    public Vector3 GetVelocity() {
        return _velocity;
    }
#endregion
}
