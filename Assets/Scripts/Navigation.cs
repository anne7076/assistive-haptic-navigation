using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Navigation : MonoBehaviour
{
    [Header("Waypoints")]
    public List<GameObject> waypoints = new List<GameObject>();
    public float reachDistance = 1.0f;
    public int currentWaypointIndex = 0;

    [Header("NavMesh Pathfinding (Step 7 Bonus)")]
    public bool useNavMesh = true;
    public Transform destination;
    private NavMeshPath path;
    private bool _destinationReached = false;

    public bool AllWaypointsCompleted =>
        waypoints != null && waypoints.Count > 0 && currentWaypointIndex >= waypoints.Count;

    public Transform CurrentTarget
    {
        get
        {
            if (waypoints != null && waypoints.Count > 0)
            {
                if (currentWaypointIndex >= 0 && currentWaypointIndex < waypoints.Count && waypoints[currentWaypointIndex] != null)
                    return waypoints[currentWaypointIndex].transform;
                return null;
            }
            return !_destinationReached ? destination : null;
        }
    }

    public bool HasActiveTarget => CurrentTarget != null;

    /// <summary>
    /// Returns the immediate walkable steering point along the NavMesh path (or the target itself).
    /// </summary>
    public Vector3 GetNextSteeringTarget()
    {
        if (useNavMesh && path != null && path.corners != null && path.corners.Length > 1)
        {
            return path.corners[1];
        }
        Transform target = CurrentTarget;
        return target != null ? target.position : transform.position;
    }

    /// <summary>
    /// Resets waypoint progression back to the first checkpoint.
    /// </summary>
    public void ResetWaypoints()
    {
        currentWaypointIndex = 0;
        _destinationReached = false;
        UpdatePath();
    }

    void Start()
    {
        path = new NavMeshPath();
        UpdatePath();
    }

    void Update()
    {
        Transform target = CurrentTarget;
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance < reachDistance)
            {
                if (waypoints != null && waypoints.Count > 0)
                {
                    Debug.Log($"[Navigation] Reached waypoint {currentWaypointIndex + 1}/{waypoints.Count} ({target.name})!");
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= waypoints.Count)
                    {
                        Debug.Log("[Navigation] All waypoints completed!");
                    }
                }
                else
                {
                    Debug.Log($"[Navigation] Reached destination ({target.name})!");
                    _destinationReached = true;
                }
                UpdatePath();
            }
            else if (useNavMesh && (Time.frameCount % 5 == 0))
            {
                UpdatePath();
            }
        }
    }

    public void UpdatePath()
    {
        if (path == null) path = new NavMeshPath();
        Transform target = CurrentTarget;
        if (target != null)
        {
            NavMeshHit hitStart, hitEnd;
            if (NavMesh.SamplePosition(transform.position, out hitStart, 4.0f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(target.position, out hitEnd, 4.0f, NavMesh.AllAreas))
            {
                NavMesh.CalculatePath(hitStart.position, hitEnd.position, NavMesh.AllAreas, path);
            }
            else
            {
                path.ClearCorners();
            }
        }
        else
        {
            path.ClearCorners();
        }
    }

    void OnDrawGizmos()
    {
        // 1. Draw NavMesh path in GREEN curving around walls (Slide 39)
        if (useNavMesh && path != null && path.corners != null && path.corners.Length > 0)
        {
            Color origColor = Gizmos.color;
            Gizmos.color = Color.green;
            for (int i = 0; i < path.corners.Length; i++)
            {
                Vector3 pt = path.corners[i] + Vector3.up * 0.15f; // slightly above ground for visibility
                Gizmos.DrawSphere(pt, 0.1f);
                if (i > 0)
                {
                    Vector3 prevPt = path.corners[i - 1] + Vector3.up * 0.15f;
                    Gizmos.DrawLine(prevPt, pt);
                }
            }
            Gizmos.color = origColor;
        }
        else
        {
            // Fallback straight line if NavMesh is off
            Transform target = CurrentTarget;
            if (target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }

        // 2. Draw remaining waypoints connecting path in yellow
        if (waypoints != null && waypoints.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].transform.position, waypoints[i + 1].transform.position);
                    Gizmos.DrawWireSphere(waypoints[i].transform.position, 0.15f);
                }
            }
            if (waypoints[waypoints.Count - 1] != null)
            {
                Gizmos.DrawWireSphere(waypoints[waypoints.Count - 1].transform.position, 0.15f);
            }
        }
    }
}
