using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshFunctions
{
    private BoundsOctree<Triangle> navMeshTriangles;
    private float tiltAngle = 20f;
    private float tiltSpeed; // Calculate degrees per second
    private float tiltStep;
    private Camera playerCamera;
    private float surfaceSlopeThreshold = 10f; // Only consider triangles with slope > 10 degrees

    public NavMeshFunctions(NavMeshTriangulation navMeshTriangulation, Transform player)
    {
        var connectivityGraph = DeduplicateVerticesAndBuildGraph(navMeshTriangulation);
        var largerstComponent = GetLargestConnectedComponent(connectivityGraph.Item1, connectivityGraph.Item2, connectivityGraph.Item3);

        CreateTriangulation(navMeshTriangulation, largerstComponent);
        tiltSpeed = tiltAngle / 1f;
        tiltStep = tiltSpeed * Time.fixedDeltaTime; // Tilt change for this frame
        playerCamera = player.GetComponentInChildren<Camera>();
    }

    private (Vector3[], int[], Dictionary<int, List<int>>) DeduplicateVerticesAndBuildGraph(NavMeshTriangulation triangulation)
    {
        var vertices = triangulation.vertices;
        var indices = triangulation.indices;

        Dictionary<string, int> uniqueVertices = new Dictionary<string, int>();
        List<Vector3> deduplicatedVertices = new List<Vector3>();
        int[] updatedIndices = new int[indices.Length];

        float tolerance = 0.0001f;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            string key = GetVertexKey(v, tolerance);

            if (!uniqueVertices.ContainsKey(key))
            {
                uniqueVertices[key] = deduplicatedVertices.Count;
                deduplicatedVertices.Add(v);
            }
        }

        for (int i = 0; i < indices.Length; i++)
        {
            Vector3 v = vertices[indices[i]];
            string key = GetVertexKey(v, tolerance);
            updatedIndices[i] = uniqueVertices[key];
        }

        // Build the connectivity graph
        Dictionary<int, List<int>> triangleConnectivity = new Dictionary<int, List<int>>();
        for (int i = 0; i < indices.Length / 3; i++)
        {
            triangleConnectivity[i] = new List<int>();
        }

        for (int i = 0; i < indices.Length; i += 3)
        {
            for (int j = 0; j < 3; j++)
            {
                int vertexIndex = updatedIndices[i + j];
                for (int k = 0; k < indices.Length; k += 3)
                {
                    if (k != i && (updatedIndices[k] == vertexIndex || updatedIndices[k + 1] == vertexIndex || updatedIndices[k + 2] == vertexIndex))
                    {
                        triangleConnectivity[i / 3].Add(k / 3);
                    }
                }
            }
        }

        return (deduplicatedVertices.ToArray(), updatedIndices, triangleConnectivity);
    }
    private string GetVertexKey(Vector3 vertex, float tolerance)
    {
        int x = Mathf.RoundToInt(vertex.x / tolerance);
        int y = Mathf.RoundToInt(vertex.y / tolerance);
        int z = Mathf.RoundToInt(vertex.z / tolerance);
        return $"{x}_{y}_{z}";
    }
    private List<int> GetLargestConnectedComponent(Vector3[] vertices, int[] indices, Dictionary<int, List<int>> triangleConnectivity)
    {
        int triangleCount = indices.Length / 3;
        DFSGraph graph = new DFSGraph(triangleCount);

        // Create an adjacency list for the triangles based on shared vertices
        for (int i = 0; i < triangleCount; i++)
        {
            foreach (var neighbor in triangleConnectivity[i])
            {
                graph.AddEdge(i, neighbor);
            }
        }

        List<int> largestComponent = graph.GetLargestComponent();

        // Extract the indices of the largest component
        List<int> largestComponentIndices = new List<int>();
        foreach (int triangleIndex in largestComponent)
        {
            for (int j = 0; j < 3; j++)
            {
                largestComponentIndices.Add(triangleIndex * 3 + j);
            }
        }

        return largestComponentIndices;
    }

    public Vector3 GetClosestTriangle(Vector3 point, float threshold)
    {
        var liftedPoint = point + Vector3.up * 0.5f;
        var ray = new Ray(liftedPoint, Vector3.down);

        var collidingTriangles = new List<Triangle>();
        navMeshTriangles.GetColliding(collidingTriangles, ray, 1f);

        // player above the largest navigable area
        if (collidingTriangles.Any())
        {
            foreach (var triangle in collidingTriangles)
            {
                if (IsPointInTriangleXZ(point, triangle.a, triangle.b, triangle.c))
                {
                    return point;
                }
            }
        }

        // snap to the closest triangle horizontally
        var collidingTrianglesHorizontally = new List<Triangle>();
        var i = 0;
        while (true)
        {
            var bounds = new Bounds(point, new Vector3(threshold, 0.2f, threshold));
            navMeshTriangles.GetColliding(collidingTrianglesHorizontally, bounds);

            if (!collidingTrianglesHorizontally.Any())
            { 
                threshold *= 2f; // Increase the threshold
            }
            else
            {
                break;
            }

            if (i > 5)
            {
                Debug.Log("Point not found on the navigation mesh!");
                return point;
            }
            i++;
        }

        var closestTriangle = collidingTrianglesHorizontally.OrderBy(t => Vector3.Distance(point, t.a)).FirstOrDefault();
        Debug.Log("Point moved to the closest triangle horizontally at Vertex " + closestTriangle.a);

        return closestTriangle.a;
    }

    private void CreateTriangulation(NavMeshTriangulation navMeshTriangulation, List<int> largerstComponentIdicesList)
    {
        Vector3[] vertices = navMeshTriangulation.vertices;
        int[] indices = navMeshTriangulation.indices;

        var bounds = GetBounds(vertices);

        navMeshTriangles = new BoundsOctree<Triangle>(Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.y), bounds.size.z), bounds.center, 1f, 1.25f);

        for (int i = 0; i < largerstComponentIdicesList.Count; i += 3)
        {
            var vertex1 = vertices[indices[largerstComponentIdicesList[i]]];
            var vertex2 = vertices[indices[largerstComponentIdicesList[i + 1]]];
            var vertex3 = vertices[indices[largerstComponentIdicesList[i + 2]]];

            var slope = CalculateTiltAngle(vertex1, vertex2, vertex3);


            Triangle tri = new Triangle(
                vertex1,
                vertex2,
                vertex3,
                slope
            );

            var triangleBounds = GetBounds(new Vector3[] { vertex1, vertex2, vertex3 });
            navMeshTriangles.Add(tri, triangleBounds);            
        }
    }

    public void TiltCameraBasedOnPlayerDirection(Transform playerTransform)
    { 
        var liftedPlayerPosition = playerTransform.position + Vector3.up * 0.5f;
        var ray = new Ray(liftedPlayerPosition, Vector3.down);

        var collidingTriangles = new List<Triangle>();
        navMeshTriangles.GetColliding(collidingTriangles,ray,1f);

        Vector3 rotation = playerCamera.transform.eulerAngles;

        if (!collidingTriangles.Any())
        {
            if(rotation.x != 0f)
            {
                // Tilt back to horizontal (limit to 0 degrees)
                rotation.x = Mathf.Clamp(rotation.x - tiltStep, 0f, tiltAngle);
                playerCamera.transform.eulerAngles = rotation;
            }
            return;
        }

        var tiltedTriangles = new List<Triangle>();
        foreach(var collidingTriangle in collidingTriangles)
        {
            if(collidingTriangle.slope > surfaceSlopeThreshold)
            {
                tiltedTriangles.Add(collidingTriangle);
            }
        }

        var isWithinTriangle = false;
        foreach (var triangle in tiltedTriangles)
        {
            if(!IsPointInTriangleXZ(playerTransform.position, triangle.a, triangle.b, triangle.c))
            {
                continue;
            }

            var directionalSlope = GetSignedSlopeAngleRelativeToHorizontal(triangle.a, triangle.b, triangle.c, playerTransform.forward);
            //Debug.Log($"Directional slope angle: {directionalSlope} degrees");

            // Normalize X rotation (-180 to 180) to avoid 360° wrapping issues
            float xRotation = (rotation.x > 180) ? rotation.x - 360 : rotation.x;

            if (directionalSlope > 10f)
            {
                // Tilt downwards (limit to +20 degrees)
                xRotation = Mathf.Clamp(xRotation + tiltStep, 0f, tiltAngle);
            }
            else
            {
                // Tilt back to horizontal (limit to 0 degrees)
                xRotation = Mathf.Clamp(xRotation - tiltStep, 0f, tiltAngle);
            }

            // Apply new rotation
            playerCamera.transform.eulerAngles = new Vector3(xRotation, rotation.y, rotation.z);

            isWithinTriangle = true;
            break;
        }

        // Reset camera rotation if no triangles are found
        if(!isWithinTriangle)
        {
            // Tilt back to horizontal (limit to 0 degrees)
            rotation.x = Mathf.Clamp(rotation.x - tiltStep, 0f, tiltAngle);
            playerCamera.transform.eulerAngles = rotation;
        }
    }

    private float GetSignedSlopeAngleRelativeToHorizontal(Vector3 a, Vector3 b, Vector3 c, Vector3 direction)
    {
        // Step 1: Get the triangle normal
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

        // Step 2: Project the direction onto the triangle plane
        Vector3 projectedDirection = Vector3.ProjectOnPlane(direction, normal).normalized;

        // Step 3: Get the horizontal version of the projected direction (Y = 0)
        Vector3 horizontalDirection = new Vector3(projectedDirection.x, 0f, projectedDirection.z).normalized;

        // Step 4: Calculate angle between horizontal and triangle-projected direction
        float angle = Vector3.Angle(horizontalDirection, projectedDirection);

        // Step 5: Determine sign using dot product with up vector
        float sign = Mathf.Sign(Vector3.Dot(projectedDirection, Vector3.down));

        return angle * sign; // Positive = downhill, Negative = uphill
    }

    private float CalculateTiltAngle(Vector3 a, Vector3 b, Vector3 c)
    {
        // Compute the normal of the triangle
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

        // Compute the angle between the normal and the upward vector (Vector3.up)
        float angle = Vector3.Angle(normal, Vector3.up);

        return angle; // Returns tilt in degrees (0° = flat, 90° = vertical wall)
    }

    private static Bounds GetBounds(Vector3[] vertices)
    {
        Vector3 min = vertices[0];
        Vector3 max = vertices[0];

        foreach (Vector3 v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        return new Bounds(center, size);
    }

    struct Triangle
    {
        public Vector3 a, b, c;
        public float slope;

        public Triangle(Vector3 v1, Vector3 v2, Vector3 v3, float sl)
        {
            a = v1;
            b = v2;
            c = v3;
            slope = sl;
        }
    }

    private bool IsPointInTriangleXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // Convert to 2D (XZ)
        Vector2 p2 = new Vector2(p.x, p.z);
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        Vector2 c2 = new Vector2(c.x, c.z);

        // Compute vectors
        Vector2 v0 = c2 - a2;
        Vector2 v1 = b2 - a2;
        Vector2 v2 = p2 - a2;

        // Compute dot products
        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);

        // Compute barycentric coordinates
        float invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        // Check if the point is inside the triangle
        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }
}
