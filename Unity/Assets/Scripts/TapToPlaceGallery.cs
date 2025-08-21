using System.Collections; 
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;         
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class TapToPlaceGallery : MonoBehaviour
{
    [Header("Prefabs & refs")]
    public GameObject galleryPrefab;          // Drag the ArtGallery prefab
    public Transform xrOrigin;               // XR Origin (Mobile AR)
    public float holdSeconds = 0.2f;          // Long press to trigger model placement

    ARRaycastManager raycaster;
    GameObject galleryInstance;
    Transform  entranceAnchor;                // User perspective starting point

    float pressTime;
    static readonly List<ARRaycastHit> hits = new();

    // Used to obtain the GalleryManager script in the scene
    public GalleryManagerVA       vaManager;
    public GalleryManagerHarvard  harvardManager;

    void Awake() => raycaster = GetComponent<ARRaycastManager>();

    void Update()
    {
        // Before placing the model, each frame was detected by long press.
        if (galleryInstance) return;

        if (Touchscreen.current.primaryTouch.press.isPressed)
        {
            pressTime += Time.deltaTime;
            if (pressTime < holdSeconds) return;

            Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
            if (!raycaster.Raycast(touchPos, hits, TrackableType.PlaneWithinPolygon)) return;

            Pose hitPose = hits[0].pose;
            PlaceGallery(hitPose);
        }
        else
        {
            pressTime = 0;    // When the finger is lifted, the timer resets to zero.
        }
    }

    void PlaceGallery(Pose hitPose)
    {
        // Instantiate the model first
        galleryInstance = Instantiate(galleryPrefab, hitPose.position, Quaternion.identity);

        // Find the entrance (User perspective starting point)
        entranceAnchor = galleryInstance.transform.Find("EntranceAnchor");
        if (!entranceAnchor)
        {
            Debug.LogError("Cannot find the EntranceAnchor!");
            return;
        }

        // Make the EntranceAnchor position itself at the click location and face the camera directly.
        // Rotate the gallery to "entrance facing → camera"
        Vector3 camPos = Camera.main.transform.position;
        Vector3 fwd    = (camPos - hitPose.position); fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        galleryInstance.transform.rotation = Quaternion.LookRotation(-fwd.normalized, Vector3.up);

        // Then perform an overall translation so that the world coordinates of EntranceAnchor = hitPose.position
        Vector3 offset = hitPose.position - entranceAnchor.position;
        galleryInstance.transform.position += offset;

         // Locate the GalleryManager script in the scene and call the ReloadGallery() function
        if (vaManager)      vaManager.ReloadGallery();
        if (harvardManager) harvardManager.ReloadGallery(); 
        else
        {
            Debug.LogError("Cannot find the GalleryManager.cs!");
        }

        // After placing the model, hide the plane points.
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager != null)
        {
            foreach (var plane in planeManager.trackables)
            plane.gameObject.SetActive(false); 
            planeManager.enabled = false;             // Stop the subsequent recognition
            }
            
        // After placing the gallery model, disable the script to avoid repeated placement.
        enabled = false;
    }
}