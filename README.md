# SerialPortMacros
This is a C# program used to read up to 4 configurable serial ports,log them and write to them, it can also plot in real time multiple variables for each port.    
<img width="800" height="567" alt="image" src="https://github.com/user-attachments/assets/106fca52-d07b-49a9-89ac-bcc57c24bd37" />

  
Each port can be selected and configured to read and write on the serial ports with different settings. The text received or sent will then be displayed with different colors based on the sender:  
<img width="799" height="566" alt="image" src="https://github.com/user-attachments/assets/455ee976-55d7-43ed-83c5-99e425c24c19" />


The color for the port is the same as the cog for the settings above the combobox; the text sent by you will be black and the text sent by a script will be brown.    

To open a serial port, you need to select it from the combobox and click the connect button, you can also customize the serial port settings.   
After connecting, you can select which ports you receive your messages by checking the checkbox near the port name.    

You can clear the display by clicking the eraser button. You can also toggle auto-scroll with the lock button and toggle the timestamp with the clock button.    

# Logging

<img width="800" height="569" alt="image" src="https://github.com/user-attachments/assets/73030c52-3b37-4fae-8bd7-77c6dd5b90fb" />

By checking the log option under a port, you will enable logging for that port. If you do, when using the logging feature, a text file corresponding to that port will be created in the logs folder in the same directory as the exe file.  This file will contain the log of all that was written by that port, if the timestamp option is enabled, the timestamp will also be written (the name of the sender is only visible if the sender is the user in this case).    
If two or more ports are selected, the software will write an additional file containing the log of all ports (in this case, the name of the sender will also be written).    
By checking the log option near the text box, you will also be logging user inputs.    

# Plotting
<img width="1918" height="1015" alt="image" src="https://github.com/user-attachments/assets/535a5f56-c7e3-442d-bfec-a6f98fb20602" />

Under each port, you can find a button to open the graphs; the number near the button decides how many plots will be opened. When reading the lines in the serial port, the software will associate the first number in the line to the first graph, the second number to the second graph, and so on. There are no defined delimiters; just make sure to add something between your numbers that isn't a dot(used for decimals). In the plot window, you can also select how long the time window should be.    

## Merging graphs
You can also merge two graphs by using the merge button  
<img width="1570" height="514" alt="image" src="https://github.com/user-attachments/assets/e7df99b2-9336-4e63-b991-0d3c69e8a95a" />  
In the image, the graph on the right is looking to merge. After clicking the merge button on a second graph, the two windows will become invisible, and a new merged window will appear   
  

<img width="787" height="513" alt="image" src="https://github.com/user-attachments/assets/bec74c95-a074-4af0-bd73-a33f8f500ea0" />

You can toggle the legend by clicking the eye icon at the bottom.  
You can show the original windows by clicking the new unmerge button; this will not close the merged window.      
If you close the merged graph, the original graphs will show up again.   
There is no hard limit on the number of graphs you can plot together. 

# Macro Menu
<img width="800" height="486" alt="image" src="https://github.com/user-attachments/assets/e99b4de0-6c87-4756-acf2-2377e30cfcb5" />


This program is meant to interact with your electronic projects through the serial port, the idea is to associate a string sent by your device to an action made by your computer, currently, it can press a button in response to a certain string or send a string to the selected ports in response to a string on the selected ports(to coordinate multiple devices).    
  
You can easily make your macros with this menu. Use "New File" to create a new macro. You can then:  
  
  1)See the name  
    
  2)Set the key(*), which will let you decide the keyword that will trigger the action  
    
  3)Set the ports that will be monitored for the key of this script  
    
  4)Set the effect (currently just "keypress" and "write back")    
    
  5)If you select keypress, you will want to set the keypress(**)  
    
  6)If you select "write back" you will want to set the reply and check on which ports it will be sent  

Once you've made the macro, you will be able to enable it or disable it by checking its checkbox (remember to apply the changes tho).  


# (**)Keypress format
In the keypress text box, you can:  
  
  1)Use a character by its virtual-key code value (https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes):  
    to do so: add a * at the start    
      example: "*0x20" will press the 'SPACE' key;  
        
  2)Write a single character you want to press. This is mostly meant to be used with alphanumeric characters; for letters, you will have to use the capital letter  
      example: "A" will press the a key;  
      
  3)Add a '+' before the key to press down the key '-' to release the key  
      example: "+A" will start pressing the 'a' key;   
      example: "-A" will stop pressing the 'a' key;  
      example: "*+0x20" will start pressing the 'SPACE' key;  
      
  4)Add a '%' before and after the key (it's either +/-/%) and then a time to press it, the program will keep the button pressed for the specified time  
      example: "%1000%A" will keep the 'a' key pressed for 1000ms or 1s;   
      example: "%1000%0x20" will keep the 'SPACE' key pressed for 1000ms or 1s;  
      
  5)Write a string that will be typed out  
  
  6)There are three other strings that you can use for useful stuff  
      !LMB will press the left mouse button;  
      !RMB will press the right mouse button;  
      !MMB will press the middle mouse button;  

#(*)Key format
  1)The program will look for the incoming stringsthat contain the key(not necessarily just the key)   
  
  2)If you set the macro key and keypress to "===" and send through the serial port a message beginning in "===" followed by a keypress, the program will interpret and press that key (all previous formats apply)
      Example: "===+A" sent by your device through the serial port will make the program start pressing the A key;

  3)If you set the macro key to "*" it will react to every message regardless of the content (I needed this mostly for reply)
