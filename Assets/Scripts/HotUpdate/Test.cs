using GoveKits.Runtime.Storage;
using UnityEngine;

public class Test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CardConfigData data = ConfigCore.Load<CardConfigData>((data) => data.id == 1000)[0];

            MessageToastManager.Instance.ShowMessage($"卡牌名称: {data.名称}\n系列: {data.系列}\n费用: {data.费用}\n数值: {data.数值}\n持续时间: {data.持续时间}\n效果: {data.效果}\n趣闻: {data.趣闻}\n备注: {data.备注}");
        }


    }
}
