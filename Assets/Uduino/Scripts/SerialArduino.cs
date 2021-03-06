﻿using UnityEngine;
using System;
using System.Collections;
using System.IO.Ports;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Uduino
{
    public class SerialArduino
    {
        //Serial status
        public SerialPort serial;

        private string _port;

        private int _baudrate;

        SerialStatus serialStatus = SerialStatus.UNDEF;

        //Timeout
        public int readTimeout 
        {
            get {
                if (serial != null) return serial.ReadTimeout;
                else return 0;
            }
            set {
                if (serial != null)
                    serial.ReadTimeout = value;
            }
        }

        //Timeout
        public int writeTimeout
        {
            get
            {
                if (serial != null) return serial.WriteTimeout;
                else return 0;
            }
            set
            {
                if (serial != null)
                    serial.WriteTimeout = value;
            }
        }

        //Messages reading
        private Queue readQueue, writeQueue, messagesToRead;
        int maxQueueLength = 10;
        public bool autoRead = false;

        public SerialArduino(string port, int baudrate = 9600)
        {
            _port = port;
            _baudrate = baudrate;

            readQueue = Queue.Synchronized(new Queue());
            writeQueue = Queue.Synchronized(new Queue());
            messagesToRead = Queue.Synchronized(new Queue());

            Open();
        }

        /// <summary>
        /// Open a specific serial port
        /// </summary>
        public void Open()
        {
            try
            {
                #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                _port = "\\\\.\\" + _port;
                #endif
                serial = new SerialPort(_port, _baudrate, Parity.None, 8, StopBits.One);
                serial.ReadTimeout = 100;
                serial.WriteTimeout = 100;
                serial.Close();
                serial.Open();
                serialStatus = SerialStatus.OPEN;

                Log.Info("Opening stream on port <color=#2196F3>[" + _port + "]</color>");
            }
            catch (Exception e)
            {
                Log.Error("Error on port <color=#2196F3>[" + _port + "]</color> : " + e);
                serialStatus = SerialStatus.CLOSE;
            }
        }

        #region Public functions
        /// <summary>
        /// Return port status 
        /// </summary>
        /// <returns>SerialArduino.SerialStatus</returns>
        public SerialStatus getStatus()
        {
            return serialStatus;
        }

        /// <summary>
        /// Return serial port 
        /// </summary>
        /// <returns>Current opened com port</returns>
        public string getPort()
        {
            return _port;
        }

        /// <summary>
        /// A board with Uduino is found 
        /// </summary>
        public void UduinoFound()
        {
            serialStatus = SerialStatus.FOUND;
            #if UNITY_EDITOR
            if(Application.isPlaying) EditorUtility.SetDirty(UduinoManager.Instance);
            #endif
        }
        /*
        /// <summary>
        /// Set serial read timeout
        /// </summary>
        /// <param name="timeout">Timeout value, in ms</param>
        public int SetReadTimeout(int timeout)
        {
            if (serial != null) serial.ReadTimeout = timeout;
            return timeout;
        }

        /// <summary>
        /// Set write Timeout
        /// </summary>
        /// <param name="timeout">Timeout value, in ms</param>
        public int SetWriteTimeout(int timeout)
        {
            if (serial != null) serial.WriteTimeout = timeout;
            return timeout;
        }
        */
        #endregion

        #region Commands

        /// <summary>
        /// Write a message to a serial port
        /// </summary>
        /// <param name="message">Message to write on this arduino serial</param>
        /// <param name="value">Extra value to send</param>
        /// <param name="instant">Write the message value now and not in the thread loop</param>
        public void WriteToArduino(string message, object value = null, bool instant = false)
        {
            if (message == null || message == "" )
                return;

            if (value != null)
                message = " " + value.ToString();

            if(!writeQueue.Contains(message) && writeQueue.Count < maxQueueLength)
                writeQueue.Enqueue(message);

            if(instant)
                WriteToArduinoLoop();
        }

        /// <summary>
        /// Loop every thead request to write a message on the arduino (if any)
        /// </summary>
        public void WriteToArduinoLoop()
        {
            if (serial == null || !serial.IsOpen)
                return;

            if (writeQueue.Count == 0)
                return;

            string message = (string)writeQueue.Dequeue();

            try
            {
                try
                {
                    serial.WriteLine(message + "\r\n");
                    serial.BaseStream.Flush();
                    Log.Info("<color=#4CAF50>" + message + "</color> sent to <color=#2196F3>[" + _port + "]</color>");
                }
                catch (System.IO.IOException e)
                {
                    writeQueue.Enqueue(message);
                    Log.Warning("Impossible to send a message to <color=#2196F3>[" + _port + "]</color>," + e);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                Close();
            }
            WritingSuccess(message);
        }

        /// <summary>
        /// Read Arduino serial port
        /// </summary>
        /// <param name="message">Write a message to the serial port before reading the serial</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="instant">Read the message value now and not in the thread loop</param>
        /// <returns>Read data</returns>
        public string ReadFromArduino(string message = null, int timeout = 0, bool instant = false)
        {
            if (serial == null || !serial.IsOpen || serialStatus == SerialStatus.STOPPING)
                return null;

            if (timeout > 0 && timeout != serial.ReadTimeout)
                readTimeout = timeout;

            if (message != null && messagesToRead.Count < maxQueueLength)
                messagesToRead.Enqueue(message);

            if (instant)
                ReadFromArduinoLoop();

            if (readQueue.Count == 0)
                return null;

            string finalMessage = (string)readQueue.Dequeue();

            //TODO : Test if the first time it returns something
            return finalMessage;
        }

        public void ReadFromArduinoLoop()
        {
            if (serial == null || !serial.IsOpen || serialStatus == SerialStatus.STOPPING)
                return;

            if (messagesToRead.Count > 0)
                WriteToArduino((string)messagesToRead.Dequeue(),instant:true);

            else if(autoRead) { }
            else
            {
                Log.Debug("TODO : It read a message only if a message  r is sent");
                // Incompatible with a "always read" method to trigger events
                return;
            }

            serial.DiscardOutBuffer();
            serial.DiscardInBuffer();
            
            try
            {
                try
                {
                    string readedLine = serial.ReadLine();
                    ReadingSuccess(readedLine);
                    if (readedLine != null && readQueue.Count < maxQueueLength)
                        readQueue.Enqueue(readedLine);
                }
                catch (TimeoutException e)
                {
                    Log.Info("ReadTimeout. Are you sure someting is written in the serial of the board ? \n"  + e);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                Close();
            }
        }


        /// <summary>
        /// Callback function when a message is written 
        /// </summary>
        /// <param name="message">Message successfully writen</param>
        public virtual void WritingSuccess(string message) { }

        /// <summary>
        /// Callback function when a message is read 
        /// </summary>
        /// <param name="message">Message successfully read</param>
        public virtual void ReadingSuccess(string message) { }
        #endregion

        #region Close

        public void Stopping()
        {
            WriteToArduino("disconnected",instant:true);
            serialStatus = SerialStatus.STOPPING;
        }

        /// <summary>
        /// Close Serial port 
        /// </summary>
        public void Close()
        {
            WriteToArduinoLoop();

            readQueue.Clear();
            writeQueue.Clear();
            messagesToRead.Clear();

            if (serial != null && serial.IsOpen)
            {
                Log.Warning("Closing port : <color=#2196F3>[" + _port + "]</color>");
                serial.Close();
                serialStatus = SerialStatus.CLOSE;
                serial = null;
            }
            else
            {
                Log.Info(_port + " already closed.");
            }
        }

        /// Specal Handler when application quit;
        private bool isApplicationQuitting = false;

        void OnDisable()
        {
            if (isApplicationQuitting) return;
            Close();
        }

        void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }
        #endregion

    }
}