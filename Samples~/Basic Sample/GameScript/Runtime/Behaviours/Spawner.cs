using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject SpawnGo;

    void Start()
    {
        UnityEngine.Debug.Log("��ʼ���ƹ��ص�GameObject");
        GameObject.Instantiate<GameObject>(SpawnGo, this.transform);
    }
}