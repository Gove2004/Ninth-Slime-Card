using GoveKits.Runtime.Core;
using UnityEngine;



public class BattleManager : MonoSingleton<BattleManager>
{
    public bool isBattleActive { get; private set; } = false;
    public Player player { get; private set; }
    public Enemy enemy { get; private set; }
    public int turnCount { get; private set; } = 0;


    // 战斗起点
    public void StartBattle()
    {
        isBattleActive = true;

        turnCount = 0;
        this.player = Object.FindFirstObjectByType<Player>();
        this.enemy = Object.FindFirstObjectByType<Enemy>();

        // 设置目标
        player.Target = enemy;
        enemy.Target = player;

        player.Health.Value = 100;
        player.Mana.Value = 100;

        // 开始玩家回合
        player.StartTurn();
    }


    public void StepTurn()
    {
        turnCount++;
        if (player.isPlayerTurn)
        {
            // 结束玩家回合，开始敌人回合
            player.EndTurn();
            enemy.StartTurn();
        }
        else
        {
            // 结束敌人回合，开始玩家回合
            enemy.EndTurn();
            player.StartTurn();
        }
    }


    public void EndBattle()
    {
        if (!isBattleActive) return;
        isBattleActive = false;
        
        // 这里可以放一些结算逻辑，比如显示胜利/失败界面，重置状态等
        // ......
    }
}
