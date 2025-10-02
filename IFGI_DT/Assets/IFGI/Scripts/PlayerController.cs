using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    private NavMeshFunctions navMeshFunctions;
    private LiftsManager liftsManager;
    // Start is called before the first frame update
    void Start()
    {
        var navMeshTriangulation = NavMesh.CalculateTriangulation();
        // initialize navMesh triangles for tilting camera when going downstairs 
        navMeshFunctions = new NavMeshFunctions(navMeshTriangulation, transform);
        liftsManager = FindObjectOfType<LiftsManager>();
    }

    // Update is called once per frame
    void Update()
    {
        // lift interaction
        if (Input.GetMouseButtonDown(0)) // Detect left mouse button click
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // check distance to the lift. If the distance is <= 3 meters, then the lift button is clicked
            if (Physics.Raycast(ray, out hit, 3f))
            {
                //// Check if the hit object has a specific tag or component
                //Debug.Log("Clicked on: " + hit.collider.gameObject.name);

                var hitObject = hit.collider.gameObject;
                if (hitObject.CompareTag("ExternalButton"))
                {
                    liftsManager.ExternalButtonClicked(hitObject);
                }
                else if (hitObject.CompareTag("InternalButton"))
                {
                    liftsManager.InternalButtonClicked(hitObject);
                }
            }
        }
    }

    void FixedUpdate()
    {
        // check if player is going beyond navmesh
        //Debug.DrawRay(transform.position, Vector3.up, Color.red);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 1f, NavMesh.AllAreas))
        {
            Vector3 closestPoint = hit.position;
            //Debug.DrawRay(closestPoint, Vector3.up, Color.blue);
            // check if player is not above the navmesh
            if (Mathf.Abs(transform.position.x - closestPoint.x) > 0.01f || Mathf.Abs(transform.position.z - closestPoint.z) > 0.01f)
            {
                //Debug.Log("player returned above the navmesh");
                transform.position = new Vector3(closestPoint.x, transform.position.y, closestPoint.z);
            }
        }
        //Debug.DrawRay(transform.position, Vector3.up, Color.yellow, 0.1f);

        // check if camera should be tilted down when going downstairs
        navMeshFunctions.TiltCameraBasedOnPlayerDirection(transform);        
    }
}
