using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.Networking;



[System.Serializable]


public class SenseBoxData

{

    public SensorData[] sensors;

}

public class DataFetcher : MonoBehaviour

{

    private string url = "https://api.opensensemap.org/boxes/682c7e1ceecb860007886939/sensors/682c7e1ceecb86000788693a"; //sensebox - 1

    public GameObject targetObject; // Assign the target object in the Unity Editor 

    public float fetchInterval = 10f; // Time interval between fetches in seconds 


    void Start()

    {

        StartCoroutine(FetchDataLoop());

    }



    IEnumerator FetchDataLoop()

    {

        while (true)

        {

            yield return StartCoroutine(FetchData());

            yield return new WaitForSeconds(fetchInterval);

        }

    }



    IEnumerator FetchData()

    {

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))

        {

            // Request and wait for the desired page. 

            yield return webRequest.SendWebRequest();



            if (webRequest.isNetworkError || webRequest.isHttpError)

            {

                Debug.LogError($"Error: {webRequest.error}, Response Code: {webRequest.responseCode}");

            }

            else

            {

                // Parse the JSON response 

                string jsonResponse = webRequest.downloadHandler.text;

                Debug.Log(jsonResponse); // Log the JSON response to debug 



                try

                {

                    SensorData sensor = JsonUtility.FromJson<SensorData>(jsonResponse);



                    // Use the fetched and parsed data 

                    Debug.Log($"ID: {sensor._id}, Title: {sensor.title}, Value: {sensor.lastMeasurement.value} {sensor.unit}");



                    // Change color based on the value 

                    ChangeColor(sensor.lastMeasurement.value);

                }

                catch (System.Exception ex)

                {

                    Debug.LogError($"Error parsing JSON: {ex.Message}");

                }

            }

        }

    }

    void ChangeColor(float value)

    {

        Renderer renderer = GetComponent<Renderer>();

        if (renderer != null)

        {

            if (value < 1000)

            {

                renderer.material.color = Color.green;

            }

            else if (value <= 1500)

            {

                renderer.material.color = Color.yellow;

            }

            else

            {

                renderer.material.color = Color.red;

            }

        }

        else

        {

            Debug.LogError("Renderer component not found on the target object.");

        }

    }

}