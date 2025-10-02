using System;

[Serializable]
public class LatestMeasurement
{
    public string createdAt;
    public float value;
}

[Serializable]
public class SensorData
{
    public string _id;
    public string icon;
    public string title;
    public string unit;
    public string sensorType;
    public LatestMeasurement lastMeasurement;
}
