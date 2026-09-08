using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class FtmsIndoorBike :MonoBehaviour
{
    string deviceName;
    string serviceId;
    string readCharacteristic;
    string writeCharacteristic;

    bool wantConnect = true;
    Dictionary<string, Dictionary<string, string>> devices = new Dictionary<string, Dictionary<string, string>>();
    string selectedDeviceId = "";
    string selectedServiceId = "";
    string selectedCharacteristicId = "";

    bool isSubscribed = false;


    //SPEED
    [ShowInEditor][SerializeField]
    float _speed;
    public float speed
    {
        protected set
        {
            _speed = value;
        }
        get
        {
            return _speed;
        }
    }
    
    public bool hasSpeed
    {
        get;
        protected set;
    }

    //DISTANCE
    [ShowInEditor]
    [SerializeField]
    float _distance;
    public float distance
    {
        protected set
        {
            _distance = value;
        }
        get
        {
            return _distance;
        }
    }

    //UNCLEAR OF ANY OF THE FOLLOWING ARE PROVIDED BY TUO
    public float averageSpeed; 
    public bool hasAverageSpeed
    {
        get;
        protected set;
    }

    public float rpm; 
    public bool hasRpm
    {
        get;
        protected set;
    }

    public float averageRpm; 
    public bool hasAverageRpm
    {
        get;
        protected set;
    }

    public bool hasDistance
    {
        get;
        protected set;
    }

    public float resistance; 
    public bool hasResistance
    {
        get;
        protected set;
    }

    public float power; 
    public bool hasPower
    {
        get;
        protected set;
    }

    public float averagePower; 
    public bool hasAveragePower
    {
        get;
        protected set;
    }

    public float expendedEnergy; 
    public bool hasExpendedEnergy
    {
        get;
        protected set;
    }


    [SerializeField] UnityEvent<float> OnSpeedUpdate;
    

    string lastError;

    float lastWriteTime = 0.0f;
    
    public static FtmsIndoorBike instance
    {
        get;
        protected set;
    }
   
    void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    // Update is called once per frame
    public void Update()
    {
        if (isSubscribed)
        {

            BleApi.BLEData res = new BleApi.BLEData();
            while (BleApi.PollData(out res, false))
            {
                {
                    hasSpeed = false;
                    hasAverageSpeed = false;
                    hasRpm = false;
                    hasAverageRpm = false;
                    hasDistance = false;
                    hasResistance = false;
                    hasPower = false;
                    hasAveragePower = false;
                    hasExpendedEnergy = false;
                }

                int index = 0;
                int flags = BitConverter.ToUInt16(res.buf, index);
                index += 2;
                if ((flags & 0) == 0)
                {
                    hasSpeed = true;
                    float value = (float)BitConverter.ToUInt16(res.buf, index);
                    speed = (value * 1.0f) / 100.0f;
                    index += 2;

                    OnSpeedUpdate.Invoke(speed);
                }
                if ((flags & 2) > 0)
                {
                    //??
                    hasAverageSpeed = true;
                    averageSpeed = BitConverter.ToUInt16(res.buf, index);
                    index += 2;
                }
                if ((flags & 4) > 0)
                {
                    rpm = (BitConverter.ToUInt16(res.buf, index) * 1.0f) / 2.0f;
                    index += 2;

                    OnSpeedUpdate.Invoke(rpm);
                }
                if ((flags & 8) > 0)
                {
                    averageRpm = (BitConverter.ToUInt16(res.buf, index) * 1.0f) / 2.0f;
                    index += 2;
                }
                if ((flags & 16) > 0)
                {
                    distance = BitConverter.ToUInt16(res.buf, index); // ?????s
                    index += 2;

                    OnSpeedUpdate.Invoke(distance);
                }
                if ((flags & 32) > 0)
                {
                    resistance = BitConverter.ToInt16(res.buf, index);
                    index += 2;
                }
                if ((flags & 64) > 0)
                {
                    power = BitConverter.ToInt16(res.buf, index);
                    index += 2;

                    OnSpeedUpdate.Invoke(power);
                }
                if ((flags & 128) > 0)
                {
                    averagePower = BitConverter.ToInt16(res.buf, index);
                    index += 2;
                }
                if ((flags & 256) > 0)
                {
                    expendedEnergy = BitConverter.ToUInt16(res.buf, index);
                    index += 2;
                }

             
            }

            // log potential errors
            BleApi.ErrorMessage resErr = new BleApi.ErrorMessage();
            BleApi.GetError(out resErr);
            if (lastError != resErr.msg)
            {
                Debug.LogError(resErr.msg);
                lastError = resErr.msg;
            }
        }
    }

    
  //  MonoBehaviour mono;
  /*  public FtmsIndoorBike(MonoBehaviour _mono)
    {
        mono = _mono;
    }*/

    // Start is called before the first frame update
    public IEnumerator Connect(string _deviceName = "TUO", string _serviceId = "{00001826-0000-1000-8000-00805f9b34fb}", string _readCharacteristic = "{00002ad2-0000-1000-8000-00805f9b34fb}", string _writeCharacteristic = "{00002ad9-0000-1000-8000-00805f9b34fb}")
    {
        if (!wantConnect) yield break;

        deviceName = _deviceName;
        serviceId = _serviceId;
        readCharacteristic = _readCharacteristic;
        writeCharacteristic = _writeCharacteristic;

        Quit();

        yield return StartCoroutine(ConnectDevice());
        if (selectedDeviceId.Length == 0) yield break;

        Debug.Log("connecting device finish");

        yield return StartCoroutine(ConnectService());
        if (selectedServiceId.Length == 0) yield break;

        Debug.Log("connecting service finish");

        yield return StartCoroutine(ConnectReadCharacteristic());
        if (selectedCharacteristicId.Length == 0) yield break;

        Debug.Log("connecting read characteristic finish");

        CheckSubscribed();
    }

    IEnumerator ConnectDevice()
    {
        Debug.Log("connecting device...");
        BleApi.StartDeviceScan();
        BleApi.ScanStatus status = BleApi.ScanStatus.AVAILABLE;
        BleApi.DeviceUpdate deviceRes = new BleApi.DeviceUpdate();
        do
        {
            status = BleApi.PollDevice(ref deviceRes, false);
            //Debug.Log(count++);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                if (!devices.ContainsKey(deviceRes.id))
                    devices[deviceRes.id] = new Dictionary<string, string>() {
                            { "name", "" },
                            { "isConnectable", "False" }
                        };
                if (deviceRes.nameUpdated)
                    devices[deviceRes.id]["name"] = deviceRes.name;
                if (deviceRes.isConnectableUpdated)
                    devices[deviceRes.id]["isConnectable"] = deviceRes.isConnectable.ToString();
                // consider only devices which have a name and which are connectable
                if (devices[deviceRes.id]["name"] == deviceName && devices[deviceRes.id]["isConnectable"] == "True")
                {
                    //BleApi.Connect(device_res.id);
                    selectedDeviceId = deviceRes.id;
                    break;
                }
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {

                if (selectedDeviceId.Length == 0)
                {
                    Debug.LogError("device " + deviceName + " not found!");
                }
            }
            yield return 0;
        } while (status == BleApi.ScanStatus.AVAILABLE || status == BleApi.ScanStatus.PROCESSING);
    }

    IEnumerator ConnectService()
    {
        Debug.Log("connecting service of device " + selectedDeviceId);
        BleApi.ScanServices(selectedDeviceId);
        BleApi.ScanStatus status;
        BleApi.Service serviceRes = new BleApi.Service();
        do
        {
            //Debug.Log("service_id = " + service_id);
            status = BleApi.PollService(out serviceRes, false);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                if (serviceRes.uuid == serviceId)
                {
                    selectedServiceId = serviceRes.uuid;
                    break;
                }
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {
                if (selectedServiceId.Length == 0)
                {
                    Debug.LogError("service " + serviceId  + " not found!");
                }
            }
            yield return 0;
        } while (status == BleApi.ScanStatus.AVAILABLE || status == BleApi.ScanStatus.PROCESSING);
    }

    IEnumerator ConnectReadCharacteristic()
    {
        Debug.Log("connecting characteristic...");
        BleApi.ScanCharacteristics(selectedDeviceId, selectedServiceId);
        BleApi.ScanStatus status;
        BleApi.Characteristic characteristicsRes = new BleApi.Characteristic();

        do
        {
            status = BleApi.PollCharacteristic(out characteristicsRes, false);
           //Debug.Log(characteristics_res.uuid);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                
                if (characteristicsRes.uuid == readCharacteristic)
                {
                    selectedCharacteristicId = characteristicsRes.uuid;
                    break;
                }
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {
                if (selectedCharacteristicId.Length == 0)
                {
                    Debug.LogError("characteristic " + readCharacteristic + " not found!");
                }
            }
            yield return 0;
        } while (status == BleApi.ScanStatus.AVAILABLE || status == BleApi.ScanStatus.PROCESSING);
    }

    void CheckSubscribed()
    {
        Debug.Log("Subscribe...");
        BleApi.SubscribeCharacteristic_Read(selectedDeviceId, selectedServiceId, selectedCharacteristicId, false);
        isSubscribed = true;
    }


    public void Quit()
    {
        BleApi.Quit();
    }

    private byte[] Convert16(string strText)
    {
        strText = strText.Replace(" ", "");
        byte[] bText = new byte[strText.Length / 2];
        for (int i = 0; i < strText.Length / 2; i++)
        {
            bText[i] = Convert.ToByte(Convert.ToInt32(strText.Substring(i * 2, 2), 16));
        }
        return bText;
    }

    public void Write(string msg)
    {
        
        byte[] payload22 = Convert16(msg);
        BleApi.BLEData data = new BleApi.BLEData();
        data.buf = new byte[512];
        data.size = (short)payload22.Length;
        data.deviceId = selectedDeviceId;
        data.serviceUuid = selectedServiceId;
        data.characteristicUuid = writeCharacteristic;
        for (int i = 0; i < payload22.Length; i++)
        {
            data.buf[i] = payload22[i];
        }
        BleApi.SendData(in data, false);
    }

    public void WriteResistance(float val)
    {
        WriteResistance(Mathf.FloorToInt(val));
    }

    public void WriteResistance(int val)
    {
        if (Time.time - lastWriteTime < 0.1f)
        {
            return;
        }
        else {
            lastWriteTime = Time.time;
        }

        Debug.Log("write resistance: " + val);

        BleApi.SubscribeCharacteristic_Write(selectedDeviceId, selectedServiceId, writeCharacteristic, false);
        Write("00");
        byte resistance1 = Convert.ToByte(val % 256);
        byte resistance2 = Convert.ToByte(val / 256);
        byte[] payload = { 0x11, 0x00, 0x00, resistance1, resistance2, 0x00, 0x00 };
        BleApi.BLEData data = new BleApi.BLEData();
        data.buf = new byte[512];
        data.deviceId = selectedDeviceId;
        data.serviceUuid = selectedServiceId;
        data.characteristicUuid = writeCharacteristic;
        for (int i = 0; i < payload.Length; i++){
            data.buf[i] = payload[i];
        }
        data.size = (short)payload.Length;
        BleApi.SendData(in data, false);
    }
}
