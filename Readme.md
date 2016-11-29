# Uduino — Simple and robust Arduino-Unity communication
-------

**Uduino** is another Open Source plugin for Unity, which allow to communicate between a Arduino board with Unity. 

## Purpose & Target

This project was created after too much frustration on how to properly connected Unity & Arduino. During past past experiences I was facing some problems: difficulties to detect a specific Arduino board when several are connected, small freeze when reading analog pins, no direct feedbacks when writing arduino, and most important : **not human-readable**.

All existing projects mixing Arduino and Unity where either not cross-platform,  too complex and complicated (using Firmata to control a LED !? Hell no! ) or simply not stable enough. What we really need for our project is a simple Arduino file and some clear c# functions to read or write instructions. 

**Uduino** aims to be a *comprehensive* and *easy to setup* solution for your Arduino/Unity projects. It features **simple declaration** both on Unity and Arduino side.


### How simple ?

You give a *unique name* to your arduino card, and declare which variable is readable. For example I want to access `mySensor` on the arduino board named `myArduinoName`.  Then on Unity, you can access to this value by using `UduinoManager.Instance.Read("myArduinoName", "mySensor");`. Uduino is handling the Serial connection between the software and the hardware !



## Quick Start

1. Import [uduino.unitypackage]() in your project
2. Add  the libraries `Uduino` and `SerialCommand`([link](https://github.com/scogswell/ArduinoSerialCommand)) to your Arduino `libraries` folder.
3. On your Arduino project, add on the top of your code :
````arduino
#include<Uduino.h>
Uduino uduino("myArduinoName"); // "myArduinoName" is your object's name !
````
4. On your main Unity script, initialize Uduino:
```csharp
using Uduino; // adding the NameSpace

public class ExampleScript : MonoBehaviour
{
    UduinoManager u; // your Instance is initialized here !
    void Start() ... // continue your code
}
```
5. Add the [methods](#Unity-Methods), and you're good to go !

## Setup

#### Unity

Download [uduino.unitypackage]() and import it in your current project or open this repository with Unity Editor.

#### Arduino

The `Arduino` folder of this repo contains the Ùniduino`library and examples for Arduino, it can be merged with your Arduino user folder. Uduino is based on [SerialCommand](https://github.com/scogswell/ArduinoSerialCommand) by [Steven Cogswell](https://github.com/scogswell/), released under GPL License. However this library is not needed to run Uduino.




## Examples 

Examples can be found under `Assets\Uduino\Examples`. Their respective arduino code are on the library folders (`Arduino IDE\Examples\Uduino`). It might be the easiest solution to understand Uduino !

### Usage

Here is your todo-list of what you should always do to have Uduino working. 

#### Arduino 
1. Add `#include<Uduino.h>`as dependency
2. Instanciate Uduino and define the name of your board : `Uduino uduino("sensorArduino");` 
3. Add new commands in Setup()
4. Update Uduino in the loop. `if (Serial.available() > 0) uduino.readSerial();`

#### Unity
1. Add he namespace `using Uduino`at the to of your script
2. Declate `UduinoManager u;` as new variable (*note: The Instance is created the First time you call Uduino.Instance . It's  recommanded to declare it as variable to find you connected boards on start.*)

### Simple Read (Arduino to Unity)

Read the value of a sensor conected to A0.

```arduino
#include<Uduino.h>
Uduino uduino("myArduinoName"); // Declare and name your object

void setup()
{
  Serial.begin(9600);
  uduino.addCommand("mySensor", GetVariable); // Link your sensor reading (called "mySensor") to a function
}

void GetVariable() {
  Serial.println(analogRead(A0));
}
void loop()
{
   if (Serial.available() > 0) // verify if the serial is available...
    uduino.readSerial();       // ...then process Uduino. /!\ This part is mandatory
}
```


```csharp
using UnityEngine;
using System.Collections;
using Uduino;

public class SimpleUduino : MonoBehaviour {

    UduinoManager u;

  void Awake ()
  {
        UduinoManager.Instance.OnValueReceived += OnValueReceived; //Create the Delegate
    }

  void Update ()
  {
        UduinoManager.Instance.Read("myArduinoName", "mySensor"); // Read every frame the value of the "mySensor" function on our board. 
  }

    void OnValueReceived(string data, string device)
    {
        Debug.Log(int.Parse(data)); // Use the data as you want !
    }

}

```
Note : To retreive the data on Unity without creating any freeze of your application, you need to create a new [delegate](#Why-using-delegates) function.


### Simple Write (Uniy to Arduino)

Write the PWM value 


## Why using delegates 

A function trying to read the of a Serial port pauses its execution until the reading is complete. If the reading never happens... the software crash !  Because Unity is mono-thread, opening a new thread to do some other calculations might not be safe. However, Uduino has a thread safe function to read and write on the serial port. The values retreived has to be transmitted from on thred to another, and we use *delegates* to do that. You can then use safely `UduinoManager.Instance.Read(..)` in your script. 



## Arduino Methods

| Name          | Description         |
|---------------|---------------------|
|`readSerial()`| Process Uduino every clock turn. Required in the `loop()` function. |
| `addCommand(string, void)` | Attach a command to a specific function. The function will be triggerd when the event is called by Unity |
| `charToInt(args)` | Convet chars* to int |
|`clearBuffer()`| Clear Serial buffer|

## Unity Methods

### Read(target, variable = null, timeout = 100, action = null)

Send a read command to a specific Arduino board.
A Read() command will be returned on the  OnValueReceived() delegate function
        
| Name          | Description         |
|---------------|---------------------|
|`target`| *System.String*<br> Target device name. Not defined means read everything |
|`variable`| *System.String*<br>Variable watched, if defined|
|`timeout`| *System.Integer*<br> Read Timeout, if defined |
|`action`|System.Action<string> Action callback |


### Write(target, message)

Write a command on an Arduino

| Name          | Description         |
|---------------|---------------------|
|`target`| *System.String*<br> Target device name. Not defined means read everything |
|`message`| *System.String*<br>Message to send to the Arduino board|

### Write(target, message, value)

Write a command on an Arduino with a specific value 

| Name          | Description         |
|---------------|---------------------|
|`target`| *System.String*<br> Target device name. Not defined means read everything |
|`message`| *System.String*<br>Message to send to the Arduino board|
|`value`|*System.Integer*<br>Value associated with the message|

### Write(target, message[], value[])

| Name          | Description         |
|---------------|---------------------|
|`target`| *System.String*<br> Target device name. Not defined means read everything |
|`message`| *System.String[]*<br>Messages to send to the Arduino board|
|`values`|*System.Integer[]*<br>List of values to be sent. Value #is associated with message #|



### GetPortState()

Debug.Log() the status of all connected Serial Ports devices. 

## FAQ


## Contribution

Uduino is an [**OPEN Open Source Project**](http://openopensource.org/). This means that:

> Individuals making significant and valuable contributions are given commit-access to the project to contribute as they see fit. This project is more like an open wiki than a standard guarded open source project.

This is an experiment and feedback is welcome. I'll be very happy to have your contribution on this library. If you create something interesting with uduino, add it to the examples folder, submit a pull-request, and we'll take a look.


## Todo
* Unity: Arduino: Create a "simple" Sketch
* Unity: Create a global #SerialDebug valu.
* Unity: Write(string target, string[] message, int[] value) could take a 2D array as parameter ?
* Arduino: Introduce a custom delay, to avoid blocking situations ?
* Documentation: Explain uduino_hardwareonly mode 

