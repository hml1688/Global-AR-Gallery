using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(ARRaycastManager))]
public class TapToPlaceWeather : MonoBehaviour
{
    public GameObject galleryPrefab;          // ArtGallery
    public Transform xrOrigin;                // XR Origin
    public float holdSeconds = 0.2f;

    public WeatherGalleryManagerVA vaManager;
    public WeatherGalleryManagerHarvard harvardManager;

    ARRaycastManager raycaster;
    GameObject galleryInstance;
    Transform entranceAnchor;
    float pressTime;
    static readonly List<ARRaycastHit> hits = new();

    void Awake() => raycaster = GetComponent<ARRaycastManager>();

    void Update()
    {
        if (galleryInstance) return;  // Already placed, exit detection

        if (Touchscreen.current.primaryTouch.press.isPressed)
        {
            pressTime += Time.deltaTime;
            if (pressTime < holdSeconds) return;

            Vector2 pos = Touchscreen.current.primaryTouch.position.ReadValue();
            if (!raycaster.Raycast(pos, hits, TrackableType.PlaneWithinPolygon)) return;

            PlaceGallery(hits[0].pose);
        }
        else pressTime = 0;
    }

    void PlaceGallery(Pose hitPose)
    {
        // Instantiate prefab
        galleryInstance = Instantiate(galleryPrefab, hitPose.position, Quaternion.identity);

        // Find EntranceAnchor (sub-object)
        entranceAnchor = galleryInstance.transform.Find("EntranceAnchor");
        if (!entranceAnchor)
        {
            Debug.LogError("Cannot find the EntranceAnchor!");
            return;
        }

        // NEW: hand over the current weather
        string currentMain = PlayerPrefs.GetString("WeatherMain", "Clear");
        galleryInstance.GetComponentInChildren<WeatherEffectController>()
        ?.ShowWeather(currentMain);

        // Facing the camera: The entrance faces the user
        Vector3 camPos = Camera.main.transform.position;
        Vector3 fwd = (camPos - hitPose.position); fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        galleryInstance.transform.rotation = Quaternion.LookRotation(-fwd.normalized, Vector3.up);

        // Align the EntranceAnchor with the click position
        Vector3 offset = hitPose.position - entranceAnchor.position;
        galleryInstance.transform.position += offset;

        // Load the weather keywords and display the exhibits
        string kw = PlayerPrefs.GetString("WeatherKeyword", "sun");
        if (vaManager) StartCoroutine(vaManager.LoadWeatherGallery(kw));
        if (harvardManager) StartCoroutine(harvardManager.LoadWeatherGallery(kw));

        // After placement, hide the auxiliary plane points.
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager != null)
        {
            foreach (var plane in planeManager.trackables)
                // Close the existing plane
                plane.gameObject.SetActive(false);
            planeManager.enabled = false;             // Stop subsequent recognition
        }

        // Placement completed. Disable the script.
        enabled = false;
    }
}