


public class Player : BaseCharacter
{
    public const int HandLimit = 8;



    // 玩家行动
    public bool isPlayerTurn { get; private set; } = false;
    public override void StartTurn()
    {
        base.StartTurn();

        // 这样可以在其他地方根据这个属性来判断是否是玩家的回合了，比如UI显示等逻辑，也可以在玩家的卡牌使用逻辑中判断是否允许玩家使用卡牌了。
        isPlayerTurn = true;
    }


    public override void EndTurn()
    {
        isPlayerTurn = false;

        base.EndTurn();
    }
}