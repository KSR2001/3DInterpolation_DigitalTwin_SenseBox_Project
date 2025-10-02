using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using static UnityEngine.UIElements.UxmlAttributeDescription;

[System.Serializable]

//getting sensor readings from one sensebox
public class SensorReading
{
    public string boxId;
    public Vector3 position;
    public float co2;
    public float temperature;
    public GameObject targetObject;
}

public class SensorManager : MonoBehaviour
{
    [System.Serializable]
    //SenseBoxes configuration -> logic for adding more senseboxes later instead of touching code
    public class SenseBoxConfig
    {
        public string boxId;
        public string co2SensorId;
        public string tempSensorId;
        public GameObject targetObject;
        public Vector3 position;
    }
    //for switching modes in Game view
    public enum InterpolationMode { CO2, Temperature }
    public InterpolationMode currentMode = InterpolationMode.CO2;

    private List<GameObject> interpolationVoxels = new List<GameObject>();
    public List<SenseBoxConfig> senseBoxes = new List<SenseBoxConfig>(); //storing all the senseboxes added by the user
    public float fetchInterval = 10f; //Time between each API data fetch

    //created a new voxelmaterial in assets, unity generates pink 2d plane and below one gives the option to drag n drop the material, 
    public Material voxelMaterial; //to remove pink material///////////////

    //FOR 2D PLANE
    [Header("2D Heatmap Settings")]
    public float planeHeight = 92f;  //default 92 and public
    public enum RoomSlice { Front, Middle, Back }
    public RoomSlice planeLocation = RoomSlice.Middle;  //default position of the heatmap, but option in sensormanager overrides this
    //2d plane ends

    private List<SensorReading> sensorReadings = new List<SensorReading>(); //this list will hold multiple objects of the type sensorreading, we keep adding sensorReadings later in the code


    //main loop
    void Start()
    {
        StartCoroutine(FetchDataLoop());
    }

    IEnumerator FetchDataLoop()
    {
        while (true)
        {
            yield return StartCoroutine(FetchAllSensors());
            yield return new WaitForSeconds(fetchInterval);  //pausing here for 10 seconds before repeating
        }
    }

    //Note: we need to clear 'readings and interpolationVoxels' both to fetch in every 10 seconds

    IEnumerator FetchAllSensors()
    {
        sensorReadings.Clear();  //empties the previous sensor readings list, ensures fresh data is collected in FetchDataLoop on every while loop iteration

        foreach (var box in senseBoxes)
        {
            float co2 = 0;
            float temp = 0;

            string co2Url = $"https://api.opensensemap.org/boxes/{box.boxId}/sensors/{box.co2SensorId}";
            string tempUrl = $"https://api.opensensemap.org/boxes/{box.boxId}/sensors/{box.tempSensorId}";

            // Fetching the CO2
            UnityWebRequest co2Req = UnityWebRequest.Get(co2Url);
            yield return co2Req.SendWebRequest();
            if (co2Req.result == UnityWebRequest.Result.Success)
            {
                SensorData data = JsonUtility.FromJson<SensorData>(co2Req.downloadHandler.text);
                co2 = data.lastMeasurement.value;
            }
            else
            {
                Debug.LogError($"CO2 Error for box {box.boxId}: {co2Req.error}");
            }

            // Fetching the Temperature
            UnityWebRequest tempReq = UnityWebRequest.Get(tempUrl);
            yield return tempReq.SendWebRequest();
            if (tempReq.result == UnityWebRequest.Result.Success)
            {
                SensorData data = JsonUtility.FromJson<SensorData>(tempReq.downloadHandler.text);
                temp = data.lastMeasurement.value;
            }
            else
            {
                Debug.LogError($"Temp Error for box {box.boxId}: {tempReq.error}");
            }

            // Storing the reading in the list sensorReadings
            sensorReadings.Add(new SensorReading
            {
                boxId = box.boxId,
                position = box.position,
                co2 = co2,
                temperature = temp,
                targetObject = box.targetObject //iterating throughthe class SensorReading , box is the object of this class
            });  

            //Coloring each object based on CO2
            Color c = GetColorFromCO2(co2);
            box.targetObject.GetComponent<Renderer>().material.color = c;
        }

        //after readings generating the 2D plane, but even before generation, need to destroy the previous voxels
        StartCoroutine(Generate2DPlaneHeatmap());
    }



