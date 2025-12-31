# SerialPortMacros
This is a C# program used to read up to 4 configurable serial ports,log them and write to them, it is still a WIP but it does work quite a bit already.
<img width="798" height="522" alt="image" src="https://github.com/user-attachments/assets/77848a25-0070-46ca-a038-75219a26d727" />

  
Each port can be selected and configured to read and write on the serial ports with different settings, the text received or sent will then be displayed with different colors based on the sender:  
<img width="800" height="522" alt="image" src="https://github.com/user-attachments/assets/2177ce21-547c-4c7b-aadb-743807c38790" />


The color for the port is the same as the cog for the settings above the combobox; the text sent by you will be black and the text sent by a script will be brown.  

To open a serial port you need to select it from the combobox and click the connect button, you can also customize the serial port settings.
After connecting, you can select which ports you receive your messages by checking the checkbox near the port name. 

You can clear the display by clicking the eraser button, you can also toggle auto-scroll with the lock button and toggle the timestamp with the clock button.  

# Logging

<img width="808" height="522" alt="image" src="https://github.com/user-attachments/assets/05c825a2-bac8-437b-928b-9a466a893360" />

By checking the log option under a port, you will enable logging for that port. If you do, when using the logging feature, a text file corresponding to that port will be created in the logs folder in the same directory as the exe file. This file will contain the log of all that was written by that port, if the timestamp option is enabled, the timestamp will also be written (the name of the sender is only visible if the sender is the user in this case).
If two or more ports are selected, the software will write an additional file containing the log of all ports (in this case, the name of the sender will also be written).
By checking the log option near the text box, you will also be logging user inputs. 
  
# Macro Menu
![image](https://github.com/Agasaworkshop/SerialPortMacros/assets/142116808/057b1100-a02f-4a6a-84e4-77cb438d2151)


This program is meant to interact with your electronic projects through the serial port, the idea is to associate a string sent by your device to an action made by your computer, I will refine and add functionalities probably, currently, it can press a button in response to a certain string or send a string to the selected ports in response to a string on the selected ports(to coordinate multiple devices).  
  
You can easily make your macros with this menu, use "New File" to create a new macro (you will have to close and reopen the window to see it in the list), you can then:  
  
  1)see the name  
    
  2)Set the key*, this will let you decide the keyworld that will trigger the action  
    
  3)Set the ports that will be monitored for the key of this script  
    
  4)Set the effect (currently just "keypress" and "write back")    
    
  5)If you select keypress you will want to set the keypress(**)  
    
  6)If you select "write back" you will want to set the reply and check on which ports it will be sent  

Once you've made the macro you will be able to enable it or disable it by checking his checkbox (remember to apply the changes tho).  


# (**)Keypress format
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

  7)If you set the macro key and keypress to "===" and send through the serial port a message beginning in "===" followed by a keypress the program will interpret and press that key (all previous formats apply)
      example: "===+A" sent by your device through the serial port will make the program start pressing the A key;

