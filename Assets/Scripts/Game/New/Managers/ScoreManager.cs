using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ScoreManager : MonoBehaviour
{
    public GameObject mainLine;
    public GameObject topLine;
    public GameObject bottomLine;
    public GameObject area;
    public GameObject back;

    public GameObject singlePrefab;
    public GameObject touchPrefab;
    public GameObject pressPrefab;

    public Transform spotsPanel;
    public Image blackInGame;

    public Sprite blackBack;
    private Sprite musicBack;

    public string registerBuilder;
    private string registerLine;
    private string registerArea;
    private string registerLine1;
    private string registerLine2;
    private string registerBack;

    private RectTransform mainLineRect;
    private RectTransform topLineRect;
    private RectTransform bottomLineRect;
    private RectTransform areaRect;
    private Image mainLineImage;
    private Image topLineImage;
    private Image bottomLineImage;
    private Image backImage;

    private int width, height;
    private static float GAME_SCALE_VERTICAL_FEC = 0.64f;
    private static float GAME_SCALE_HORIZ_FEC = 0.9f;

    private void Start()
    {
        Init();
    }

    public void Bind(ChartPlayback playback, string registerName)
    {
        registerBuilder = registerName;
        registerLine = registerBuilder + "_Line";
        registerArea = registerBuilder + "_Area";
        registerLine1 = registerBuilder + "_Line1";
        registerLine2 = registerBuilder + "_Line2";
        registerBack = registerBuilder + "_Back";

        playback.Subscribe(registerBuilder, (lane, e) => SpotsProducer(e.payload, e.time));
        playback.Subscribe(registerLine, (lane, e) => LineMove(e.payload));
        playback.Subscribe(registerLine1, (lane, e) => TopScaleLineMove(e.payload));
        playback.Subscribe(registerLine2, (lane, e) => BottomScaleLineMove(e.payload));
        playback.Subscribe(registerArea, (lane, e) => AreaMove(e.payload));
        playback.Subscribe(registerBack, (lane, e) => BackChange(e.payload));
    }

    private void Init()
    {
        mainLineRect = mainLine.GetComponent<RectTransform>();
        topLineRect = topLine.GetComponent<RectTransform>();
        bottomLineRect = bottomLine.GetComponent<RectTransform>();
        areaRect = area.GetComponent<RectTransform>();
        mainLineImage = mainLine.GetComponent<Image>();
        topLineImage = topLine.GetComponent<Image>();
        bottomLineImage = bottomLine.GetComponent<Image>();
        backImage = back.GetComponent<Image>();

        musicBack = backImage.sprite;
        width = Screen.width / 2;
        height = Screen.height / 2;
    }

    private void SpotsProducer(string payload, float songTime)
    {
        if (string.IsNullOrEmpty(payload))
            return;

        SpotInfo[] spotInfos = GetInfo(payload);
        for (int i = 1; i <= spotInfos[0].len; i++)
        {
            switch (spotInfos[i].type)
            {
                case 0: // 单击音符
                    GameObject single = Instantiate(singlePrefab);
                    single.transform.SetParent(spotsPanel, false);
                    single.GetComponent<SingleSpotNew>().spawnSongTime = songTime;
                    single.GetComponent<RectTransform>().SetPositionAndRotation(ScreenFecToPos(new Vector2(spotInfos[i].posX,spotInfos[i].posY)), area.transform.localRotation);
                    single.transform.SetSiblingIndex(0); //将该UI元素在当前父级序列中的渲染位置调整为0（第一个），也就是叠在早生成元素的下方
                    break;
                case 1: // 触发音符
                    GameObject touch = Instantiate(touchPrefab);
                    touch.transform.SetParent(spotsPanel, false);
                    touch.GetComponent<TouchSpotNew>().spawnSongTime = songTime;
                    touch.GetComponent<RectTransform>().SetPositionAndRotation(ScreenFecToPos(new Vector2(spotInfos[i].posX, spotInfos[i].posY)), area.transform.localRotation);
                    touch.transform.SetSiblingIndex(0);
                    break;
                case 2: // 蓄力音符
                case 3:
                case 4:
                    GameObject press = Instantiate(pressPrefab);
                    press.transform.SetParent(spotsPanel, false);

                    press.GetComponent<PressSpotNew>().dir = spotInfos[i].dir;
                    press.GetComponent<PressSpotNew>().spd = spotInfos[i].len;
                    press.GetComponent<PressSpotNew>().type = spotInfos[i].type;
                    press.GetComponent<PressSpotNew>().spawnSongTime = songTime;

                    press.GetComponent<RectTransform>().SetPositionAndRotation(ScreenFecToPos(new Vector2(spotInfos[i].posX, spotInfos[i].posY)), area.transform.localRotation);
                    
                    press.transform.SetAsFirstSibling();
                    break;
            }

        }
    }

    private Vector3 ScreenFecToPos(Vector2 fecs)
    {
        Vector3 v = Camera.main.ScreenToWorldPoint(new Vector3(fecs.x * width * GAME_SCALE_HORIZ_FEC + width, fecs.y * height * GAME_SCALE_VERTICAL_FEC + height));
        v.z = 0;
        return v;
    }

    //存放音符的信息
    private class SpotInfo
    {
        public int type;
        public float posX;
        public float posY;
        public int dir;
        public int len;
        public SpotInfo() { }
    }

    // 转换负载的文本
    /*
     * 10142034802,10181123411
     * -> [0] len  = 2
     * 
     *    [1] type = 1
     *        posX = 1.42
     *        posY = 3.48
     *        dir  = 0
     *        len  = 2
     *        
     *    [2] type = 1
     *        posX = 1.81
     *        posY = -2.34
     *        dir  = 1
     *        len  = 1
     */
    private SpotInfo[] GetInfo(string s)
    {
        SpotInfo[] res = new SpotInfo[11];
        res[0] = new SpotInfo();
        int cnt = 0;
        string[] infos = s.Split(',');
        foreach (string info in infos)
        {
            //Debug.Log(info);
            long x = long.Parse(info);
            cnt++;
            res[cnt] = new SpotInfo();
            res[cnt].len = (int)(x % 10);
            x /= 10;
            res[cnt].dir = (int)(x % 10);
            x /= 10;
            res[cnt].posY = (x % 1000)/100f;
            x /= 1000;
            if (x % 10 == 1)
                res[cnt].posY *= -1;
            x /= 10;
            res[cnt].posX = (x % 1000)/100f;
            x /= 1000;
            if (x % 10 == 1)
                res[cnt].posX *= -1;
            x /= 10;
            res[cnt].type = (int)x;

            //Debug.Log(res[cnt].type.ToString() + "  " + res[cnt].posX.ToString() + "  " + res[cnt].posY.ToString() + "  " + res[cnt].dir.ToString() + "  " + res[cnt].len.ToString());
        }
        res[0].len = cnt;
        return res;
    }

    private void LineMove(string payload)
    {
        AnimChange(mainLineRect, mainLineImage, payload);
    }

    private void TopScaleLineMove(string payload)
    {
        AnimChange(topLineRect, topLineImage, payload);
    }

    private void BottomScaleLineMove(string payload)
    {
        AnimChange(bottomLineRect, bottomLineImage, payload);
    }
    
    private void AreaMove(string payload)
    {
        AnimChange(areaRect, payload);
    }

    private void AnimChange(RectTransform rect, Image img, string payload)
    {
        if (string.IsNullOrEmpty(payload))
            return;

        AnimInfo[] infos = GetAnimInfo(payload);
        for (int i = 1; i <= infos[0].type; i++)
        {
            switch (infos[i].type)
            {
                case 0: //线性移动
                    RectMove(rect, new Vector3(GetScreenPosX(infos[i].vecX/100f), GetScreenPosY(infos[i].vecY/100f), 0), infos[i].duration, 0);
                    break;
                case 1: //线性旋转
                    RectRotate(rect, new Vector3(infos[i].vecX, infos[i].vecY, infos[i].vecZ), infos[i].duration, 0);
                    break;
                case 2: //渐变
                    ImageFade(img, infos[i].vecX, infos[i].duration);
                    break;
                case 3: //开始动画
                    mainLineRect.DOScaleX(1, 0.6f).SetId<Tween>("game");
                    Invoke("StartMove", 0.2f);
                    topLineImage.DOFade(1, 0.5f).SetId<Tween>("game");
                    topLineRect.DOScale(1, 0.5f).SetId<Tween>("game");
                    topLineRect.DOLocalMove(new Vector3(0, GAME_SCALE_VERTICAL_FEC * height, 0), 0.5f).SetId<Tween>("game");
                    bottomLineImage.DOFade(1, 0.5f).SetId<Tween>("game");
                    bottomLineRect.DOScale(1, 0.5f).SetId<Tween>("game");
                    bottomLineRect.DOLocalMove(new Vector3(0, -1 * GAME_SCALE_VERTICAL_FEC * height, 0), 0.5f).SetId<Tween>("game");
                    break;
                case 4: //非线性移动
                    RectMove(rect, new Vector3(GetScreenPosX(infos[i].vecX / 100f), GetScreenPosY(infos[i].vecY / 100f), 0), infos[i].duration, 1);
                    break;
                case 5: //非线性旋转
                    RectRotate(rect, new Vector3(infos[i].vecX, infos[i].vecY, infos[i].vecZ), infos[i].duration, 1);
                    break;
            }
        }
    }

    private float GetScreenPosX(float posFec)
    {
        return posFec * GAME_SCALE_HORIZ_FEC * width;
    }

    private float GetScreenPosY(float posFec)
    {
        return posFec * GAME_SCALE_VERTICAL_FEC * height;
    }

    private void StartMove()
    {
        mainLineRect.DOLocalMove(new Vector3(0,height * GAME_SCALE_VERTICAL_FEC,0),0.4f).SetId<Tween>("game");
    }

    private void AnimChange(RectTransform rect, string payload)
    {
        if (string.IsNullOrEmpty(payload))
            return;

        AnimInfo[] infos = GetAnimInfo(payload);
        for (int i = 1; i <= infos[0].type; i++)
        {
            switch (infos[i].type)
            {
                case 0: //移动
                    RectMove(rect, new Vector3(GetScreenPosX(infos[i].vecX / 100f), GetScreenPosY(infos[i].vecY / 100f), 0), infos[i].duration, 0);
                    break;
                case 1: //旋转
                    RectRotate(rect, new Vector3(infos[i].vecX, infos[i].vecY, infos[i].vecZ), infos[i].duration, 0);
                    break;
                case 4: //非线性移动
                    RectMove(rect, new Vector3(GetScreenPosX(infos[i].vecX / 100f), GetScreenPosY(infos[i].vecY / 100f), 0), infos[i].duration, 1);
                    break;
                case 5: //非线性旋转
                    RectRotate(rect, new Vector3(infos[i].vecX, infos[i].vecY, infos[i].vecZ), infos[i].duration, 1);
                    break;
            }
        }
    }

    /*
     * 0：背景图变黑
     * 1：背景图恢复
     */
    private void BackChange(string payload)
    {
        if (!int.TryParse(payload, out int value))
            return;
        switch (value)
        {
            case 0: //背景图变黑
                BackTurnBlack();
                break;
            case 1: //背景图转为曲绘
                BackTurnMusicBack();
                break;
        }
    }

    //存储动画信息
    private class AnimInfo
    {
        public int type;
        public int vecX;
        public int vecY;
        public int vecZ;
        public float duration;
        public AnimInfo() { }
    }

    /*
     * 1 0100 1100 0101 101
     * -> [0] type = 1;
     * 
     *    [1] type = 1;
     *        vecX = 100;
     *        vecY = -100;
     *        vecZ = 101;
     *        duration = 1.01
     */
    private AnimInfo[] GetAnimInfo(string s)
    {
        AnimInfo[] infos = new AnimInfo[10];
        infos[0] = new AnimInfo();
        int cnt = 0;
        string[] ss = s.Split(',');
        foreach (string tmp in ss)
        {
            long x = long.Parse(tmp);
            cnt++;
            infos[cnt] = new AnimInfo();
            infos[cnt].duration = (x % 1000) / 100f;
            x /= 1000;
            infos[cnt].vecZ = (int)(x % 1000);
            x /= 1000;
            if (x % 10 == 1)
                infos[cnt].vecZ *= -1;
            x /= 10;
            infos[cnt].vecY = (int)(x % 1000);
            x /= 1000;
            if (x % 10 == 1)
                infos[cnt].vecY *= -1;
            x /= 10;
            infos[cnt].vecX = (int)(x % 1000);
            x /= 1000;
            if (x % 10 == 1)
                infos[cnt].vecX *= -1;
            x /= 10;
            infos[cnt].type = (int)x;
        }
        infos[0].type = cnt;
        return infos;
    }

    private void RectMove(RectTransform rect, Vector3 endVector, float duration, int mode)
    {
        if (mode == 0)
            rect.DOLocalMove(endVector, duration).SetEase(Ease.Linear).SetId<Tween>("game");
        else if (mode == 1)
            rect.DOLocalMove(endVector, duration).SetId<Tween>("game");
    }

    private void RectRotate(RectTransform rect, Vector3 euler, float duration, int mode)
    {
        if (mode == 0)
            rect.DORotate(euler, duration).SetEase(Ease.Linear).SetId<Tween>("game");
        else if (mode == 1)
            rect.DORotate(euler, duration).SetId<Tween>("game");
    }

    private void ImageFade(Image img, float endFade, float duration)
    {
        img.DOFade(endFade, duration).SetId<Tween>("game");
    }

    private void BackTurnBlack()
    {
        blackInGame.DOFade(1, 0.6f).SetId<Tween>("game");
    }

    private void BackTurnMusicBack()
    {
        blackInGame.DOFade(0, 0.6f).SetId<Tween>("game");
    }
}