    //Interpolation logic
    IEnumerator Generate2DPlaneHeatmap()
    {
        
        //in order to not get the duplicates, destroying previous ones
        foreach (var voxel in interpolationVoxels)
        {
            Destroy(voxel);
        }
        interpolationVoxels.Clear(); //clearing the reference list

        //----logic to selectively visualize only one horizontal slice (front, middle, or back)

        //finding the plane area by changing the room's depth
        Vector3 roomOrigin = new Vector3(35, planeHeight, -35);
        Vector3 roomSize = new Vector3(6, 0, 12);
        float tileSize = 0.5f; //grid is formed tile by tile, creates clean square grid of 1x1 units, 0.5x0.5 units

        // Adjusting X-Z origin based on room slice
        float sliceDepth = roomSize.z / 3f; //i.e 12/3 = 4 

        switch (planeLocation) //plane's horizontal location
        {
            case RoomSlice.Front:
                roomOrigin.z += 0; //origin remains the same i.e -35
                roomSize.z = sliceDepth; 
                break;
            case RoomSlice.Middle:
                roomOrigin.z += sliceDepth; //origin shifts forward by 1 slice i.e -35+4 = -31
                roomSize.z = sliceDepth;
                break;
            case RoomSlice.Back:
                roomOrigin.z += 2 * sliceDepth; //origin shifts by 2 slice i.e -31+4 = -27 
                roomSize.z = sliceDepth;
                break;
        }

       
        for (float x = 0; x < roomSize.x; x += tileSize)  //moving along x axis of the room - left to right
        {
            for (float z = 0; z < roomSize.z; z += tileSize)  //moving along z axis of the room - front to back
            {
                Vector3 samplePoint = new Vector3(roomOrigin.x + x, planeHeight, roomOrigin.z + z); //this point defines bottom left corner of the tile area

                float interpolatedValue;

                if (currentMode == InterpolationMode.CO2)
                    interpolatedValue = InterpolationHelper.InverseDistanceWeighting(samplePoint, sensorReadings, true); //sensorreadings has latest values
                else
                    interpolatedValue = InterpolationHelper.InverseDistanceWeighting(samplePoint, sensorReadings, false);


                Debug.Log($"Tile at {samplePoint} - Value: {interpolatedValue}");

                //coloring the voxel as per the current mode
                Color color = (currentMode == InterpolationMode.CO2) ?
                              GetColorFromCO2(interpolatedValue) :
                              GetColorFromTemperature(interpolatedValue);

                color.a = 0.3f; //transparency for overlap

                //creating the tile object
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad); //creating the tile object i.e Quad, representation of one grid cell
                Destroy(tile.GetComponent<Collider>());

                tile.transform.position = samplePoint + new Vector3(0.5f, 0, 0.5f);  // adding offset to position the tile 
                tile.transform.rotation = Quaternion.Euler(90, 0, 0); // face upward -rotated 90 degrees to lie flat flat on the xz plane, by default quad faces z direction
                tile.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); //to scale the tiles larger or smaller

                //this lets the script use the assigned material at runtime, not allowing it to be pink, without this all tiles will get the same colour
                Material materialInstance = new Material(voxelMaterial);
                materialInstance.color = color;
                tile.GetComponent<Renderer>().material = materialInstance;

                interpolationVoxels.Add(tile);
                yield return new WaitForSeconds(0.1f); //progressively loading, 0.1 seconds delay

            }
        }


    }

    Color GetColorFromCO2(float co2)
    {
        if (co2 < 1000f) return Color.green;
        else if (co2 < 1500f) return Color.yellow;
        else return Color.red;
    }

    Color GetColorFromTemperature(float temp)
    {
        if (temp < 18f) return Color.blue;
        else if (temp < 25f) return Color.green;
        else if (temp < 30f) return Color.yellow;
        else return Color.red;
    }

    public void OnInterpolationTypeChanged(int index)
    {
        //Debug.Log("Interpolation changed: " + index);
        currentMode = (InterpolationMode)index;
        StartCoroutine(Generate2DPlaneHeatmap());
    }

    public void OnHeightChanged(float newHeight)
    {
        //Debug.Log("Height changed: " + newHeight);
        planeHeight = newHeight;
        StartCoroutine(Generate2DPlaneHeatmap());
    }

    public void OnPlanePositionChanged(int index)
    {
        //Debug.Log("Plane position changed: " + index);
        planeLocation = (RoomSlice)index;
        StartCoroutine(Generate2DPlaneHeatmap());
    }



}

//IDW gives higher weight to closer sensors
public static class InterpolationHelper
{
    public static float InverseDistanceWeighting(Vector3 targetPosition, List<SensorReading> sources, bool isCO2)
    {
        float weightedSum = 0f;
        float totalWeight = 0f;

        foreach (var sensor in sources) //looping through all available sensors and using each sensor's reading to influence the target tile's value
        {
            float distance = Vector3.Distance(targetPosition, sensor.position); //straight line distance between tile being drawn and sensor's position
            float weight = 1f / Mathf.Max(distance * distance, 0.01f); //weight assigned to the sensor's distance, IDW = 1/ distance sq,
                                                                       //mathf and 0.01f prevents division by 0, just in case tile is exactly at sensor's position

            float value = isCO2 ? sensor.co2 : sensor.temperature;

            weightedSum += value * weight;  //sensor's value x sensor's weight , closer sensor should give high influence
            totalWeight += weight;
        }

        return weightedSum / totalWeight; //gives the average of all the sensors
    }

}
