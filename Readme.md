# Preview

https://user-images.githubusercontent.com/29563847/206692277-24c135b1-d06d-4e1e-8ed6-1eaf90afc387.mp4

# Settings 
![photo_2022-12-09_15-27-48](https://user-images.githubusercontent.com/29563847/206692706-76605621-1118-4b7a-a1d9-9cd73ce214bb.jpg)
![photo_2022-12-09_15-27-57](https://user-images.githubusercontent.com/29563847/206692648-3eeeec76-3301-47ad-ad1c-2cf8c81a0c80.jpg)


# Description
This is an interactive grass shader. It fakes some wind (strength and speed is adjustable) and also bends around specified objects.

Cutting grass works by shrinking it to 0.1f height and emitting particles at the cut position. Collision and growing is calculated using a script but the grass is drawn using GPU instancing command using an optimized array with all the information like size, bending etc. The wind bending is done using noise function in the vertex shader.


# How to use

Look at complete setup in `GrassScene`.

