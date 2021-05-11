# UnityML_DatasetGenerator

![alt text](https://github.com/ValerioB88/UnityML_DatasetGenerator/blob/master/other_imgs/main.png)

This is a tool for creating a 2D image dataset from 3D models, taking snapshots of the 3D objects from different viewpoints. It gives you a great flexibility of how to generate the dataset. You only need [Unity](https://unity3d.com/get-unity/download) to run it (my version is 2020.4f1). 


Originally this tool was created for two uses:
1. As a Dataset Generator
2. As an interface for online training with Python (which would allow Computer Vision experiment with an RL setup - or the possibility to generate models at runtime during training)

The second way requires an appropriate Python interface, which is not currently provided. Therefore, the current documentation will only refer to the first usage as a data generator. I might release the Python interface in the future, but in the meantime you are welcome to inspect the code if you want to do it yourself.
Also, this tool was born to perform simulations with sequence of images. Even though it's still possible to use it that way, I'll limit here the explanation for dataset of images. Many options present in the scene and not explained here are relative to Sequence creations. 

This tool is made possible by:
* [ml-agents](https://github.com/Unity-Technologies/ml-agents) for setting up the camera sensor (but more heavily exploited when this tool is used as a RL interface)
* [AsImpL](https://github.com/gpvigano/AsImpL) for asynchronous models loading.


# Getting Started
Given a dataset of 3D objects (currently only .obj), this tool will generate a set of images by taking "frame" or "snapshots" of the objects from different viewpoints. It does that by iteratively placing cameras around an objects and saving the frame on disk. The cameras are placed on a sphere around each object (represented during generation by a blue sphere), and they will automatically point to the center of the object. In the default mode, the cameras will be placed in an uniform grid on the sphere around an object, but there is the option to place them randomly.
A history of the 100 last placement is shown in the scene as small green spheres, and the current cameras (one for each object) are shown as small blue spheres:

![alt text](https://github.com/ValerioB88/UnityML_DatasetGenerator/blob/master/other_imgs/bitmap.png)

The images will be arranged in different folder for each class, ready to be used by standard ML libraries such as Pytorch. 

The tool is intended to be used within Unity directly, and all the options are located in the `Agent` GameObject. Alternatively, for speedup, you can build the scene and run it as an executable (not documented).

There are two ways you can build your dataset. In the first one, the datasets are objects in the Unity scene. In the second one, the dataset is on the disk. If you have few objects, let's say 10 of them, and you want to generate them without any separation in different categories, the first technique is the easiest one. 
If you have a complex dataset on disk, such as ShapeNet, and you need to generate them following their original class structure, then you can use the second method.

**You can create your first dataset straight away by opening one of the example scenes, `Scene/Demo Dataset In The Scene` or `Scene/Demo Dataset from Disk` and click Play. Do not delete any component in the scenes are they are necessary for the dataset generation. Use these scenes as template for you personalize generation.**

## Method 1. Objects in the scene.
This method is demoed in `Scene/Demo Dataset In The Scene`. 
### Preparing the dataset.
#### Step 1. Import the objects with the right structure.
In the Hierarchy, you'll see an object called `DATASETS`, which contains some datasets. You can use them as templates. *If you just want to use them directly to see how everything work, go to Step 2.*
To use your own objects, import them in the scene. You need to organize them in a structure similar to the one used here, eg:
```
DATASETS/
|____DatasetName/
     |____Object1Name/
          |____Object1Name/
               |____Mesh1
               |____Mesh2
               . 
               .
     |____Object2Name/
          |____Object2Name
               |____Mesh1
               |____Mesh2
               .
               .
      .
      .
```

Notice that both `Object1Name` objects _need_ to have `Transform.position` set to `[0, 0, 0]`. 
The Objects need to be scaled to the same size and their pivot point needs to be centered. Fortunately there is a tool for that!  In `Agent` there is a component called `Dataset Utils`. Drag and drop your dataset there and click `Adjust Dataset`. A new dataset with the same name and the suffix `ADJ` will be created, and the original dataset will be hidden. 

#### Step 2. Select the dataset and Run!
In `Agent`, the Component `Sequence Learning Task` contains a field named `Dataset Name`. Type the name of the dataset you want to use (or leave the one selected). In `Save Frames` selected where to save the output images. Then enter Play mode, and everything should work as planned. Each object will be saved in its own folder.

To see different options available check out [Options](#options)

## Method 2. Objects in folders
There is a demo scene in `Scene/Demo Dataset from Disk`. To load the objects asynchronously we use a `BatchProvider` object. The path is specified in the Component `BatchProvider`, field `File Path Dataset`. I provide with some sample 3D object in the folder `./3Dmodels/`. You will need to copy the folder structure:
```
./3Dmodels/
|____Class1Name/
     |____Object1Name/
          |____images/ (optional folder, for textures and other resources - the location of this will depend on your .obj file)
          |____models/
               |_____model_normalized.obj
               |_____ (other optional files such as .mtl, .json etc)
     |____Object2Name/
     .
     .
     .
|____Class2Name/
.
.
.
```
Notice that your .obj file _needs_ to be named `model_normalized.obj`, and need to be in a folder names `models`. 

If you want, change the `Save Folder`, enter play mode, and it should start generating images. Objects are not load all together but in batches, specified by the field `Camera Sets` in `Sequence Build Scene CLI` (default is 4). Loading objects is an expensive operation so this may take long: 100 objects took around 4 minutes on my machine. The whole ShapeNet, 50'000 objects, took around 20 hours. This also highly depends on the complexity of the objects, e.g. the number of meshes. 

At the end of Method 1 or Method 2 you should get your folders with the images inside:
![alt text](https://github.com/ValerioB88/UnityML_DatasetGenerator/blob/master/other_imgs/folder.png)

# Options
There is an abudance of options to personalize the way you want to generate the dataset. I will explain those that I believe most people will want to play around with.

#### Change the Camera Placement parameters
By default the cameras are placed on a sphere centered on the object, at a distance of 4 meters from the center of the object (the ray of the sphere), and they cover an area that goes from 30 to 120 degrees in inclination, and the full sphere azimuth. You can have an idea of the area covered by looking at the green spheres (the "history" of the cameras). You may want to change this parameters.
In `Sequence Learning Task` select `Camera Sphere Parameters`. You will be presented with an overwhelming (and mostly irrelevant amount of options). 
The ones you may want to change are: `Distance`, `Min Center Point Incl T Cameras`, and `Max Center Point Incl T Cameras`. This will change the area covered longitudinally, and you can set it from 0 to 180. Similarly, the following two parameters, `Min Center Point Azi T Cameras` and `Max Center Point Azi T Cameras`, will change the space covered latitudinally (from 0 to 360).
For example, setting these parameters to 4, 20, 30, 0, 180, will result in this type of camera placing:

![alt text](https://github.com/ValerioB88/UnityML_DatasetGenerator/blob/master/other_imgs/change_params.png)

The other parameters are used for creating Sequence of Images and are not covered in this documentation. Use it at your own risk!

#### Grid Spacing
The cameras are placed around the Camera-Sphere in a 10x10 grids. You can change the grid spacing by changing the values in `Num Grid Points`. This will automatically calculate the new grid given the number of points and the placement parameters (see [before](###change the camera placement parameters). The number of iteration to cover the whole grid is automatically adjusted.

#### Random Placement
You can use random placement by changing the value in `Place Camera Mode` to `RND`. If you do so, you need to specify the number of snapshot you want to take for each object in `Repeat Same Batch`. 

#### Select Classes
You can specify a list with the name of the classes to use, ignoring the others.

#### Ignore Massive Objects
By default, the tool will simply skip massive objects (composed by more than 1000 meshes), but you may want to change that. Do it in `Batch Provider` -> `Import Option` -> `Skip if More Meshes`. 

#### Other Options
Other interesting options are self-explainatory. You can change the size of the output image (124 by default) in `Sequence Build Scene CLI` -> `Size Canvas`, the `Seed` in `Sequence Learning Task`, the variability of the lightning in `Change Lights each Iteration` (`false` by default)

#### Changing camera background or other Camera properties
You can do it directly in Unity. Every Camera used in the tool is a clone of `Training Camera`, an object in the Hierarchy (there is also a `Candidate Camera` but you can ignore it). For example, change the background by clicking on the Camera Component and select `Background`.




