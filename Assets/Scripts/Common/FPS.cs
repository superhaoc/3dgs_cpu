using TMPro;
using UnityEngine;
 
 
public class FPS : MonoBehaviour
{
    public TMP_Text txtShow;
    public int targetFrameRate = -1;
    float fps;
    int frames;
    string hudstr;
    float lasttime;
    Rect hudrc;
    Rect hudbkrc;
    
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        #if !UNITY_EDITOR
        if (targetFrameRate > 0)
        {
            Application.targetFrameRate = targetFrameRate;
        }
        #endif
    }
 
    void OnEnable()
    {
    }
 
    void Start()
    {
        fps = 0f;
        frames = 0;
        lasttime = Time.realtimeSinceStartup;
    }
 
    void LateUpdate()
    {
        ++frames;
        float currtime = Time.realtimeSinceStartup;
        if (currtime - lasttime > 1f)
        {
            fps =
                frames / (currtime - lasttime);
            frames = 0;
            lasttime = currtime;
            hudstr = string.Format("FPS : {0:N2}", fps);

            if (txtShow != null)
            {
                txtShow.text = hudstr;
            }
        }
    }
 
    void OnGUI()
    {
#if !UNITY_EDITOR
        GUI.matrix = Matrix4x4.Scale(new Vector3(2, 2, 2));
#endif
        GUIStyle style = new GUIStyle();
        style.fontSize = 30;
        GUILayout.Label(hudstr, style);
#if !UNITY_EDITOR
        GUI.matrix = Matrix4x4.identity;
#endif
    }
}