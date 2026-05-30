
using System.Collections.Generic;
using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using UnityEngine;

public class MessageToastManager : MonoSingleton<MessageToastManager>
{
    private GameObject toastPrefab;
    private readonly Queue<string> messageQueue = new();
    private float nextShowTime;

    protected override void Awake()
    {
        base.Awake();

        toastPrefab = ResCore.LoadAssetSync<GameObject>("MessageToast").GetAssetObject<GameObject>();
    }

    private void Update()
    {
        if (messageQueue.Count > 0 && Time.unscaledTime >= nextShowTime)
        {
            string message = messageQueue.Dequeue();
            ShowMessageImmediate(message);
            nextShowTime = Time.unscaledTime + 0.5f;
        }
    }

    public void ShowMessage(string message, float duration = 2f)
    {
        messageQueue.Enqueue(message);
    }

    private void ShowMessageImmediate(string message, float duration = 2f)
    {
        var toastObj = Instantiate(toastPrefab, transform);
        var toast = toastObj.GetComponent<MessageToast>();
        toast.ShowMessage(message, duration);
    }
}
