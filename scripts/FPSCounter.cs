using UnityEngine;
using UnityEngine.UI;

public class FPSCounter : MonoBehaviour
{
    private int frames=0;
    private float Ftime;
    public Text fpsDisplay;
    [Range(0,1)]
    public float updateInterval;
    public static float forPlayer=60f;

	// Use this for initialization
	void Start ()
    {
        Ftime = Time.time;
        fpsDisplay.text = frames.ToString();
	}
	
	// Update is called once per frame
	void Update ()
    {
        Application.targetFrameRate = 60;
        frames++;
        float f = frames/updateInterval;
        if(Time.time > Ftime+updateInterval)
        {
            forPlayer = f;
            fpsDisplay.text = f.ToString();
            frames = 0;
            Ftime+= updateInterval;
        }
	}
}
