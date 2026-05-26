

using GoveKits.Runtime.Core;

public class CardUsedEvent : EventData
{
    public BaseCharacter User { get; set; }
    public BaseCharacter Target { get; set; }
    public BaseCard Card { get; set; }

    public override void OnRecycle()
    {
        User = null;
        Target = null;
        Card = null;
    }
}