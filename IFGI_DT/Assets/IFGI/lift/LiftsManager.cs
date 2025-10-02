using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LiftsManager : MonoBehaviour
{
    public LiftController[] lifts;
    public List<DirectionButton> externalButtons = new List<DirectionButton>();

    void Start()
    {
        foreach (var lift in lifts)
        {
            lift.liftsManager = this;
        }
    }

    public void ExternalButtonClicked(GameObject button)
    {
        var directionButton = button.GetComponent<DirectionButton>();
        externalButtons.Add(directionButton);
        var openLift = lifts.FirstOrDefault(lift => lift.currentFloor == directionButton.floorNumber && lift.GetDoorStatus() != DoorStatus.Closed &&
        (lift.direction == directionButton.direction || lift.direction == Direction.Neutral));

        if (openLift != null)
        {
            openLift.OpenDoors();
            Debug.Log("openLift: " + openLift.liftIndex);
            return;
        }
        directionButton.isClicked = true;
        TurnOnButton(directionButton);
        UpdateLiftsQueueExternally(directionButton);
    }

    public void InternalButtonClicked(GameObject button)
    {
        var directionButton = button.GetComponent<DirectionButton>();
        var lift = lifts[directionButton.liftIndex];
        if (directionButton.floorNumber == -100 && lift.isMoving == false)
        {
            lift.OpenDoors();
            return;
        }

        if (directionButton.isClicked)
            return;
        
        if (lift.currentFloor == directionButton.floorNumber)
            return;
        directionButton.isClicked = true;
        TurnOnButton(directionButton);
        UpdateLiftsQueueInternally(directionButton, lift);
    }

    public void UpdateLiftsQueueExternally(DirectionButton directionButton)
    {

        var closestLift = lifts.Where(lift => lift.direction == Direction.Neutral && lift.targetFloor == lift.noTargetFloor)
            .OrderBy(lift => Mathf.Abs(lift.currentFloor - directionButton.floorNumber)).FirstOrDefault();

        if (closestLift != null)
        {
            Debug.Log("closestLift: " + closestLift.liftIndex);
            if (closestLift.targetFloor == closestLift.noTargetFloor && closestLift.direction == Direction.Neutral) // has no target floor
            {
                directionButton.liftIndex = closestLift.liftIndex;
                closestLift.direction = directionButton.direction;

                if (directionButton.floorNumber != closestLift.currentFloor)
                {
                    closestLift.directionalButtonsQueue.Add(directionButton);
                    closestLift.targetFloor = directionButton.floorNumber;
                    closestLift.isMoving = true;
                    return;
                }

                closestLift.OpenDoors();
                return;
            }
        }
        else
        { 
            // to change the target floor if the button selected is in between the current and the target floor, but in the same direction
            var closestMovingLift = lifts
                .Where(lift =>
                    (lift.direction == Direction.Up && lift.currentFloor < directionButton.floorNumber && lift.targetFloor > directionButton.floorNumber) ||
                    (lift.direction == Direction.Down && lift.currentFloor > directionButton.floorNumber && lift.targetFloor < directionButton.floorNumber))
                .OrderBy(lift => Mathf.Abs(lift.currentFloor - directionButton.floorNumber))
                .FirstOrDefault();
            
            if (closestMovingLift != null)
            {
                closestMovingLift.directionalButtonsQueue.Add(directionButton);
                closestMovingLift.targetFloor = directionButton.floorNumber;
                directionButton.liftIndex = closestMovingLift.liftIndex;
            }
        }
    }

    public void UpdateLiftsQueueInternally(DirectionButton directionButton, LiftController lift)
    {
        if (lift.targetFloor == lift.noTargetFloor) 
        {
            if (directionButton.floorNumber != lift.currentFloor)
            {
                lift.targetFloor = directionButton.floorNumber;
                lift.internalButtons.Add(directionButton);
                lift.directionalButtonsQueue.Add(directionButton);
                return;
            }
            // lift is already at the target floor
            directionButton.isClicked = false;
            return;
        }

        ChangeTargetFloor(lift, directionButton);

        lift.internalButtons.Add(directionButton);
    }

    private void ChangeTargetFloor(LiftController lift, DirectionButton directionButton)
    {
        if (lift.direction == Direction.Up)
        {
            if (directionButton.floorNumber > lift.currentFloor)
            {
                lift.directionalButtonsQueue.Add(directionButton);
                if (lift.targetFloor > directionButton.floorNumber)
                {
                    lift.targetFloor = directionButton.floorNumber;
                }
                directionButton.liftIndex = lift.liftIndex;
            }
        }
        else
        {
            if (directionButton.floorNumber < lift.currentFloor)
            {
                lift.directionalButtonsQueue.Add(directionButton);
                if (lift.targetFloor < directionButton.floorNumber)
                {
                    lift.targetFloor = directionButton.floorNumber;
                }
                directionButton.liftIndex = lift.liftIndex;
            }
        }        
    }

    private void TurnOnButton(DirectionButton directionButton)
    {
        for (var i = 0; i < directionButton.transform.parent.childCount; i++)
        {
            var gameObject = directionButton.transform.parent.GetChild(i).gameObject;
            if (gameObject != directionButton.gameObject)
            {
                if (gameObject.CompareTag("PressedButton"))
                {
                    gameObject.SetActive(true);
                }
            }
        }
    }
    
}
