# Preview

https://user-images.githubusercontent.com/29563847/209691526-c7405757-e0a9-4fb8-857a-9b010747ef22.mp4

# Settings 
![photo_2022-12-09_15-27-48](https://user-images.githubusercontent.com/29563847/206692706-76605621-1118-4b7a-a1d9-9cd73ce214bb.jpg)
![image](https://user-images.githubusercontent.com/29563847/209691659-58bd07d0-f41a-4843-973f-bd4beda97236.png)


# Description
This is an interactive grass shader. It fakes some wind (strength and speed is adjustable) and also bends around specified objects.

Cutting grass works by shrinking it to 0.1f height and emitting particles at the cut position. Collision and growing is calculated using a script but the grass is drawn using GPU instancing command using an optimized array with all the information like size, bending etc. The wind bending is done using noise function in the vertex shader.


# How to use

Look at complete setup in `GrassScene`.






