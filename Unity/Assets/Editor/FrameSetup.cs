#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class FrameSetup : EditorWindow
{
    Material mat;

    [MenuItem("Tools/ArtGallery/Setup Selected Frames")]
    static void Init(){ GetWindow<FrameSetup>("Frame Setup"); } // Create the editor window

    void OnGUI()
    {
        // Load default material
        if(!mat)
            mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Mat_Picture.mat");
        // Material Selection Box
        mat = (Material)EditorGUILayout.ObjectField("Canvas Material", mat, typeof(Material), false);

        GUILayout.Space(10);
        // Execution button
        if(GUILayout.Button("Add Canvas + ArtFrame to selection"))
        {
            if(!mat){ Debug.LogError("Please specify the Mat_Picture material first."); return; }
            foreach(var go in Selection.gameObjects)
                CreateCanvas(go,mat);
            Debug.Log("Done! Canvases have been generated for "+Selection.gameObjects.Length+" Frames.");
        }
    }

    static void CreateCanvas(GameObject frame, Material mat)
{
    if(frame.GetComponentInChildren<ArtFrame>()) return;

    var mr = frame.GetComponent<MeshRenderer>();
    if(!mr) { Debug.LogWarning(frame.name + " 没有 MeshRenderer"); return; }

    // 1️⃣ 计算法线 -----------------------------
    Vector3 sz = mr.bounds.size;
    // 找到最小轴索引（0=x,1=y,2=z）
    int minAxis = (sz.x < sz.y && sz.x < sz.z) ? 0 : (sz.y < sz.z ? 1 : 2);
    Vector3 worldNormal = Vector3.zero;
    worldNormal[minAxis] = 1f; // 取 + 方向
    // 把法线转到父物体局部
    Vector3 localNormal = frame.transform.InverseTransformVector(worldNormal).normalized;

    // 2️⃣ 创建 Quad -----------------------------
    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
    quad.name = "Canvas";
    quad.transform.SetParent(frame.transform, false);

    // 居中 + 推前 1cm
    Vector3 localCenter = frame.transform.InverseTransformPoint(mr.bounds.center);
    quad.transform.localPosition = localCenter + localNormal * 0.01f;

    // 朝向
    quad.transform.localRotation = Quaternion.LookRotation(localNormal);

    // 尺寸（用局部 XY）
    Vector3 localSize = frame.transform.InverseTransformVector(sz);
    quad.transform.localScale = new Vector3(Mathf.Abs(localSize.x),
                                            Mathf.Abs(localSize.y), 1);

    // 材质 & 组件
    var qmr = quad.GetComponent<MeshRenderer>();
    qmr.sharedMaterial = mat;
    DestroyImmediate(quad.GetComponent<MeshCollider>());

    quad.tag = "ArtFrame";
    quad.AddComponent<ArtFrame>();
}


}
#endif
