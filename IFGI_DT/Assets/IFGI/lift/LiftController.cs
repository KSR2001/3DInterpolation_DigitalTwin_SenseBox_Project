using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class LiftController : MonoBehaviour
{
    [Header("Lift Settings")]
    public int liftIndex;                        // Index of the lift
    public float speed = 2f;                     // Speed of the lift movement
    public float[] floorsHeights;                 // Array of floor positions
    public int noTargetFloor;                     // Index of floor when no target floor is set
    public int[] floorsNumbers;                  // Array of floor numbers

    public float doorSpeed = 1f;                // Speed of door movement
    public float door1OpenDistance = 0.9f;       // How far the doors open
    public float door2OpenDistance = 0.45f;      // How far the doors open
    public float doorCloseDelay = 7f;           // Time to wait before closing the doors
    public TextMeshPro[] floorIndicators;       // Current floor indicators for each floor
    public LiftsManager liftsManager;          // Reference to the lifts manager
    private int indexOffset;                // Offset to match the floor index with the floor number

    [Header("Lift State")]
    public int currentFloor = 0;                        // Current floor index
    public int targetFloor = -2;                        // Target floor index

    public Direction direction = Direction.Neutral;     // Direction of movement of the lift, which is not necessarly the same direction of the requested floors
    public bool isMoving = false;                       // Whether the lift is moving
    private bool doorsAreOpening = false;               // Whether doors are opening
    private bool doorsAreClosing = false;               // Whether doors are closing
    private bool doorsAreClosed = true;                 // Whether doors are closed
    private float lastInteractionTime = 0f;             // Tracks the last time someone entered or exited
    private bool isGoingDown = false;                   // Whether the lift is going down
    private Coroutine updateIndicatorCoroutine;         // Coroutine to update the floor indicators
    public List<DirectionButton> internalButtons;       // List of floor buttons pressed
    private List<GameObject> players = new List<GameObject>();         // List of players in the lift
    private List<CharacterController> playerCharacterController = new List<CharacterController>();
    private Vector3 playerPreviousPosition;            // Players' previous position
    private Bounds liftBounds;                          // Bounds of the lift
    public List<DirectionButton> directionalButtonsQueue = new List<DirectionButton>();
    private List<Collider> liftShaftCollidees;

    // lift doors
    public Transform leftDoor;                  // Left door transform
    public Transform rightDoor;                 // Right door transform
    private Vector3 leftDoorClosedPosition;
    private Vector3 rightDoorClosedPosition;
    private Vector3 leftDoorOpenPosition;
    private Vector3 rightDoorOpenPosition;
    // external floor doors
    public List<GameObject> externalDoors = new List<GameObject>(); // List of external doors
    private List<Transform> externalLeftDoors = new List<Transform>();
    private List<Transform> externalRightDoors = new List<Transform>();
    private List<Vector3> externalLeftDoorsClosedPosition = new List<Vector3>();
    private List<Vector3> externalRightDoorsClosedPosition = new List<Vector3>();
    private Transform externalLeftDoor;
    private Transform externalRightDoor;
    private Vector3 externalLeftDoorClosedPosition;
    private Vector3 externalRightDoorClosedPosition;
    private Vector3 externalLeftDoorOpenPosition;
    private Vector3 externalRightDoorOpenPosition;

    void Start()
    {
        foreach (var door in externalDoors)
        {
            var leftDoor = door.transform.GetChild(0).transform;
            var rightDoor = door.transform.GetChild(1).transform;
            externalLeftDoors.Add(leftDoor);
            externalRightDoors.Add(rightDoor);
            externalLeftDoorsClosedPosition.Add(leftDoor.position);
            externalRightDoorsClosedPosition.Add(rightDoor.position);
        }
        leftDoorClosedPosition = leftDoor.position;
        rightDoorClosedPosition = rightDoor.position;
        leftDoorOpenPosition = leftDoor.position + Vector3.left * door1OpenDistance;
        rightDoorOpenPosition = rightDoor.position + Vector3.left * door2OpenDistance;

        // Set invalid and the initial target floor
        noTargetFloor = Mathf.Min(floorsNumbers) - 1;
        targetFloor = noTargetFloor;
        liftBounds = GetComponent<BoxCollider>().bounds;

        indexOffset = Mathf.Min(floorsNumbers);
        UpdateShaftBoxCollider();
    }

    void FixedUpdate()
    {
        if (isMoving)
        {
            MoveLift();
            return;
        }

        if (doorsAreOpening)
        {
            OpenDoorsUpdate();
            return;
        }

        if (doorsAreClosing)
        {
            CloseDoorsUpdate();
            return;
        }

        if (doorsAreClosed)
        {
            AtFloorAction();
            return;
        }

        // Close the doors if no interaction for the set delay
        if (Time.time - lastInteractionTime > doorCloseDelay)
        {
            CloseDoors();
        }
    }

    // Move the lift towards the target floor
    private void MoveLift()
    {
        // move the lift towards the target floor
        float targetHeight = floorsHeights[targetFloor - indexOffset];
        Vector3 targetPosition = new Vector3(transform.position.x, targetHeight, transform.position.z);
        var nextLiftPosition = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        // update player vertical position
        foreach (var playerController in playerCharacterController)
        {
            Vector3 movement = new Vector3(0, nextLiftPosition.y - playerController.gameObject.transform.position.y, 0);
            playerController.Move(movement);
        }

        transform.position = nextLiftPosition;

        // update the current floor if the lift has passed it
        var nextFloor = targetFloor > currentFloor ? currentFloor + 1 : currentFloor - 1;
        var currentFloorHeight = floorsHeights[currentFloor - indexOffset];
        var nextFloorHeight = floorsHeights[nextFloor - indexOffset];
        if ((nextFloor > currentFloor && transform.position.y > nextFloorHeight) ||
                (nextFloor < currentFloor && transform.position.y < nextFloorHeight))
        {
            currentFloor = nextFloor;
            UpdateAllFloorIndicators();
        }

        if ((transform.position - targetPosition).sqrMagnitude < 0.0001f)
        {
            isMoving = false;
            currentFloor = targetFloor;
            UpdateAllFloorIndicators();
            OpenDoors();
            UpdateFloorIndicators();
        }
    }

    private void UpdateFloorIndicators()
    {
        // Stop any previous coroutine to avoid overlaps
        if (updateIndicatorCoroutine != null)
        {
            StopCoroutine(updateIndicatorCoroutine);
        }

        // Start the coroutine to show the arrow and then the floor
        updateIndicatorCoroutine = StartCoroutine(ShowArrowThenFloor());
    }

    private IEnumerator ShowArrowThenFloor()
    {
        // Show the arrow (e.g., ▼ for down, ▲ for up)
        foreach (var indicator in floorIndicators)
        {
            if (direction == Direction.Down)
                indicator.text = "\u25BC"; // Arrow pointing down (▼)
            else if (direction == Direction.Up)
                indicator.text = "\u25B2"; // Arrow pointing up (▲)
        }

        // Wait for 2 seconds
        yield return new WaitForSeconds(2f);

        UpdateAllFloorIndicators();
    }

    private void UpdateAllFloorIndicators()
    {
        // Update the indicators to show the current floor
        foreach (var indicator in floorIndicators)
        {
            indicator.text = currentFloor.ToString();
        }
    }

    // Called when the lift reaches the target floor
    private void AtFloorAction()
    {
        // check the directional queue first
        if (directionalButtonsQueue.Any())
        {
            DirectionButton closestFloorInQueue;
            if (direction == Direction.Up)
            {
                closestFloorInQueue = directionalButtonsQueue.OrderBy(obj => obj.floorNumber).FirstOrDefault();
            }
            else
            {
                closestFloorInQueue = directionalButtonsQueue.OrderByDescending(obj => obj.floorNumber).FirstOrDefault();
            }
            targetFloor = closestFloorInQueue.floorNumber;
            isMoving = true;
        }
        else
        {
            // change the direction
            direction = direction == Direction.Up ? Direction.Down : Direction.Up;
            // update the directional queue
            if (internalButtons.Any())
            {
                // Find the next target floor based on the direction and the floor that is the closest to the current floor
                foreach (var button in internalButtons)
                {
                    if (direction == Direction.Up && button.floorNumber > currentFloor)
                    {
                        directionalButtonsQueue.Add(button);
                        if (button.floorNumber < targetFloor)
                        {
                            targetFloor = button.floorNumber;
                        }
                    }
                    else if (direction == Direction.Down && button.floorNumber < currentFloor)
                    {
                        directionalButtonsQueue.Add(button);
                        if (button.floorNumber > targetFloor)
                        {
                            targetFloor = button.floorNumber;
                        }
                    }
                }

                // Find the next target floor based on the direction and the floor that is the closest to the current floor
                foreach (var button in liftsManager.externalButtons)
                {
                    if (button.liftIndex != liftIndex)
                    {
                        continue;
                    }

                    if (direction == button.direction && button.floorNumber > currentFloor)
                    {
                        button.liftIndex = liftIndex;
                        directionalButtonsQueue.Add(button);
                        if (button.floorNumber < targetFloor)
                        {
                            targetFloor = button.floorNumber;
                        }
                    }
                    else if (direction == button.direction && button.floorNumber < currentFloor)
                    {
                        button.liftIndex = liftIndex;
                        directionalButtonsQueue.Add(button);
                        if (button.floorNumber > targetFloor)
                        {
                            targetFloor = button.floorNumber;

                        }
                    }
                }
            }
            else if (liftsManager.externalButtons.Any())
            {
                // Find the next target floor based on the direction and the floor that is the closest to the current floor
                foreach (var button in liftsManager.externalButtons)
                {
                    if (button.liftIndex != liftIndex)
                    {
                        continue;
                    }

                    if (direction == button.direction && button.floorNumber > currentFloor)
                    {
                        button.liftIndex = liftIndex;
                        directionalButtonsQueue.Add(button);
                        if (button.floorNumber < targetFloor)
                        {
                            targetFloor = button.floorNumber;
                        }
                    }
                    else if (direction == button.direction && button.floorNumber < currentFloor)
                    {
                        button.liftIndex = liftIndex;
                        directionalButtonsQueue.Add(button);
                        if (button.floorNumber > targetFloor)
                        {
                            targetFloor = button.floorNumber;

                        }
                    }
                }

                if (targetFloor == noTargetFloor)
                {
                    direction = Direction.Neutral;
                }
            }
            else
            {
                targetFloor = noTargetFloor;
                direction = Direction.Neutral;
            }
        }
    }

    // Open the lift doors
    public void OpenDoors()
    {
        // remove the directional button from the queue
        var queueButton = directionalButtonsQueue.FirstOrDefault(x => x.floorNumber == currentFloor);
        if (queueButton != null)
        {
            directionalButtonsQueue.Remove(queueButton);
        }

        if (!directionalButtonsQueue.Any() && !liftsManager.externalButtons.Any())
        {
            direction = Direction.Neutral;
        }

        // turn off external button
        var externalFloorButton = liftsManager.externalButtons.FirstOrDefault(x => x.floorNumber == currentFloor && x.direction == direction);
        if (externalFloorButton != null)
        {
            ChangeButtonView(externalFloorButton, false);
            externalFloorButton.isClicked = false;
            liftsManager.externalButtons.Remove(externalFloorButton);
        }

        // turn off internal button
        var internalFloorButton = internalButtons.FirstOrDefault(x => x.floorNumber == currentFloor);
        if (internalFloorButton != null)
        {
            ChangeButtonView(internalFloorButton, false);
            internalFloorButton.isClicked = false;
            internalButtons.Remove(internalFloorButton);
        }

        lastInteractionTime = Time.time; // Reset interaction timer
        doorsAreOpening = true;

        var index = currentFloor - indexOffset;
        externalLeftDoor = externalLeftDoors[index];
        externalRightDoor = externalRightDoors[index];
        externalLeftDoorClosedPosition = externalLeftDoorsClosedPosition[index];
        externalRightDoorClosedPosition = externalRightDoorsClosedPosition[index];
        externalLeftDoorOpenPosition = externalLeftDoorClosedPosition + Vector3.left * door1OpenDistance;
        externalRightDoorOpenPosition = externalRightDoorClosedPosition + Vector3.left * door2OpenDistance;

        externalLeftDoor.GetComponent<NavMeshObstacle>().carving = false;
        externalRightDoor.GetComponent<NavMeshObstacle>().carving = false;
        leftDoor.GetComponent<NavMeshObstacle>().carving = false;
        rightDoor.GetComponent<NavMeshObstacle>().carving = false;

        leftDoorOpenPosition = new Vector3(leftDoorOpenPosition.x, leftDoor.position.y, leftDoorOpenPosition.z);
        rightDoorOpenPosition = new Vector3(rightDoorOpenPosition.x, rightDoor.position.y, rightDoorOpenPosition.z);
    }

    private void OpenDoorsUpdate()
    {
        var step = doorSpeed * Time.deltaTime;

        leftDoor.position = Vector3.Lerp(leftDoor.position, leftDoorOpenPosition, step);
        rightDoor.position = Vector3.Lerp(rightDoor.position, rightDoorOpenPosition, step);

        externalLeftDoor.position = Vector3.Lerp(externalLeftDoor.position, externalLeftDoorOpenPosition, step);
        externalRightDoor.position = Vector3.Lerp(externalRightDoor.position, externalRightDoorOpenPosition, step);

        if ((leftDoor.position - leftDoorOpenPosition).sqrMagnitude < 0.0001f)
        {
            externalLeftDoor.position = externalLeftDoorOpenPosition;
            externalRightDoor.position = externalRightDoorOpenPosition;
            leftDoor.position = leftDoorOpenPosition;
            rightDoor.position = rightDoorOpenPosition;
            doorsAreOpening = false;
            doorsAreClosed = false;
        }
    }

    // Close the lift doors
    private void CloseDoors()
    {
        doorsAreClosing = true;

        leftDoorClosedPosition = new Vector3(leftDoorClosedPosition.x, leftDoor.position.y, leftDoorClosedPosition.z);
        rightDoorClosedPosition = new Vector3(rightDoorClosedPosition.x, rightDoor.position.y, rightDoorClosedPosition.z);
    }

    private void CloseDoorsUpdate()
    {
        //open door if a player is in the way
        var openPart = leftDoorClosedPosition - leftDoor.position;
        var halfDoorWidth = door2OpenDistance / 2f;
        RaycastHit hit;
        // Does the ray hits any player
        if (Physics.Raycast(leftDoor.position + halfDoorWidth * openPart.normalized, openPart.normalized, out hit, openPart.magnitude - halfDoorWidth))
        {
            lastInteractionTime = Time.time; // Reset interaction timer
            OpenDoors();
            return;
        }

        var step = doorSpeed * Time.deltaTime;

        leftDoor.position = Vector3.Lerp(leftDoor.position, leftDoorClosedPosition, step);
        rightDoor.position = Vector3.Lerp(rightDoor.position, rightDoorClosedPosition, step);

        externalLeftDoor.position = Vector3.Lerp(externalLeftDoor.position, externalLeftDoorClosedPosition, step);
        externalRightDoor.position = Vector3.Lerp(externalRightDoor.position, externalRightDoorClosedPosition, step);

        if ((leftDoor.position - leftDoorClosedPosition).sqrMagnitude < 0.0001f)
        {
            externalLeftDoor.position = externalLeftDoorClosedPosition;
            externalRightDoor.position = externalRightDoorClosedPosition;
            leftDoor.position = leftDoorClosedPosition;
            rightDoor.position = rightDoorClosedPosition;
            doorsAreClosing = false;
            doorsAreClosed = true;

            externalLeftDoor.GetComponent<NavMeshObstacle>().carving = true;
            externalRightDoor.GetComponent<NavMeshObstacle>().carving = true;
            leftDoor.GetComponent<NavMeshObstacle>().carving = true;
            rightDoor.GetComponent<NavMeshObstacle>().carving = true;
        }
    }

    // Trigger detection for entering/exiting
    private void OnTriggerEnter(Collider other)
    {
        lastInteractionTime = Time.time; // Reset interaction timer

        if (players.Contains(other.gameObject))
        {
            return;
        }

        foreach (Collider col in liftShaftCollidees)
        {
            if (col is MeshCollider) // Ignore ceilings & floors
            {
                Physics.IgnoreCollision(other, col, true);
            }
        }

        players.Add(other.gameObject);
        playerCharacterController.Add(other.gameObject.GetComponent<CharacterController>());
    }

    private void OnTriggerExit(Collider other)
    {
        lastInteractionTime = Time.time; // Reset interaction timer

        if (players.Contains(other.gameObject))
        {
            players.Remove(other.gameObject);
            playerCharacterController.Remove(other.gameObject.GetComponent<CharacterController>());
        }

        foreach (Collider col in liftShaftCollidees)
        {
            if (col is MeshCollider)
            {
                Physics.IgnoreCollision(other, col, false);
            }
        }
    }

    public DoorStatus GetDoorStatus()
    {
        if (doorsAreOpening)
            return DoorStatus.Opening;
        if (doorsAreClosing)
            return DoorStatus.Closing;
        if (doorsAreClosed)
            return DoorStatus.Closed;
        return DoorStatus.Open;
    }

    private void ChangeButtonView(DirectionButton directionButton, bool isButtonOn)
    {
        if (isButtonOn)
        {
            for (var i = 0; i < directionButton.transform.parent.childCount; i++)
            {
                var gameObject = directionButton.transform.parent.GetChild(i).gameObject;
                if (gameObject.CompareTag("PressedButton"))
                {
                    gameObject.SetActive(true);
                }
            }
        }
        else
        {
            for (var i = 0; i < directionButton.transform.parent.childCount; i++)
            {
                var gameObject = directionButton.transform.parent.GetChild(i).gameObject;
                if (gameObject.CompareTag("PressedButton"))
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }

    private void UpdateShaftBoxCollider()
    {
        // Initialize a bounds object to accumulate all child bounds
        Bounds combinedBounds = new Bounds(this.gameObject.transform.GetChild(0).transform.position, Vector3.zero);

        // Iterate through all renderers in the child objects to combine their bounds
        foreach (Renderer r in this.gameObject.transform.GetComponentsInChildren<Renderer>())
        {
            combinedBounds.Encapsulate(r.bounds); // Combine bounds
        }

        var minHeight = floorsHeights[0];
        var maxHeight = floorsHeights[floorsHeights.Length - 1];
        // Calculate the box scale based on the accumulated bounds
        Vector3 boxScale = new Vector3(combinedBounds.size.x, maxHeight - minHeight, combinedBounds.size.z);
        // Position the box to be at the center of the accumulated bounds
        var boxCenter = new Vector3(combinedBounds.center.x, (maxHeight + minHeight) / 2f, combinedBounds.center.z);

        //var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //cube.transform.position = boxCenter;
        //cube.transform.localScale = boxScale;

        Collider[] nearbyColliders = Physics.OverlapBox(boxCenter, boxScale / 2);

        // Get all colliders that are part of the liftParts
        HashSet<Collider> liftPartColliders = new HashSet<Collider>(this.gameObject.transform.GetComponentsInChildren<Collider>());

        // Filter out liftParts colliders
        liftShaftCollidees = new List<Collider>();

        foreach (Collider col in nearbyColliders)
        {
            if (!liftPartColliders.Contains(col)) // Exclude lift parts
            {
                liftShaftCollidees.Add(col);
            }
        }
    }
}

public enum DoorStatus
{
    Open,
    Opening,
    Closing,
    Closed
}

