

using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using UnityEngine;

public class MessageToastManager : MonoSingleton<MessageToastManager>
{
    private GameObject toastPrefab;

    protected override void Awake()
    {
        base.Awake();
        
        toastPrefab = ResCore.LoadAssetSync<GameObject>("MessageToast").GetAssetObject<GameObject>();
    }


    public void ShowMessage(string message, float duration = 2f)
    {
        var toastObj = Instantiate(toastPrefab, transform);
        var toast = toastObj.GetComponent<MessageToast>();
        toast.ShowMessage(message, duration);
    }
}
