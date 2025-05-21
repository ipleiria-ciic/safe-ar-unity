## SafeAR - Image Obfuscation System

This project is an image obfuscation system developed in Unity using the YOLOv8 
model for object detection. The system is designed to detect objects in images 
and apply obfuscation techniques to them. The obfuscation techniques currently 
supported are blurring, pixelation, and masking.

<p align="center">
  <img src="assets/safeAR_eng.png" alt="Placeholder Image">
</p>
<p align="center">Overall Schematic of Image Obfuscation System</p>

**Notes**: 

- Include Input and output formats, size of Render Texture  
- Files to be included: ImgObfuscator,ImgUtils, ImgAnnot 

**TO DO: Adapt tensors readability for GPU backend!**

## Image Obfuscator Integration Guide for Unity

This guide explains how to integrate and configure the Image Obfuscator module into your Unity project. The Image Obfuscator is designed to apply obfuscation effects to specific classes or objects within an image, such as masking, pixelation, blurring, or no obfuscation.

1) Create new Unity project or open an existing project.

2) Install Unity Sentis (versio 1.2.0) using Package Manager (com.unity.sentis)

3) Create a Unity Object (e.g., a GameObject) to project the obfuscated image (ex: SafeARLayer)

4) In Assets, create a Scripts folder and import ImgObfuscator.cs, ImgUtils.cs, and ImgAnnot.cs into the folder.

5) In Assets, create a Models folder and import the YOLOv8 onnx model into the folder.

6) In the Unity Editor, in SafeARLayer, add a Script component and attach the ImgObfuscator.cs script to the component.

    6.1) In the SafeARLayer, add a Render Texture component and assign it to the ImgObfuscator.cs script.

    6.2) In the SafeARLayer, drag and drop the YOLOv8 model into Yolo v8 Asset field in the ImgObfuscator.cs script.

7) Create a Canvas object in the Unity Editor and add an Image object to the Canvas.
7.1) In the Canvas, add Image (UI) component, add a Script component and attach the ImgCanvas.cs script to the component.
7,2) In the ImgCanvas.cs script, assign the Image (UI)  object to the imageUI variable and the Canvas object to the canvasUI variable.


### Setup Instructions

Follow these steps to integrate the Image Obfuscator into your Unity project:

1. **Import the Module:**
   - Download the Image Obfuscator module and import it into your Unity project.

2. **Include Image Object:**
   - Ensure you have a `UnityEngine.UI.Image` object in your Unity project to 
   display the obfuscated image. This object will be used to project the output 
   Sprite.

   ```csharp
   // Image to project Sprite (Unity Editor)
   [SerializeField] private UnityEngine.UI.Image imageUI; 
    ```

#### Configure Obfuscation Mapping:

Define the obfuscation mapping in the `Start()` method. This mapping associates 
class IDs with obfuscation types. 
Available obfuscation types include **Masking**, **Pixelation**, **Blurring**, or None.

**Example:**  
```csharp
obfuscationTypes = new Dictionary<int, Obfuscation.Type>
{
    { 0, Obfuscation.Type.Masking },       // person
    { 1, Obfuscation.Type.Masking },       // bicycle
    { 53, Obfuscation.Type.Pixelation },   // pizza
    { 67, Obfuscation.Type.Masking },      // cell phone
};
```

### Update Method:

In the <code class="language-csharp">Update()</code> method, apply the obfuscation effects to the current frame using the <code class="language-csharp">imgObfuscator.Run()</code> function.

```csharp
void Update()
{
    currentFrame = ToTexture2D(renderTexture);

    outputTexture = imgObfuscator.Run(currentFrame, obfuscationTypes);

    // Projects outputTexture into a sprite
    if (imageUI != null)
    {
        var sprite = Sprite.Create(
            outputTexture,
            new Rect(0, 0, outputTexture.width, outputTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        imageUI.sprite = sprite;
    }
    else
    {
        Debug.LogWarning("imageUI is null");
    }
}
```

### Additional Notes:

- Ensure that the renderTexture and imgObfuscator variables are correctly initialized and assigned in your Unity project.  
- Adjust parameters such as pixelSize, blurSize, maskColor, and alpha as needed for your specific obfuscation requirements.  
- Test the integration thoroughly to ensure proper functionality within your Unity environment.  

By following these steps, you can  integrate and configure the Image Obfuscator module into your Unity project, allowing you to apply obfuscation effects to images based on specified class IDs.

## How It Works

**Initialization:**

The module begins by determining the type of device available for processing, distinguishing between CPU and GPU. Once the device type is identified, the YOLOv8 instance segmentation model is loaded.

**Preprocessing:**

Before object detection can commence, the input image undergoes preprocessing. This involves resizing the image to match the dimensions expected by the YOLOv8 model. 

**Object Detection:**

The heart of the module lies in the object detection phase. Utilizing the loaded YOLOv8 model, the system analyzes the preprocessed input image to identify objects present within it. Upon detection, the model extracts the bounding box coordinates and instance segmentation mask of each identified object.

**Obfuscation:**

Following successful object detection, the module proceeds to obfuscate sensitive objects within the image. For each detected object, an appropriate obfuscation technique is determined based on its type. Techniques such as blurring, pixelation, or masking may be applied to conceal sensitive information effectively.

**Output**

Upon completion of the obfuscation process, the module generates the obfuscated image as the final output. This image contains obscured regions where sensitive objects were detected, safeguarding privacy and confidentiality. The output format remains consistent with the input format, ensuring seamless integration with existing workflows.


<!-- ## Performance

<TO BE COMPLETED> -->

## Citation 

If you use any piece of the code available in this repository, please cite the following paper:

```bibtex
@InProceedings{10.1007/978-3-031-81713-7_13,
author="Ribeiro, Tiago and Marto, Anabela and Gon{\c{c}}alves, Alexandrino and Santos, Leonel and Rabad{\~a}o, Carlos and de C. Costa, Rog{\'e}rio Lu{\'i}s",
title="SafeARUnity: Real-Time Image Processing to Enhance Privacy Protection in LBARGs",
booktitle="Videogame Sciences and Arts",
year="2025",
publisher="Springer Nature Switzerland",
address="Cham",
pages="186--200",
isbn="978-3-031-81713-7",
doi="10.1007/978-3-031-81713-7_13"
}
```


## Future Work

We plan to add support for more obfuscation techniques and object detection 
models in the future. We also plan to optimize the system further to improve 
its performance.

## Contributions

Contributions to this project are welcome. If you have a feature request, bug 
report, or proposal for improvement, please open an issue or submit a pull request.


## Acknowledgements
This work is funded by FCT - Fundação para a Ciência e a Tecnologia, I.P., through project with reference 2022.09235.PTDC.


<hr style="height:0.5px; background-color:grey; border:none;">

<p align="center">
<img src="assets/CIIC_logo.png" width="700px"/>
</p>

<hr style="height:0.5px; background-color:grey; border:none;">
