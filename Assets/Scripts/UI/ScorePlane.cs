using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScorePlane : MonoBehaviour
{
    public GameObject SinglePrefab;
    public string eventId;

    // Start is called before the first frame update
    void Start()
    {
        if (string.IsNullOrEmpty(eventId) || ChartPlayback.Instance == null)
            return;
        ChartPlayback.Instance.Subscribe(eventId, (lane, e) => SingleSpotProduce());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void SingleSpotProduce()
    {
        GameObject tmp = Instantiate(SinglePrefab);
        tmp.transform.position = new Vector3(0, 0, 0);
        tmp.transform.parent = this.transform;
    }
}
