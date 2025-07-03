using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(ARRaycastManager))]
public class TapToPlaceWeather : MonoBehaviour
{
    public GameObject galleryPrefab;          // 拖 ArtGallery
    public Transform xrOrigin;                // XR Origin
    public float holdSeconds = 0.4f;

    public WeatherGalleryManagerVA      vaManager;
    public WeatherGalleryManagerHarvard harvardManager;

    ARRaycastManager raycaster;
    GameObject galleryInstance;
    Transform entranceAnchor;
    float pressTime;
    static readonly List<ARRaycastHit> hits = new ();

    void Awake() => raycaster = GetComponent<ARRaycastManager>();

    void Update()
    {
        if (galleryInstance) return;  // 已放置，退出检测

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
        // ① 实例化 prefab
        galleryInstance = Instantiate(galleryPrefab, hitPose.position, Quaternion.identity);

        // ② 找 EntranceAnchor（子物体）
        entranceAnchor = galleryInstance.transform.Find("EntranceAnchor");
        if (!entranceAnchor)
        {
            Debug.LogError("❌ EntranceAnchor 未找到！");
            return;
        }

        // ③ 朝向摄像机：入口朝向用户
        Vector3 camPos = Camera.main.transform.position;
        Vector3 fwd = (camPos - hitPose.position); fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        galleryInstance.transform.rotation = Quaternion.LookRotation(-fwd.normalized, Vector3.up);

        // ④ 把 EntranceAnchor 对准点击位置
        Vector3 offset = hitPose.position - entranceAnchor.position;
        galleryInstance.transform.position += offset;

        // ⑤ 加载天气关键词并展示展品
        string kw = PlayerPrefs.GetString("WeatherKeyword", "sun");
        if (vaManager)      StartCoroutine(vaManager.LoadWeatherGallery(kw));
        if (harvardManager) StartCoroutine(harvardManager.LoadWeatherGallery(kw));

        // ⑥ 放置完毕，禁用脚本
        enabled = false;
    }
}
