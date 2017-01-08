﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Uduino
{
    // We use a class Pin to optimize
    public class Pin
    {
        public UduinoManager manager = null;

        public string arduino = "";

        public PinMode pinMode;
        public PinMode prevPinMode;

        public int currentPin = -1;
        public int prevSendValue = 0;

        public Pin(string arduinoParent, int pin, PinMode mode)
        {
            manager = UduinoManager.Instance;
            arduino = arduinoParent;
            currentPin = pin;
            ChangePinMode(mode);
        }

        public virtual void SendMessage(string message)
        {
          manager.SendMessage(arduino, message);
        }

        public bool PinTargetExists(string parentArduinoTarget, int currentPinTarget)
        {
            if (parentArduinoTarget == arduino && currentPinTarget == currentPin) return true;
            else return false;
        }

        /// <summary>
        /// Change Pin mode
        /// </summary>
        /// <param name="mode">Mode</param>
        public void ChangePinMode(PinMode mode)
        {
            pinMode = mode;
            SendMessage("s " + currentPin + " " + (int)pinMode);
        }

        /// <summary>
        /// Send OptimizedValue
        /// </summary>
        /// <param name="sendValue">Value to send</param>
        public void SendPinValue(int sendValue)
        {
            if(sendValue != prevSendValue)
            {
                SendMessage("w " + currentPin + " " + sendValue);
                prevSendValue = sendValue;
            }
        }

        public void Destroy()
        {
            SendMessage("w " + currentPin + " 0");
        }

        public virtual void Draw()
        {
        }

    }

}