## SafeAR - Image Obfuscation System

This project is an image obfuscation system developed in Unity using the YOLOv8 
model for object detection. The system is designed to detect objects in images 
and apply obfuscation techniques to them. The obfuscation techniques currently 
supported are blurring, pixelation, and masking.

## How It Works

1. **Image Resizing**: The system first resizes the input image to a size that 
is suitable for object detection. This is done using Unity's `RenderTexture`
 and `Graphics.Blit` methods.

2. **Object Detection**: The system then uses the YOLOv8 model to detect objects 
in the resized image. This is done using the `worker.Execute(inputTensor)` 
method, where `inputTensor` is a tensor representation of the image.

3. **Obfuscation**: Once the objects have been detected, the system applies the 
specified obfuscation technique to each object. This is done using the 
`ProcessMask` method, which calculates a mask for each object and applies the 
obfuscation to the area of the image covered by the mask.

## Usage

To use the system, you need to provide an input image and specify the type of 
obfuscation you want to apply. The system will then output an image with the 
specified obfuscation applied to the detected objects.

## Performance

The performance of the system can vary depending on the size of the input image 
and the number of objects in it. The system is designed to be as efficient as 
possible, but object detection and image obfuscation are inherently computationally 
intensive tasks. If you experience performance issues, consider resizing your 
input image to a smaller size or using a faster object detection model.

## Future Work

We plan to add support for more obfuscation techniques and object detection 
models in the future. We also plan to optimize the system further to improve 
its performance.

## Contributions

Contributions to this project are welcome. If you have a feature request, bug 
report, or proposal for improvement, please open an issue or submit a pull request.
