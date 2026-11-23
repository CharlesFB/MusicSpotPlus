using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInfoSingleton : MonoBehaviour
{

    public static GameInfoSingleton instance; //单例模式
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    //游戏前

    public string musicName = "Originally";
    public string author = "SNKS";
    public string diff = "Hard";
    public int musicId = 3;//音乐编号，用于MusicMananger播放;同时用于背景图改变
    public float bpm = 100;
    public int beat = 1;
    public int singlePerfectScore = 400;
    public int touchPerfectScore = 300;
    public int pressPerfectScore = 700;
    public int extraScore = 100;
    public int totalCombo = 290; //谱面的七个关键信息

    //游戏中
    public bool isPaused = false;
    public float[] gaps = {0.62f, 0.78f, 1.1f, 1.66f, 1.852f, 1.932f}; //相对判定点 1.1 秒，两侧容差放大 60%

    //结算后

    public string score;
    public string level;//评级
    public int maxCombo;
}
