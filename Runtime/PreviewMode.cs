using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PreviewMode : MonoBehaviour
{
    public AudioSource AS;
    // Start is called before the first frame update
    void Start()
    {
       
        //   Debug.Log("channels:"+AS.clip.channels);
    }

    // Update is called once per frame
    void Update()
    {
        if(AS == null)
        {
            AS = this.gameObject.GetComponent<AudioSource>();
            return;
        }
        float[] samples = new float[2];
        AS.GetOutputData(samples, 1);
        Debug.Log("Length                            " + samples.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            Debug.Log(samples[i]);
        }
    }
}
