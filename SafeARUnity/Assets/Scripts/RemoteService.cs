// using System.Collections;
// using UnityEngine;
// using UnityEngine.Networking;

// /// <summary>
// /// This class provides methods to send and receive images to/from a remote server.
// /// </summary>
// /// <remarks>
// /// Documentation:
// /// - UnityWebRequest: https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest.html
// /// - Sending a form to an HTTP server (POST): https://docs.unity3d.com/Manual/UnityWebRequest-SendingForm.html
// /// </remarks>
// public class RemoteService : MonoBehaviour
// {
//     private string serverUrl = "http://seu-servidor-flask.com/caminho-para-a-api";

//     public IEnumerator SendImage(Texture2D texture)
//     {
//         byte[] imageData = texture.EncodeToJPG(); // encode the texture to JPG format

//         // Create a UnityWebRequest object to send the image, using the POST method
//         UnityWebRequest www = new UnityWebRequest(serverUrl, "POST");
//         UploadHandlerRaw upHandler = new UploadHandlerRaw(imageData);
//         www.uploadHandler = upHandler;
//         www.downloadHandler = new DownloadHandlerBuffer();

//         // Send the request and wait for a response
//         yield return www.SendWebRequest();

//         // Check for errors
//         if (www.result != UnityWebRequest.Result.Success)
//         {
//             Debug.LogError(www.error);
//         }
//     }

//     public IEnumerator ReceiveImage(string imageUrl)
//     {
//         // Create a UnityWebRequest object to receive the image
//         UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl);
//         yield return www.SendWebRequest();

//         if (www.result != UnityWebRequest.Result.Success)
//         {
//             Debug.LogError(www.error);
//         }
//         else
//         {
//             // Access the received image
//             Texture2D receivedTexture = DownloadHandlerTexture.GetContent(www);

//             // Use the received texture here as needed
//         }
//     }
// }
