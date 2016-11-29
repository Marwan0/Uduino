﻿/* 
 * Uduino - Yet Another Arduin-Unity Library
 * Version 0.0.1, 2016, Marc Teyssier
 *  
 *  ================
 *       TODOs
 *  ================
 *  UduinoManager.Instance.Read("sensorArduino", "SENSOR",2000); /// TIMEOUT NOT WORKING
 *  Quand on "write", faire une option pour write sur toutes les arduinos (ou mettre target en facultatif ?)
 *  public values for the number of tries ?
 *  Quand on le call et qu'il n'ets pas instancié, l'instancier sur la  scène
 *  Function to discover manually a specific port ?
 *  Faire un script avec des "utils" our le OnValueReceived, comme parser un array de string, etc, convertir string to int, etc
 * 
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;

namespace Uduino
{
    public class UduinoManager : MonoBehaviour {
        
        /// <summary>
        /// UduinoManager unique instance.
        /// Create  a new instance if any UduinoManager is present on the scene.
        /// Set the Uduinomanager only on the first time.
        /// </summary>
        /// <value>UduinoManager static instance</value>
        public static UduinoManager Instance
        {
            get {
                //UduinoManager Instance StackOverflowException 
                UduinoManager[] uduinoManagers = FindObjectsOfType(typeof(UduinoManager)) as UduinoManager[];
                if (uduinoManagers.Length == 0 )
                {
                    Debug.Log("UduinoManager not present on the scene. Creating a new one.");
                    UduinoManager manager = new GameObject("UduinoManager").AddComponent<UduinoManager>();
                    _instance = manager;
                    return _instance;
                }
                else
                    return _instance;
            }
            set {
                if(UduinoManager.Instance == null)
                    _instance = value;
                else
                {
                    Debug.Log("You can only use one UduinoManager. Destroying the new one attached to the GameObject " + value.gameObject.name);
                    DestroyImmediate(value);
                }
            }
        }
        private static UduinoManager _instance = null;

        /// <summary>
        /// Dictionnary containing all the connected Arduino devices
        /// </summary>
        public Dictionary<string, UduinoDevice> uduinoDevices = new Dictionary<string, UduinoDevice>();

        /// <summary>
        /// Create a delegate event to trigger the function OnValueReceived()
        /// Takes one parameter, the returned data.
        /// </summary>
        public delegate void OnValueReceivedEvent(string data, string device);
        public event OnValueReceivedEvent OnValueReceived;

        /// <summary>
        /// Create a delegate event to trigger the function OnExtendedValueReceivedEvent()
        /// </summary>
        /// TODO : REMOVE ?
        public delegate void OnExtendedValueReceivedEvent(string data, string device);
        public event OnExtendedValueReceivedEvent OnExtendedValueReceived;

        /// <summary>
        /// Enable the reading of serial port in a different Thread.
        /// Might be usefull for optimization and not block the runtime during a reading. 
        /// </summary>
        [SerializeField]
        private bool ReadOnThread = true;

        /// <summary>
        /// Debug infos in the console
        /// </summary>
        public static bool DebugInfos = true;

        /// <summary>
        /// BaudRate
        /// </summary>
        [SerializeField]
        private int baudRate = 9600;

        void Awake()
        {
            Instance = this;
            DiscoverPorts();
            if(ReadOnThread) StartThread();
        }

        /// <summary>
        /// Get the ports names, dependings of the current OS
        /// </summary>
        public void DiscoverPorts()
        {
        #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            Discover(new string[] {"/dev/ttyUSB0", "/dev/ttyUSB1", "/dev/ttyUSB2", "/dev/ttyUSB3", "/dev/ttyUSB4", "/dev/ttyUSB5"});
        #else
            Discover(SerialPort.GetPortNames());
        #endif
        }

        /// <summary>
        /// Discover all active serial ports connected.
        /// When a new serial port is connected, send the IDENTITY request, to get the name of the arduino
        /// </summary>
        /// <param name="portNames">All Serial Ports names, dependings of the current OS</param>
        void Discover(string[] portNames)
        {
            if (portNames.Length == 0) Debug.Log("Are you sure your arduino is connected ?");

            foreach (string portName in portNames)
            {
                UduinoDevice uduinoDevice = new UduinoDevice(portName, baudRate);
                int tries = 0;
                do
                {
                    if (uduinoDevice.getStatus() == SerialArduino.SerialStatus.OPEN)
                    {
                        // TODO : Changer ça et faire un read async, qui prend comme callback l'initialisation de Uniduino
                        string reading = uduinoDevice.ReadFromArduino("IDENTITY", 200);
                        if (reading != null && reading.Split(new char[0])[0] == "uduinoIdentity") 
                        {
                            string name = reading.Split(new char[0])[1];
                            uduinoDevices.Add(name, uduinoDevice); //Add the new device to the devices array
                            if (!ReadOnThread) StartCoroutine(ReadSerial(name)); // Initiate the Async reading of variables 
                            if (DebugInfos) Debug.Log("Object <color=#ff3355>" + name + "</color> <color=#2196F3>[" + uduinoDevice.getPort() + "]</color> added to dictionnary");
                            break;
                        }
                        else
                        {
                            Debug.LogWarning("Impossible to get name on <color=#2196F3>[" + portName + "]</color>. Retrying (" + tries + "/20)");
                        }
                    }
                } while (uduinoDevice.getStatus() != SerialArduino.SerialStatus.UNDEF && tries++ < 20);

                if (uduinoDevice.getStatus() == SerialArduino.SerialStatus.UNDEF || uduinoDevice.getStatus() == SerialArduino.SerialStatus.CLOSE)
                {
                    uduinoDevice.Close();
                    uduinoDevice = null;
                }
            }
        }

        /// <summary>
        /// Debug all ports state.
        /// TODO : Really neccessary ? They are always open...
        /// TODO : A mettre dans les utils
        /// </summary>
        public void GetPortState()
        {
            if (uduinoDevices.Count == 0)
            {
                Debug.Log("No port currently open");
            }
            foreach (KeyValuePair<string, UduinoDevice> uduino in uduinoDevices)
            {
                string state = uduino.Value.serial.IsOpen ? "open " : "closed";
                if (DebugInfos) Debug.Log("" + uduino.Value.getPort() + " (" + uduino.Key + ")" + " is " + state);
            }
        }

        /// <summary>
        /// Send a read command to a specific arduino.
        /// A read command will be returned in the OnValueReceived() delegate function
        /// </summary>
        /// <param name="target">Target device name. Not defined means read everything</param>
        /// <param name="variable">Variable watched, if defined</param>
        /// <param name="timeout">Read Timeout, if defined </param>
        /// <param name="callback">Action callback</param>
        public void Read(string target, string variable = null, int timeout = 100, System.Action<string> action = null)
        {
            if (UduinoTargetExists(target))
            {
                uduinoDevices[target].read = variable;
                uduinoDevices[target].callback = action;
            }
        }

        /*
        /// <summary>
        /// Send a read command to a specific arduino.
        /// A read command will be returned in the OnValueReceived() delegate function
        /// </summary>
        /// <param name="target">Target device name. Not defined means read everything</param>
        /// <param name="variable">Variable watched, if defined</param>
        /// <param name="callback">Action callback</param>
        public void Read(string target, string variable = null, System.Action<string> action = null)
        {
            Read(target, variable, callback: action);
        }
        */

        /// <summary>
        /// Write a command on an Arduino
        /// </summary>
        /// <param name="target">Target device</param>
        /// <param name="message">Message to write in the serial</param>
        public void Write(string target, string message)
        {
            if (UduinoTargetExists(target))
                uduinoDevices[target].WriteToArduino(message);
        }

        /// <summary>
        /// Write a command on an Arduino with a specific value  
        /// </summary>
        public void Write(string target, string message, int value) {
            if (UduinoTargetExists(target))
                uduinoDevices[target].WriteToArduino(message + " " + value);
        }

        /// <summary>
        /// Write a command on an Arduino with several commands and values
        /// </summary>
        /// TODO : To improve
        public void Write(string target, string[] message, int[] values) {
            if (UduinoTargetExists(target))
                uduinoDevices[target].AdvancedWriteToArduino(message, values);
        }

        /// <summary>
        /// Verify if the target exists when we want to get a value
        /// </summary>
        /// <param name="target">Target Uduino Name</param>
        /// <returns>Re</returns>
        private bool UduinoTargetExists(string target)
        {
            UduinoDevice uduino = null;
            if (uduinoDevices.TryGetValue(target, out uduino))
                return true;
            else
            {
                Debug.Log("The object " + target + " cannot be found. Are you sure it's connected and correctly detected ?");
                return false;
            }
        }

        /// <summary>
        /// Threading variables
        /// </summary>
        private System.Threading.Thread _Thread = null;
        private bool readAllPorts = true;

        /// <summary>
        /// Initialisation of the Thread reading on Awake()
        /// </summary>
        void StartThread()
        {
            try
            {
                _Thread = new System.Threading.Thread(ReadPorts);
                _Thread.Start();
            }
            catch (System.Threading.ThreadStateException e)
            {
                Debug.LogError(e);
            }
        }

        /// <summary>
        ///  Read the Serial Port data in a new thread.
        /// </summary>
        public void ReadPorts()
        {
            while (readAllPorts)
            {
                foreach (KeyValuePair<string, UduinoDevice> uduino in uduinoDevices)
                {
                    if (uduino.Value.read != null)
                    {
                        string data = uduino.Value.ReadFromArduino(uduino.Value.read, 50);
                        uduino.Value.read = null;
                        ReadData(data, uduino.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Retreive the Data from the Serial Prot using Unity Coroutines
        /// </summary>
        /// <param name="target"></param>
        /// <returns>null</returns>
        public IEnumerator ReadSerial(string target)
        {
            while (true)
            {
                UduinoDevice uduino = null;
                if (uduinoDevices.TryGetValue(target, out uduino))
                {
                    if (uduino.read != null)
                    {
                        string data = uduino.ReadFromArduino(uduino.read, 50);
                        uduino.read = null;
                        yield return null;
                        ReadData(data, target);
                    }
                    else
                    {
                        yield return null;
                    }
                }
                else
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Parse the received data
        /// </summary>
        /// <param name="data">Received data</param>
        /// <param name="target">TODO : for the moment target is unused</param>
        void ReadData(string data, string target = null)
        {
            if (data != null && data != "" && data != "Null")
            {
                UduinoDevice uduino = uduinoDevices[target];
                if (uduino.callback != null) uduino.callback(data);
                else OnValueReceived(data, target);
            }
        }

        /// <summary>
        /// Close all opened serial ports
        /// </summary>
        public void CloseAllPorts()
        {
            if (uduinoDevices.Count == 0)
            {
                if(DebugInfos) Debug.Log("All ports are closed.");
            }
            List<string> keys = new List<string>(uduinoDevices.Keys);
            foreach (string key in keys)
            {
                SerialArduino device = uduinoDevices[key];
                device.Close();
                uduinoDevices.Remove(key);
            }
        }

        public void OnDisable()
        {
            if (ReadOnThread)
            {
                readAllPorts = false;
                if(_Thread != null) _Thread.Abort();
                _Thread = null;
            }
            if(uduinoDevices.Count != 0) CloseAllPorts();
        }

    }
}