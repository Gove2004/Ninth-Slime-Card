



public class Enemy : BaseCharacter
{



    public override void StartTurn()
    {
        base.StartTurn();
        // 敌人行动逻辑
        // ......
    }


    public override void EndTurn()
    {
        
        // 敌人结束回合逻辑
        // ......
        base.EndTurn();
    }



    public void AIAction()
    {
        // 这里可以放一些简单的AI逻辑，比如随机使用手牌，或者攻击玩家等
        // ......
    }
}