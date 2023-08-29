# SerialPortMacros
This is a C# program used to read up to 4 configurable serial ports and write to them, it is still a WIP but it does work quite a bit already, it will have a lot of unhandled errors tho...  
![image](https://github.com/Agasaworkshop/SerialPortMacros/assets/142116808/7aad28a2-f5ec-4e63-9603-f375e05a9e6d)

This program is meant to interact with your electrical projects through the serial port, the idea is to associate a string sent by your device to an action made by your computer, I will refine and add functionalities probably, currently, it can press a button in response to a certain string or send a string to the selected ports in response to a string on the selected ports(to coordinate multiple devices).  

Each port can be selected and configured to read and write on the serial ports with different settings, the text received or sent will then be displayed with different colors based on the sender:  
![image](https://github.com/Agasaworkshop/SerialPortMacros/assets/142116808/32f5f7ad-ac02-49c2-99a0-e8922e756a08)

The color for the port is the same as the cog for the settings above the combobox, the text sent by you will be black and the text sent by a script will be brown.  

To interact with the port you have to select it, you could also set some variables but there are some standard ones, then check the checkbox near the port name and click the connect button.  

You can clear the display by clicking the eraser button, you can also enable and disable auto-scroll with the lock button.  

# Macro Menu
![image](https://github.com/Agasaworkshop/SerialPortMacros/assets/142116808/057b1100-a02f-4a6a-84e4-77cb438d2151)

You can easily make your macros with this menu, use "New File" to create a new macro (you will have to close and reopen the window to see it in the list), you can then:  
  1)see the name  
  2)Set the key, this will let you decide the keyworld that will trigger the action   
  3)Set the ports that will be monitored for the key of this script  
  4)Set the effect (currently just "keypress" and "write back")    
  5)If you select keypress you will want to set the keypress(***)  
  6)If you select "write back" you will want to set the reply and check on which ports it will be sent  

Once you've made the macro you will be able to enable it or disable it by checking his checkbox (remember to apply the changes tho).  

# (***)Keypress format
In the keypress text box you can:
  1)Use a character by his virtual-key code value (https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes):  
    to do so: add a * at the start    
      example: "*0x20" will press the 'SPACE' key;  
  2)Write a single character you want to press, this is mostly meant to be used with alphanumeric characters, for letters you will have to use the capital letter  
      example: "A" will press the a key;  
  3)Add a '+' before the key to press down the key '-' to release the key  
      example: "+A" will start pressing the 'a' key;   
      example: "-A" will stop pressing the 'a' key;  
      example: "*+0x20" will start pressing the 'SPACE' key;  
  4)Add a '%' before and after the key (it's either +/-/%) and then a time to press it, the program will keep the button pressed for the specified time  
      example: "%1000%A" will keep the 'a' key pressed for 1000ms or 1s;   
      example: "%1000%0x20" will keep the 'SPACE' key pressed for 1000ms or 1s;  
  5)Write a string that will typed out  
  6)There are three other strings that you can use for useful stuff  
      !LMB will press the left mouse button;  
      !RMB will press the right mouse button;  
      !MMB will press the middle mouse button;  
