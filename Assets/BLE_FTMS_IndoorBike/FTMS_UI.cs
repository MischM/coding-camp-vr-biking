using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class FTMS_UI : MonoBehaviour
{
    // Start is called before the first frame update
    private bool connected = false;
    public FtmsIndoorBike connector;
    public Text info;
    public Text resistanceShow;

    public string deviceName = "TUO";
    public string serviceID = "{00001826-0000-1000-8000-00805f9b34fb}";
    public string readCharacteristic = "{00002ad2-0000-1000-8000-00805f9b34fb}";
    public string writeCharacteristic= "{00002ad9-0000-1000-8000-00805f9b34fb}";

    /*
    public string device_name = "APXPRO 46080";
    public string service_id = "{00001826-0000-1000-8000-00805f9b34fb}";
    public string read_characteristic = "{00002ad2-0000-1000-8000-00805f9b34fb}";
    public string write_characteristic= "{00002ad9-0000-1000-8000-00805f9b34fb}";
    */
    void OnEnable()
    {
        connector = FtmsIndoorBike.instance;

    }

    void Update()
    {
        if (connected)
        {
           // connector.Update();
            info.text = GenerateOutputText();
        }
    }

    private string GenerateOutputText()
    {
        string output = String.Empty;
        
        if (connector.hasSpeed)
        {
            output += "Speed: " + connector.speed + "\n";
        
        }
        if (connector.hasAverageSpeed)
        {
            output += "Average Speed: " + connector.averageSpeed + "\n";

        }
        if (connector.hasRpm)
        {
            output += "RPM: (rev/min): " + connector.rpm + "\n";
        }
        if (connector.hasAverageRpm)
        {
            output += "Average RPM: " + connector.averageRpm + "\n";
        }
        if (connector.hasDistance)
        {
            output += "Distance (meter): " + connector.distance + "\n";
        }
        if (connector.hasResistance)
        {
            output += "Resistance: " + connector.resistance + "\n";
        }
        if (connector.hasPower)
        {
            output += "Power (Watt): " + connector.power + "\n";
        }
        if (connector.hasAveragePower)
        {
            output += "AveragePower: " + connector.averagePower + "\n";
        }
        if (connector.hasExpendedEnergy)
        {
            output += "ExpendedEnergy: " + connector.expendedEnergy + "\n";
        }

        return output;
    }

    private void OnApplicationQuit()
    {
        connector.Quit();
    }

    public void Connect() {
        if (deviceName.Length > 0 && serviceID.Length > 0 && readCharacteristic.Length > 0 && writeCharacteristic.Length > 0)
        {
            StartCoroutine(connector.Connect(deviceName, serviceID, readCharacteristic, writeCharacteristic));
            connected = true;
        }
    }

    public void WriteResistance(float val) {
        if (connected)
        {
            connector.WriteResistance(val);
            resistanceShow.text = "Resistance: " + Mathf.FloorToInt(val).ToString();
        }
    }

    public void ChangeDeviceName(string _deviceName) {
        deviceName = _deviceName;
    }
    public void ChangeServiceID(string _serviceId)
    {
        serviceID = _serviceId;
    }
    public void SetReadCharacteristics(string _readCharacteristic)
    {
        readCharacteristic = _readCharacteristic;
    }
    public void SetWriteCharacteristics(string _writeCharacteristic)
    {
        writeCharacteristic = _writeCharacteristic;
    }
}
