This is a modification of Interesting Terrains by Monobelisk, intended to work in tandem with Enhanced and Eroded Terrains.

https://www.nexusmods.com/daggerfallunity/mods/166/

https://github.com/Freak2121/Enhanced-ErodedTerrains

Original readme:

# Interesting Terrains
![Screenshot of Interesting Terrains](https://staticdelivery.nexusmods.com/mods/2927/images/headers/115_1600006224.jpg "Interesting Terrains")

- [Interesting Terrains on Nexus](https://www.nexusmods.com/daggerfallunity/mods/115)
- [Interesting Terrains on DFWorkshop](https://forums.dfworkshop.net/viewtopic.php?f=14&t=4062)

Full source code for Interesting Terrains.

Includes a ScriptableObject with a custom editor for creating a different set of heightmap generator params. To use the altered parameters, hit the Export as INI button and overwrite `Assets/interesting_terrains.txt`.

Also includes a script for quickly visualizing each heightmap generator in 3D. To use it:
1. Create an empty GameObject in the scene.
2. Attach the script TestTerrain.cs

Currently, the only way to preview different heightmap generators is to manually edit the `HeightSample` function in `Testing/Editor/Shaders/TerrainTestShader.shader`.
