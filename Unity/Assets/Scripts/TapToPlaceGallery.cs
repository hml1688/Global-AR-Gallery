using System.Collections; 
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;            // 新输入系统
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class TapToPlaceGallery : MonoBehaviour
{
    [Header("Prefabs & refs")]
    public GameObject galleryPrefab;          // 拖 ArtGallery 预制体
    public Transform xrOrigin;               // XR Origin (Mobile AR)
    public float holdSeconds = 0.4f;          // 长按判定时间

    ARRaycastManager raycaster;
    GameObject galleryInstance;
    Transform  entranceAnchor;                // 运行时缓存

    float pressTime;
    static readonly List<ARRaycastHit> hits = new();

    // ✅ 添加：用于获取场景里的 GalleryManager 脚本
    GalleryManager galleryManager;

    void Awake() => raycaster = GetComponent<ARRaycastManager>();

    void Update()
    {
        // 没放之前，每帧检测长按
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
            pressTime = 0;    // 手指抬起，计时归零
        }
    }

    void PlaceGallery(Pose hitPose)
    {
        // ① 先实例化
        galleryInstance = Instantiate(galleryPrefab, hitPose.position, Quaternion.identity);

        // ② 找入口锚点
        entranceAnchor = galleryInstance.transform.Find("EntranceAnchor");
        if (!entranceAnchor)
        {
            Debug.LogError("EntranceAnchor 未找到！");
            return;
        }

        /* ③ 让 EntranceAnchor 落到点击位置，且正对摄像机 */
        //   ▸ 把场馆旋转到“入口朝向 → 摄像机”
        Vector3 camPos = Camera.main.transform.position;
        Vector3 fwd    = (camPos - hitPose.position); fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        galleryInstance.transform.rotation = Quaternion.LookRotation(-fwd.normalized, Vector3.up);

        //   ▸ 再把整体平移，使 EntranceAnchor 世界坐标 = hitPose.position
        Vector3 offset = hitPose.position - entranceAnchor.position;
        galleryInstance.transform.position += offset;

         // ✅ 添加：找到场景里的 GalleryManager 脚本并调用 ReloadGallery()
        if (!galleryManager)
        {
            galleryManager = FindObjectOfType<GalleryManager>();
        }

        if (galleryManager != null)
        {
            galleryManager.ReloadGallery();
        }
        else
        {
            Debug.LogError("❌ 场景中未找到 GalleryManager 脚本！");
        }

        // ④ 放好后禁用脚本避免重复放置
        enabled = false;
    }
}