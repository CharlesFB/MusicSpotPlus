using System;
using System.Collections.Generic;
using System.Globalization;
using FB.Metronome.Data;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[DefaultExecutionOrder(-60)]
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

    GameObjectPool singlePool;
    GameObjectPool touchPool;
    GameObjectPool pressPool;
    NoteBoard board;
    Camera cam;
    bool runtimeReady;
    readonly Dictionary<string, AnimInfo[]> animCache = new Dictionary<string, AnimInfo[]>();
    readonly List<QueuedNote> queue = new List<QueuedNote>();
    readonly List<NoteRecipe> recipes = new List<NoteRecipe>();
    TempoMap tempo;
    int queueIndex;
    float lastSong = -1f;

    struct QueuedNote
    {
        public float time;
        public string payload;
    }

    private void Awake()
    {
        EnsureRuntime();
    }

    private void Start()
    {
        Init();
    }

    void EnsureRuntime()
    {
        if (runtimeReady)
            return;
        runtimeReady = true;
        if (GameServices.Results == null)
            GameServices.Results = FindObjectOfType<ResultManager>();
        if (GameServices.Effects == null)
            GameServices.Effects = FindObjectOfType<EffectController>();
        cam = Camera.main;
        GameServices.Camera = cam;
        HitSound hits = GetComponent<HitSound>();
        if (hits == null)
            hits = gameObject.AddComponent<HitSound>();
        GameServices.Hits = hits;
        board = GetComponent<NoteBoard>();
        if (board == null)
            board = gameObject.AddComponent<NoteBoard>();
        GameServices.Board = board;
        if (singlePrefab != null)
            singlePool = new GameObjectPool(singlePrefab, 24);
        if (touchPrefab != null)
            touchPool = new GameObjectPool(touchPrefab, 12);
        if (pressPrefab != null)
            pressPool = new GameObjectPool(pressPrefab, 8);
    }

    public void Bind(ChartPlayback playback, string registerName, MusicChart chart)
    {
        EnsureRuntime();
        Warm(chart);
        BuildChart(chart);
        registerBuilder = registerName;
        registerLine = registerBuilder + "_Line";
        registerArea = registerBuilder + "_Area";
        registerLine1 = registerBuilder + "_Line1";
        registerLine2 = registerBuilder + "_Line2";
        registerBack = registerBuilder + "_Back";

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

    void Update()
    {
        if (tempo == null || board == null || width == 0)
            return;
        ChartPlayback playback = ChartPlayback.Instance;
        if (playback == null)
            return;
        float song = (float)playback.SongTime;
        if (song + 0.05f < lastSong)
        {
            queueIndex = 0;
            RecycleBoard();
        }
        lastSong = song;
        while (queueIndex < queue.Count)
        {
            float hit = queue[queueIndex].time;
            float ticksLeft = tempo.TickAt(hit) - tempo.TickAt(song);
            if (ticksLeft > ChartGrid.TicksPerQuarter)
                break;
            SpawnPayload(queue[queueIndex].payload, hit);
            queueIndex++;
        }
        FollowNotes(song);
    }

    void BuildChart(MusicChart chart)
    {
        tempo = new TempoMap();
        float bpm = GameInfoSingleton.instance != null ? GameInfoSingleton.instance.bpm : ChartGrid.ReferenceBpm;
        tempo.SetBase(bpm);
        queue.Clear();
        queueIndex = 0;
        lastSong = -1f;
        if (chart != null && chart.lanes != null)
        {
            foreach (EventLane lane in chart.lanes)
            {
                if (lane == null || lane.events == null)
                    continue;
                bool tempoLane = lane.laneId == "tempo";
                bool spots = lane.laneId == "spots";
                foreach (MetroEvent e in lane.events)
                {
                    if (tempoLane)
                    {
                        AddTempo(e.time, e.payload);
                        continue;
                    }
                    if (spots && !string.IsNullOrEmpty(e.payload))
                        queue.Add(new QueuedNote { time = e.time, payload = e.payload });
                }
            }
        }
        queue.Sort((a, b) => a.time.CompareTo(b.time));
        ChartSession.Tempo = tempo;
    }

    void AddTempo(float time, string payload)
    {
        if (string.IsNullOrEmpty(payload))
            return;
        string token = payload.Split(',')[0].Trim();
        if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float bpm))
            tempo.Add(time, bpm);
    }

    void SpawnPayload(string payload, float hitTime)
    {
        if (spotsPanel == null)
            return;
        NotePayload.Read(payload, recipes);
        for (int i = 0; i < recipes.Count; i++)
        {
            NoteRecipe recipe = recipes[i];
            if (recipe.type == 1)
                SpawnTouch(recipe, hitTime);
            else if (recipe.type >= 2)
                SpawnPress(recipe, hitTime);
            else
                SpawnSingle(recipe, hitTime);
        }
    }

    void SpawnSingle(NoteRecipe recipe, float hitTime)
    {
        if (singlePool == null)
            return;
        GameObject single = singlePool.Get(spotsPanel);
        SingleSpotNew spot = single.GetComponent<SingleSpotNew>();
        spot.Prepare(hitTime);
        Place(single, spot, recipe);
        board.Add(spot);
    }

    void SpawnTouch(NoteRecipe recipe, float hitTime)
    {
        if (touchPool == null)
            return;
        GameObject touch = touchPool.Get(spotsPanel);
        TouchSpotNew spot = touch.GetComponent<TouchSpotNew>();
        spot.Prepare(hitTime);
        Place(touch, spot, recipe);
        board.Add(spot);
    }

    void SpawnPress(NoteRecipe recipe, float hitTime)
    {
        if (pressPool == null)
            return;
        GameObject press = pressPool.Get(spotsPanel);
        PressSpotNew spot = press.GetComponent<PressSpotNew>();
        spot.dir = recipe.dir;
        spot.spd = recipe.rateIndex;
        spot.type = recipe.type;
        spot.Prepare(hitTime);
        Place(press, spot, recipe);
        spot.ApplyDirection();
        board.Add(spot);
    }

    void Place(GameObject note, Spot spot, NoteRecipe recipe)
    {
        spot.useGrid = true;
        spot.gridColumn = recipe.column;
        RectTransform rect = note.GetComponent<RectTransform>();
        rect.SetPositionAndRotation(ScreenFecToPos(new Vector2(ChartGrid.ColumnToFec(recipe.column), LineFec())), area.transform.localRotation);
        Quaternion rotation = rect.localRotation;
        rect.localEulerAngles = Vector3.zero;
        rect.localRotation = rotation;
        note.transform.SetAsFirstSibling();
    }

    void FollowNotes(float song)
    {
        float nowTick = tempo.TickAt(song);
        float step = ChartGrid.PitchScale(tempo.BpmAt(song)) / ChartGrid.TicksPerQuarter;
        float lineFec = LineFec();
        List<Spot> active = board.Active;
        for (int i = 0; i < active.Count; i++)
        {
            Spot spot = active[i];
            if (!spot.useGrid)
                continue;
            float ticksLeft = tempo.TickAt(spot.spawnSongTime) - nowTick;
            if (ticksLeft < 0f)
                ticksLeft = 0f;
            float y = lineFec + ticksLeft * step;
            spot.transform.position = ScreenFecToPos(new Vector2(ChartGrid.ColumnToFec(spot.gridColumn), y));
        }
    }

    float LineFec()
    {
        if (mainLineRect == null || height == 0)
            return 0f;
        float span = height * GAME_SCALE_VERTICAL_FEC;
        if (span < 0.001f)
            return 0f;
        return mainLineRect.localPosition.y / span;
    }

    void RecycleBoard()
    {
        List<Spot> active = board.Active;
        for (int i = active.Count - 1; i >= 0; i--)
            active[i].Recycle();
    }

    void Warm(MusicChart chart)
    {
        if (chart == null || chart.lanes == null)
            return;
        foreach (EventLane lane in chart.lanes)
        {
            if (lane == null || lane.events == null)
                continue;
            if (lane.laneId == "spots" || lane.laneId == "back" || lane.laneId == "tempo")
                continue;
            foreach (var e in lane.events)
            {
                if (!string.IsNullOrEmpty(e.payload))
                    AnimFor(e.payload);
            }
        }
    }

    AnimInfo[] AnimFor(string payload)
    {
        if (animCache.TryGetValue(payload, out AnimInfo[] cached))
            return cached;
        AnimInfo[] parsed = GetAnimInfo(payload);
        animCache[payload] = parsed;
        return parsed;
    }

    private Vector3 ScreenFecToPos(Vector2 fecs)
    {
        if (cam == null)
            cam = Camera.main;
        Vector3 v = cam.ScreenToWorldPoint(new Vector3(fecs.x * width * GAME_SCALE_HORIZ_FEC + width, fecs.y * height * GAME_SCALE_VERTICAL_FEC + height));
        v.z = 0;
        return v;
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

            AnimInfo[] infos = AnimFor(payload);
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

            AnimInfo[] infos = AnimFor(payload);
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
