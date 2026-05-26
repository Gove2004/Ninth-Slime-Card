using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    // Player HUD
    private Player player;
    [SerializeField] private RollTMP hpText;
    [SerializeField] private RollTMP manaText;

    // Enemy HUD
    private Enemy enemy;
    [SerializeField] private RollTMP enemyHpText;
    [SerializeField] private Image enemyHpBar;



    private void Update()
    {
        // 更新玩家HUD
        if (BattleManager.Instance == null) return; // 确保BattleManager存在

        if (player == null)
        {
            player = BattleManager.Instance.player;
        }
        else
        {
            UpdatePlayerHUD();
        }
        

        // 更新敌人HUD
        if (enemy == null)
        {
            enemy = BattleManager.Instance.enemy;
        }
        else
        {
            UpdateEnemyHUD();
        }
    }


    private void UpdatePlayerHUD()
    {
        hpText.StopAndSetFinalValue("{0}", player.Health.Value);
        manaText.StopAndSetFinalValue("{0}", player.Mana.Value);
    }

    private void UpdateEnemyHUD()
    {
        enemyHpText.StopAndSetFinalValue("{0}", enemy.Health.Value);
        enemyHpBar.fillAmount = enemy.HealthPercent;
    }

}
